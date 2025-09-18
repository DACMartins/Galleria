using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Galleria.ViewModels
{
    public class UploadMediaViewModel
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        public string? Description { get; set; }

        [Required]
        public IFormFile File { get; set; } // Represents the uploaded file

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        // For populating the dropdown list in the view
        public IEnumerable<SelectListItem>? Categories { get; set; }

    }
}
