namespace Galleria.Models
{
    public class Keyword
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public ICollection<MediaItem> MediaItems { get; set; }
    }
}
