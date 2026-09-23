using System;

namespace ParadoxusBrowser.Data.Models
{
    public class HistoryItem
    {
        public long Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
        public string FaviconUrl { get; set; } = string.Empty;

        public string FormattedTime => VisitedAt.ToLocalTime().ToString("g");
    }
}
