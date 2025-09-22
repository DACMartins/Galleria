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
                ApplicationUserId = _userManager.GetUserId(User),
                Keywords = new List<Keyword>() // Initialize the Keywords collection
            };

            if (!string.IsNullOrEmpty(model.Tags))
            {
                var tagNames = model.Tags.Split(',').Select(t => t.Trim().ToLower()).ToList();
                foreach (var tagName in tagNames)
                {
                    if (string.IsNullOrWhiteSpace(tagName)) continue;

                    // Check if the keyword already exists
                    var keyword = await _context.Keywords.FirstOrDefaultAsync(k => k.Text == tagName);
                    if (keyword == null)
                    {
                        // If it doesn't exist, create it
                        keyword = new Keyword { Text = tagName };
                        _context.Keywords.Add(keyword);
                    }
                    // Add the keyword to the media item
                    mediaItem.Keywords.Add(keyword);
                }
            }

            // 4. Save the new record to the database
            _context.MediaItems.Add(mediaItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home"); // Or redirect to a gallery page
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
        public async Task<IActionResult> Index(int? SelectedCategoryId, string searchKeyword, string selectedType, DateTime? selectedDate)
        {
            IQueryable<MediaItem> mediaQuery = _context.MediaItems.Include(m => m.Category);

            if (SelectedCategoryId.HasValue)
            {
                mediaQuery = mediaQuery.Where(m => m.CategoryId == SelectedCategoryId.Value);
            }
            // Inside the Index method in MediaController.cs

            if (!string.IsNullOrEmpty(searchKeyword))
            {
                // --- UPDATE THIS LINE ---
                mediaQuery = mediaQuery.Where(m =>
                    m.Title.Contains(searchKeyword) ||
                    m.Description.Contains(searchKeyword) ||
                    m.Keywords.Any(k => k.Text.Contains(searchKeyword)) // Search in keywords
                );
            }

            // --- ADD THIS LOGIC ---
            if (!string.IsNullOrEmpty(selectedType))
            {
                if (Enum.TryParse<MediaType>(selectedType, out var mediaType))
                {
                    mediaQuery = mediaQuery.Where(m => m.Type == mediaType);
                }
            }
            if (selectedDate.HasValue)
            {
                mediaQuery = mediaQuery.Where(m => m.UploadDate.Date == selectedDate.Value.Date);
            }
            // --- END OF NEW LOGIC ---

            var filteredMedia = await mediaQuery
                .OrderByDescending(m => m.UploadDate)
                .Select(m => new GalleryItemViewModel
                {
                    Id = m.Id,
                    Title = m.Title,
                    ThumbnailPath = m.ThumbnailPath
                }).ToListAsync();

            var viewModel = new GalleryViewModel
            {
                MediaItems = filteredMedia,
                Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", SelectedCategoryId),
                SelectedCategoryId = SelectedCategoryId,
                SearchKeyword = searchKeyword,
                SelectedType = selectedType, // Pass the values back to the view
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

            // We'll create a simple view for this
            return View("Share", mediaItem);
        }

    }
}
