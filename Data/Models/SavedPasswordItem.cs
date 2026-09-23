using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParadoxusBrowser.Data.Models
{
    public class SavedPasswordItem : INotifyPropertyChanged
    {
        private bool _isPasswordVisible;
        private string _plainPassword = string.Empty;

        public long Id { get; set; }
        public string SiteUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string PlainPassword
        {
            get => _plainPassword;
            set
            {
                if (_plainPassword != value)
                {
                    _plainPassword = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayPassword));
                }
            }
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                if (_isPasswordVisible != value)
                {
                    _isPasswordVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayPassword));
                }
            }
        }

        public string DisplayPassword => IsPasswordVisible ? PlainPassword : new string('•', Math.Max(PlainPassword.Length, 8));

        public string Domain
        {
            get
            {
                try
                {
                    if (Uri.TryCreate(SiteUrl, UriKind.Absolute, out var uri))
                        return uri.Host;
                }
                catch { }
                return SiteUrl;
            }
        }

        public string FormattedDate => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}