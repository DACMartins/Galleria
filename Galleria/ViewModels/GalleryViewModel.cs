using Microsoft.AspNetCore.Mvc.Rendering;

namespace Galleria.ViewModels
{
    public class GalleryViewModel
    {




        // The list of media items to display on the page
        public List<GalleryItemViewModel> MediaItems { get; set; }

        public PagingInfo PagingInfo { get; set; }

        // For populating the Category filter dropdown
        public SelectList Categories { get; set; }

        // The user's selected category
        public int? SelectedCategoryId { get; set; }

        // The user's search string
        public string? SearchKeyword { get; set; }

        public string? SelectedType { get; set; } // "Photo" or "Video"
        public DateTime? SelectedDate { get; set; }
    }
}