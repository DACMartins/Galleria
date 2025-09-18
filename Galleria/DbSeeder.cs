using Galleria.Data; // Add this using statement
using Galleria.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Add this using statement

namespace Galleria
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            //Required Services
            var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = service.GetRequiredService<UserManager<ApplicationUser>>();

            //2. ADDING ROLES (with checks)
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }

            //Creating admin user
            var admin = new ApplicationUser 
            {
                UserName = "admin@example.com",
                Email = "admin@example.com",
                EmailConfirmed = true
            };

            var userInDb = await userManager.FindByEmailAsync(admin.Email);
            if (userInDb == null)
            {
                await userManager.CreateAsync(admin, "Adm123!"); //Example of a password 
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            var context = service.GetRequiredService<ApplicationDbContext>();
            if (!await context.Categories.AnyAsync())
            {
                await context.Categories.AddRangeAsync(
                    new Category { Name = "Conferences" },
                    new Category { Name = "Training" },
                    new Category { Name = "Events" }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
