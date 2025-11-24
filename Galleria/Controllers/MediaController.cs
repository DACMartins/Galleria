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
                model.Categories = await _context.Categories.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToListAsync();
                return View(model);
            }

            // ... (Folder setup and saving the original file remains the same) ...
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            string thumbnailsFolder = Path.Combine(uploadsFolder, "thumbnails");
            Directory.CreateDirectory(uploadsFolder);
            Directory.CreateDirectory(thumbnailsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.File.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.File.CopyToAsync(fileStream);
            }

            // --- UPDATED THUMBNAIL GENERATION LOGIC ---
            string thumbnailFileName = "thumb_" + Path.GetFileNameWithoutExtension(uniqueFileName) + ".jpg";
            string thumbnailPath = Path.Combine(thumbnailsFolder, thumbnailFileName);
            string webThumbnailPath = "/uploads/thumbnails/" + thumbnailFileName;

            // Check if a manual thumbnail was uploaded
            if (model.OptionalThumbnailFile != null && model.OptionalThumbnailFile.Length > 0)
            {
                // If yes, save the user's provided thumbnail
                using (var fileStream = new FileStream(thumbnailPath, FileMode.Create))
                {
                    await model.OptionalThumbnailFile.CopyToAsync(fileStream);
                }
            }
            else
            {
                // If no, use the automatic generation as a fallback
                bool isVideo = model.File.ContentType.StartsWith("video");
                if (isVideo)
                {
                    FFmpeg.SetExecutablesPath("C:\\ffmpeg\\bin");
                    var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(filePath, thumbnailPath, TimeSpan.FromSeconds(1));
                    await conversion.Start();
                }
                else
                {
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
            }

            // ... (Saving the MediaItem to the database remains the same) ...
            var mediaItem = new MediaItem
            {
                Title = model.Title,
                Description = model.Description,
                FilePath = "/uploads/" + uniqueFileName,
                ThumbnailPath = webThumbnailPath,
                // ... etc.
            };
            // ... (Tag processing and saving logic) ...

            return RedirectToAction("Index", "Home");
        }

        // Details view for a specific media item

        public async Task<IActionResult> Details(int id)
        {
            var mediaItem = await _context.MediaItems
                .Include(m => m.Category)        // Include the Category information
                .Include(m => m.ApplicationUser) // Include the User information
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mediaItem == null)
            {
                return NotFound(); // Return a 404 error if no item is found
            }

            return View(mediaItem);
        }


        // GET: /Media/ or /Media/Index
        public async Task<IActionResult> Index(int? SelectedCategoryId, string searchKeyword, string selectedType, DateTime? selectedDate, int page = 1)
        {
            int pageSize = 9;

            IQueryable<MediaItem> mediaQuery = _context.MediaItems.Where(m => !m.IsDeleted);

            // ... (all your existing filter logic remains the same) ...
            if (SelectedCategoryId.HasValue) { /* ... */ }
            if (!string.IsNullOrEmpty(searchKeyword)) { /* ... */ }
            if (!string.IsNullOrEmpty(selectedType)) { /* ... */ }
            if (selectedDate.HasValue) { /* ... */ }

            // --- NEW MANUAL PAGINATION LOGIC ---

            // 1. Get the total count of items that match the filter
            var totalItems = await mediaQuery.CountAsync();

            // 2. Fetch only the items for the current page
            var pagedMediaItems = await mediaQuery
                .OrderByDescending(m => m.UploadDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new GalleryItemViewModel
                {
                    Id = m.Id,
                    Title = m.Title,
                    ThumbnailPath = m.ThumbnailPath
                })
                .ToListAsync();

            // 3. Create the final ViewModel
            var viewModel = new GalleryViewModel
            {
                MediaItems = pagedMediaItems,
                PagingInfo = new PagingInfo
                {
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalItems = totalItems
                },
                Categories = new SelectList(await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(), "Id", "Name", SelectedCategoryId),
                SelectedCategoryId = SelectedCategoryId,
                SearchKeyword = searchKeyword,
                SelectedType = selectedType,
                SelectedDate = selectedDate
            };

            return View(viewModel);
        }


        [AllowAnonymous] // Allows access without logging in
        public async Task<IActionResult> Share(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return NotFound();
            }

            var mediaItem = await _context.MediaItems.FirstOrDefaultAsync(m => m.ShareToken == token);

            if (mediaItem == null)
            {
                return NotFound();
            }

            // create a simple view for this
            return View("Share", mediaItem);
        }

    }
}
