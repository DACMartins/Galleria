using Galleria.Data;
using Galleria.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Galleria.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MediaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public MediaController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }
        // GET: /Admin/Media
        public async Task<IActionResult> Index()
        {
            var allMedia = await _context.MediaItems
                .Include(m => m.ApplicationUser)
                .Include(m => m.Category)
                .OrderByDescending(m => m.UploadDate)
                .ToListAsync();

            return View(allMedia);
        }
        // GET: /Admin/Media/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var mediaItem = await _context.MediaItems
                .Include(m => m.ApplicationUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mediaItem == null)
            {
                return NotFound();
            }

            return View(mediaItem);
        }

        // POST: /Admin/Media/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mediaItem = await _context.MediaItems.FindAsync(id);
            if (mediaItem != null)
            {
                // Delete physical files from wwwroot
                var mainFilePath = Path.Combine(_webHostEnvironment.WebRootPath, mediaItem.FilePath.TrimStart('/'));
                var thumbFilePath = Path.Combine(_webHostEnvironment.WebRootPath, mediaItem.ThumbnailPath.TrimStart('/'));

                if (System.IO.File.Exists(mainFilePath))
                {
                    System.IO.File.Delete(mainFilePath);
                }
                if (System.IO.File.Exists(thumbFilePath))
                {
                    System.IO.File.Delete(thumbFilePath);
                }

                _context.MediaItems.Remove(mediaItem);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
        // GET: /Admin/Media/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var mediaItem = await _context.MediaItems
                                          .Include(m => m.Keywords)
                                          .FirstOrDefaultAsync(m => m.Id == id);
            if (mediaItem == null)
            {
                return NotFound();
            }

            var viewModel = new EditMediaViewModel
            {
                Id = mediaItem.Id,
                Title = mediaItem.Title,
                Description = mediaItem.Description,
                CategoryId = mediaItem.CategoryId,
                Tags = string.Join(", ", mediaItem.Keywords.Select(k => k.Text)),
                Categories = new SelectList(_context.Categories, "Id", "Name", mediaItem.CategoryId)
            };

            return View(viewModel);
        }

        // POST: /Admin/Media/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(EditMediaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories = new SelectList(_context.Categories, "Id", "Name", model.CategoryId);
                return View(model);
            }

            var mediaItem = await _context.MediaItems
                                          .Include(m => m.Keywords)
                                          .FirstOrDefaultAsync(m => m.Id == model.Id);

            if (mediaItem == null)
            {
                return NotFound();
            }

            mediaItem.Title = model.Title;
            mediaItem.Description = model.Description;
            mediaItem.CategoryId = model.CategoryId;

            // Update logic for tags can be added here, similar to the Upload method

            _context.Update(mediaItem);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        // Inside Areas/Admin/Controllers/MediaController.cs

        [HttpPost]
        public async Task<IActionResult> GenerateShareLink(int id)
        {
            var mediaItem = await _context.MediaItems.FindAsync(id);
            if (mediaItem == null)
            {
                return NotFound();
            }

            mediaItem.ShareToken = Guid.NewGuid().ToString(); // Create a new unique token
            await _context.SaveChangesAsync();

            // Generate the full public URL
            var shareUrl = Url.Action("Share", "Media", new { token = mediaItem.ShareToken }, Request.Scheme);

            return Ok(new { url = shareUrl }); // Return the URL as JSON
        }
    }
}