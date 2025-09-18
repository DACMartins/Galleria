using Galleria.Data;
using Galleria.Models;
using Galleria.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Xabe.FFmpeg;
using Microsoft.AspNetCore.Identity;


namespace Galleria.Controllers
{

    [Authorize] // Ensures only logged-in users can access this
    public class MediaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;

        public MediaController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        // GET: /Media/Upload
        public async Task<IActionResult> Upload()
        {
            var viewModel = new UploadMediaViewModel
            {
                // Load categories from the database to populate the dropdown
                Categories = await _context.Categories
                                           .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                                           .ToListAsync()
            };
            return View(viewModel);
        }

        // POST: /Media/Upload
        [HttpPost]
        public async Task<IActionResult> Upload(UploadMediaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // If the model is not valid, reload the categories and return the view with errors
                model.Categories = await _context.Categories
                                           .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                                           .ToListAsync();
                return View(model);
            }

            // --- FOLDER SETUP ---
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            string thumbnailsFolder = Path.Combine(uploadsFolder, "thumbnails");
            Directory.CreateDirectory(uploadsFolder); // This does nothing if the folder already exists
            Directory.CreateDirectory(thumbnailsFolder);

            // 1. Create a unique filename
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.File.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 2. Save the file to the server
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.File.CopyToAsync(fileStream);
            }

            // --- THUMBNAIL GENERATION ---
            string thumbnailFileName = "thumb_" + uniqueFileName.Split('.')[0] + ".jpg";
            string thumbnailPath = Path.Combine(thumbnailsFolder, thumbnailFileName);
            string webThumbnailPath = "/uploads/thumbnails/" + thumbnailFileName;

            bool isVideo = model.File.ContentType.StartsWith("video");
            if (isVideo)
            {
                // Video thumbnail logic
                FFmpeg.SetExecutablesPath("C:\\ffmpeg\\bin"); // IMPORTANT: Change this path to where you extract FFmpeg
                var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(filePath, thumbnailPath, TimeSpan.FromSeconds(1));
                await conversion.Start();
            }
            else
            {
                // Image thumbnail logic
                using (var image = await Image.LoadAsync(model.File.OpenReadStream()))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(400, 400),
                        Mode = ResizeMode.Crop
                    }));
                    await image.SaveAsJpegAsync(thumbnailPath);
                }
            }

            // Get current user's ID
            var userId = _userManager.GetUserId(User);

            // 3. Create a new MediaItem entity
            var mediaItem = new MediaItem
            {
                Title = model.Title,
                Description = model.Description,
                FilePath = "/uploads/" + uniqueFileName,
                ThumbnailPath = webThumbnailPath,
                Type = isVideo ? MediaType.Video : MediaType.Photo,
                UploadDate = DateTime.UtcNow,
                CategoryId = model.CategoryId,
                ApplicationUserId = userId // <-- SET THE USER ID HERE
            };

            // 4. Save the new record to the database
            _context.MediaItems.Add(mediaItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home"); // Or redirect to a gallery page
        }
    }
}
