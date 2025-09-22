using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Galleria.ViewModels
{
    public class EditMediaViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }
        public IEnumerable<SelectListItem>? Categories { get; set; }

        [Display(Name = "Tags (comma-separated)")]
        public string? Tags { get; set; }
    }
}