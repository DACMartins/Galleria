using Galleria.Data;
using Galleria.Models;
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
                .Where(m => !m.IsDeleted)
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
                mediaItem.IsDeleted = true; // Set the flag
                _context.Update(mediaItem);
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

            // ---------------- TAG UPDATE LOGIC ----------------

            // 1. Parse & normalize tags from view model
            var tagNames = (model.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 2. Remove tags that are no longer present
            var tagsToRemove = mediaItem.Keywords
                .Where(k => !tagNames.Contains(k.Text, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var keyword in tagsToRemove)
            {
                mediaItem.Keywords.Remove(keyword);
            }

            // 3. Add / attach tags that should be present

            // Fetch existing Keyword entities for these tag names
            var existingKeywords = await _context.Keywords
                .Where(k => tagNames.Contains(k.Text))
                .ToListAsync();

            // Attach existing keywords that are not yet on this media item
            foreach (var keyword in existingKeywords)
            {
                if (!mediaItem.Keywords.Any(k => k.Id == keyword.Id))
                {
                    mediaItem.Keywords.Add(keyword);
                }
            }

            // Find which tag names don't exist as Keyword entities yet
            var existingKeywordNames = existingKeywords
                .Select(k => k.Text)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newTagNames = tagNames
                .Where(name => !existingKeywordNames.Contains(name))
                .ToList();

            // Create new Keyword entities for brand-new tags
            foreach (var name in newTagNames)
            {
                var keyword = new Keyword { Text = name };
                mediaItem.Keywords.Add(keyword);
                // EF will track and insert this via cascade
            }


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