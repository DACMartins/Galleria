using Galleria.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Galleria.Models;
using Microsoft.AspNetCore.Authorization;

namespace Galleria.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        // GET: /Admin/Users/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        // POST: /Admin/Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            // Se quiseres atribuir logo uma role no futuro:
            // if (!string.IsNullOrEmpty(model.RoleName))
            // {
            //     await _userManager.AddToRoleAsync(user, model.RoleName);
            // }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Users/ManageRoles/some-guid
        public async Task<IActionResult> ManageRoles(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new RoleManagementViewModel
            {
                UserId = user.Id,
                UserName = user.UserName,
                Roles = new List<RoleViewModel>()
            };

            foreach (var role in _roleManager.Roles)
            {
                var roleViewModel = new RoleViewModel
                {
                    RoleName = role.Name,
                    IsSelected = await _userManager.IsInRoleAsync(user, role.Name)
                };
                viewModel.Roles.Add(roleViewModel);
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ManageRoles(RoleManagementViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, roles);
            await _userManager.AddToRolesAsync(user, model.Roles.Where(r => r.IsSelected).Select(r => r.RoleName));

            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Users/Delete/123
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Impede apagar o próprio admin
            if (user.UserName == User.Identity?.Name)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // POST: /Admin/Users/DeleteConfirmed/123
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Impede apagar-se a si próprio
            if (user.UserName == User.Identity?.Name)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            // Impede apagar o ÚLTIMO admin existente
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");

                if (admins.Count <= 1)
                {
                    TempData["Error"] = "You cannot delete the last admin account.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await _userManager.DeleteAsync(user);

            TempData["Success"] = "User deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

    }
}
