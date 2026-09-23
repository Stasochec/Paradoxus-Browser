using System;
using System.IO;
using Microsoft.Web.WebView2.Core;
using ParadoxusBrowser.ViewModels.Common;

namespace ParadoxusBrowser.Data.Models
{
    public enum DownloadState
    {
        Downloading,
        Completed,
        Canceled,
        Failed
    }

    public class DownloadItem : ViewModelBase
    {
        private long _receivedBytes;
        private long _totalBytes;
        private int _progressPercent;
        private DownloadState _state = DownloadState.Downloading;
        private string _statusText = "Загрузка...";

        public Guid Id { get; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public CoreWebView2DownloadOperation? Operation { get; set; }

        public long ReceivedBytes
        {
            get => _receivedBytes;
            set
            {
                if (SetProperty(ref _receivedBytes, value))
                {
                    OnPropertyChanged(nameof(FormattedProgress));
                }
            }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                if (SetProperty(ref _totalBytes, value))
                {
                    OnPropertyChanged(nameof(FormattedProgress));
                }
            }
        }

        public int ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        public DownloadState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsDownloading));
                    OnPropertyChanged(nameof(IsCompleted));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsDownloading => State == DownloadState.Downloading;
        public bool IsCompleted => State == DownloadState.Completed;

        public string FormattedProgress
        {
            get
            {
                if (TotalBytes <= 0)
                {
                    return $"{FormatSize(ReceivedBytes)}";
                }
                return $"{FormatSize(ReceivedBytes)} / {FormatSize(TotalBytes)} ({ProgressPercent}%)";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} Б";
            if (bytes < 1024 * 1024)
                return $"{(bytes / 1024.0):F1} КБ";
            if (bytes < 1024 * 1024 * 1024)
                return $"{(bytes / (1024.0 * 1024.0)):F1} МБ";
            return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} ГБ";
        }
    }
}
