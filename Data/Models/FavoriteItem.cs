using System;

namespace ParadoxusBrowser.Data.Models
{
    public class FavoriteItem
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#FF7600";
        public string IconLetter { get; set; } = "в…";
        public int SortOrder { get; set; }

        public string Domain
        {
            get
            {
                if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                {
                    return uri.Host.Replace("www.", "");
                }
                return Url;
            }
        }
    }
}
