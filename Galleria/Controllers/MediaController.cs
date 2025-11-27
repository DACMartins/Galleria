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
                model.Categories = await _context.Categories
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Name
                    })
                    .ToListAsync();

                return View(model);
            }

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

            // --- THUMBNAIL GENERATION ---
            string thumbnailFileName = "thumb_" + Path.GetFileNameWithoutExtension(uniqueFileName) + ".jpg";
            string thumbnailPath = Path.Combine(thumbnailsFolder, thumbnailFileName);
            string webThumbnailPath = "/uploads/thumbnails/" + thumbnailFileName;

            if (model.OptionalThumbnailFile != null && model.OptionalThumbnailFile.Length > 0)
            {
                using (var fileStream = new FileStream(thumbnailPath, FileMode.Create))
                {
                    await model.OptionalThumbnailFile.CopyToAsync(fileStream);
                }
            }
            else
            {
                bool isVideo = model.File.ContentType.StartsWith("video");
                if (isVideo)
                {
                    FFmpeg.SetExecutablesPath("C:\\ffmpeg\\bin");
                    var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(
                        filePath, thumbnailPath, TimeSpan.FromSeconds(1));
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

            var user = await _userManager.GetUserAsync(User);

            var mediaItem = new MediaItem
            {
                Title = model.Title,
                Description = string.IsNullOrWhiteSpace(model.Description)
                    ? string.Empty
                    : model.Description,
                FilePath = "/uploads/" + uniqueFileName,
                ThumbnailPath = webThumbnailPath,
                CategoryId = model.CategoryId,
                Type = model.File.ContentType.StartsWith("video")
                    ? MediaType.Video
                    : MediaType.Photo,
                ApplicationUserId = user.Id,
                UploadDate = DateTime.UtcNow,
                Keywords = new List<Keyword>() // IMPORTANT: initialize this
            };

            // -------- TAG PROCESSING --------
            var tagNames = (model.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tagNames.Any())
            {
                // Existing keywords
                var existingKeywords = await _context.Keywords
                    .Where(k => tagNames.Contains(k.Text))
                    .ToListAsync();

                foreach (var keyword in existingKeywords)
                {
                    mediaItem.Keywords.Add(keyword);
                }

                // New tags
                var existingNames = existingKeywords
                    .Select(k => k.Text)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newTagNames = tagNames
                    .Where(name => !existingNames.Contains(name))
                    .ToList();

                foreach (var name in newTagNames)
                {
                    var keyword = new Keyword { Text = name };
                    mediaItem.Keywords.Add(keyword); // EF will insert these
                }
            }
            // -------------------------------

            _context.MediaItems.Add(mediaItem);
            await _context.SaveChangesAsync();

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

            IQueryable<MediaItem> mediaQuery = _context.MediaItems
                .Include(m => m.Keywords)
                .Where(m => !m.IsDeleted);


            // 1) Filtro por categoria
            if (SelectedCategoryId.HasValue)
            {
                mediaQuery = mediaQuery.Where(m => m.CategoryId == SelectedCategoryId.Value);
            }

            // 2) Filtro por palavra-chave (Title / Description / Tag)
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var keyword = searchKeyword.Trim().ToLower();

                mediaQuery = mediaQuery.Where(m =>
                    m.Title.ToLower().Contains(keyword) ||
                    (m.Description != null && m.Description.ToLower().Contains(keyword)) ||
                    m.Keywords.Any(k => k.Text != null && k.Text.ToLower().Contains(keyword))
                );
            }

            // 3) Filtro por tipo (Photo / Video)
            if (!string.IsNullOrWhiteSpace(selectedType))
            {
                if (Enum.TryParse<MediaType>(selectedType, out var mediaType))
                {
                    mediaQuery = mediaQuery.Where(m => m.Type == mediaType);
                }
            }

            // 4) Filtro por data (só pela parte da data, sem horas)
            if (selectedDate.HasValue)
            {
                var dateOnly = selectedDate.Value.Date;

                mediaQuery = mediaQuery.Where(m => m.UploadDate.Date == dateOnly);
            }

            // --- PAGINAÇÃO ---

            var totalItems = await mediaQuery.CountAsync();

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

            var viewModel = new GalleryViewModel
            {
                MediaItems = pagedMediaItems,
                PagingInfo = new PagingInfo
                {
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalItems = totalItems
                },
                Categories = new SelectList(
                    await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(),
                    "Id",
                    "Name",
                    SelectedCategoryId),
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
