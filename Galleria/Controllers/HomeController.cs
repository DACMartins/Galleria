using Galleria.Data;
using Galleria.Models;
using Galleria.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Galleria.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var latestMedia = await _context.MediaItems
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.UploadDate)
                .Take(12) // Get the 12 most recent items
                .Select(m => new GalleryItemViewModel
                {
                    Id = m.Id,
                    Title = m.Title,
                    ThumbnailPath = m.ThumbnailPath
                })
                .ToListAsync();

            return View(latestMedia);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
