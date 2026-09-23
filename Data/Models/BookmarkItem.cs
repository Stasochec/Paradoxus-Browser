using System;

namespace ParadoxusBrowser.Data.Models
{
    public class BookmarkItem
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Url : Title;
    }
}
