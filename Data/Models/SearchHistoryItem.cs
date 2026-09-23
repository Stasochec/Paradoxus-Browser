using ParadoxusBrowser.ViewModels.Common;

namespace ParadoxusBrowser.Data.Models
{
    public class SearchHistoryItem : ViewModelBase
    {
        private string _query = string.Empty;
        public string Query
        {
            get => _query;
            set => SetProperty(ref _query, value);
        }

        private string _matchedPrefix = string.Empty;
        public string MatchedPrefix
        {
            get => _matchedPrefix;
            set => SetProperty(ref _matchedPrefix, value);
        }

        private string _remainingText = string.Empty;
        public string RemainingText
        {
            get => _remainingText;
            set => SetProperty(ref _remainingText, value);
        }

        public bool HasHighlight => !string.IsNullOrEmpty(_matchedPrefix);
    }
}
