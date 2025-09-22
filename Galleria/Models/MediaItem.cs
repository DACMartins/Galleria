using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace Galleria.Models
{
    public class MediaItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string FilePath { get; set; }
        public string ThumbnailPath { get; set; }
        public MediaType Type { get; set; }
        public DateTime UploadDate { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public ICollection<Keyword> Keywords { get; set; }

        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }

        public string? ShareToken { get; set; }
    }

    public enum MediaType { Photo, Video }
}