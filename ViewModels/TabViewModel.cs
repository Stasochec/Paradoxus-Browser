using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ParadoxusBrowser.Core;
using ParadoxusBrowser.Data;
using ParadoxusBrowser.Data.Models;
using ParadoxusBrowser.ViewModels.Common;

namespace ParadoxusBrowser.ViewModels
{
    public class TabViewModel : ViewModelBase, IDisposable
    {
        private string _title = "New Tab";
        private string _url = "about:blank";
        private string _inputUrl = string.Empty;
        private bool _isLoading;
        private int _loadingProgress;
        private bool _isPrivate;
        private int _adBlockCount;
        private bool _shieldsEnabled = true;
        private bool _isSecure;
        private bool _canGoBack;
        private bool _canGoForward;
        private string? _ephemeralDataFolder;
        private bool _isDisposed;

        private bool _isPlayingAudio;
        private bool _isMuted;
        private bool _isPinned;

        private readonly TaskCompletionSource<bool> _initTcs = new();
        public Task InitializationTask => _initTcs.Task;

        public Guid Id { get; } = Guid.NewGuid();

        public WebView2 WebView { get; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Url
        {
            get => _url;
            set
            {
                if (SetProperty(ref _url, value))
                {
                    InputUrl = value;
                    IsSecure = NavigationHelper.IsHttps(value);
                    OnPropertyChanged(nameof(CurrentDomain));
                }
            }
        }

        public string InputUrl
        {
            get => _inputUrl;
            set => SetProperty(ref _inputUrl, value);
        }

        public string CurrentDomain => NavigationHelper.ExtractDomain(_url);

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int LoadingProgress
        {
            get => _loadingProgress;
            set => SetProperty(ref _loadingProgress, value);
        }

        public bool IsPrivate
        {
            get => _isPrivate;
            set => SetProperty(ref _isPrivate, value);
        }

        public int AdBlockCount
        {
            get => _adBlockCount;
            set => SetProperty(ref _adBlockCount, value);
        }

        public bool ShieldsEnabled
        {
            get => _shieldsEnabled;
            set => SetProperty(ref _shieldsEnabled, value);
        }

        public bool IsSecure
        {
            get => _isSecure;
            set => SetProperty(ref _isSecure, value);
        }

        public bool CanGoBack
        {
            get => _canGoBack;
            set => SetProperty(ref _canGoBack, value);
        }

        public bool CanGoForward
        {
            get => _canGoForward;
            set => SetProperty(ref _canGoForward, value);
        }

        public bool IsPlayingAudio
        {
            get => _isPlayingAudio;
            set
            {
                if (SetProperty(ref _isPlayingAudio, value))
                {
                    OnPropertyChanged(nameof(ShowAudioIndicator));
                }
            }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (SetProperty(ref _isMuted, value))
                {
                    OnPropertyChanged(nameof(ShowAudioIndicator));
                }
            }
        }

        public bool ShowAudioIndicator => _isPlayingAudio || _isMuted;

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        private bool _isHubVisible = true;
        private bool _isSettingsVisible = false;

        public bool IsHubVisible
        {
            get => _isHubVisible;
            set
            {
                if (SetProperty(ref _isHubVisible, value))
                {
                    OnPropertyChanged(nameof(IsHubTab));
                    OnPropertyChanged(nameof(IsWebVisible));
                }
            }
        }

        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set
            {
                if (SetProperty(ref _isSettingsVisible, value))
                {
                    OnPropertyChanged(nameof(IsSettingsTab));
                    OnPropertyChanged(nameof(IsWebVisible));
                }
            }
        }

        public bool IsHubTab => _isHubVisible;
        public bool IsSettingsTab => _isSettingsVisible;

        public bool IsWebVisible => !_isHubVisible && !_isSettingsVisible;

        private string? _faviconUrl;
        public string? FaviconUrl
        {
            get => _faviconUrl;
            set
            {
                if (SetProperty(ref _faviconUrl, value))
                {
                    OnPropertyChanged(nameof(HasFavicon));
                }
            }
        }

        public bool HasFavicon => !string.IsNullOrWhiteSpace(_faviconUrl);

        private bool _showWaybackNotice;
        public bool ShowWaybackNotice
        {
            get => _showWaybackNotice;
            set => SetProperty(ref _showWaybackNotice, value);
        }

        private string _waybackNoticeUrl = string.Empty;
        public string WaybackNoticeUrl
        {
            get => _waybackNoticeUrl;
            set => SetProperty(ref _waybackNoticeUrl, value);
        }

        public ICommand OpenWaybackUrlCommand { get; }
        public ICommand DismissWaybackNoticeCommand { get; }

        private bool _isSpeedreaderActive;
        public bool IsSpeedreaderActive
        {
            get => _isSpeedreaderActive;
            set => SetProperty(ref _isSpeedreaderActive, value);
        }

        public ICommand ToggleSpeedreaderCommand { get; }

        // Zoom Badge fields & properties
        private DispatcherTimer? _zoomBadgeTimer;
        private DispatcherTimer? _zoomFadeTimer;
        private double _previousZoomFactor = 1.0;
        private bool _isZoomBadgeVisible;
        private double _zoomBadgeOpacity = 1.0;
        private string _zoomBadgeText = "100%";

        public bool IsZoomBadgeVisible
        {
            get => _isZoomBadgeVisible;
            set => SetProperty(ref _isZoomBadgeVisible, value);
        }

        public double ZoomBadgeOpacity
        {
            get => _zoomBadgeOpacity;
            set => SetProperty(ref _zoomBadgeOpacity, value);
        }

        public string ZoomBadgeText
        {
            get => _zoomBadgeText;
            set => SetProperty(ref _zoomBadgeText, value);
        }

        public ICommand ResetZoomCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }


        public event Action<string, bool>? OpenNewTabRequested;
        public event Action<DownloadItem, CoreWebView2DownloadOperation>? DownloadStarted;

        public TabViewModel(bool isPrivate = false, string initialUrl = NavigationHelper.HubUrl)
        {
            IsPrivate = isPrivate;
            _url = initialUrl;
            _isHubVisible = NavigationHelper.IsHubUrl(initialUrl);
            _isSettingsVisible = NavigationHelper.IsSettingsUrl(initialUrl);
            _inputUrl = _isHubVisible ? string.Empty : initialUrl;
            _title = isPrivate ? "Приватная вкладка" : (_isHubVisible ? "Табло" : (_isSettingsVisible ? "Настройки" : "Новая вкладка"));

            OpenWaybackUrlCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(WaybackNoticeUrl))
                {
                    ShowWaybackNotice = false;
                    Navigate(WaybackNoticeUrl);
                }
            });

            DismissWaybackNoticeCommand = new RelayCommand(() =>
            {
                ShowWaybackNotice = false;
            });

            ToggleSpeedreaderCommand = new RelayCommand(async () => await ToggleSpeedreaderAsync());
            ResetZoomCommand = new RelayCommand(ResetZoom);
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);

            ThemeManager.ThemeChanged += OnThemeChanged;

            WebView = new WebView2();
            _ = InitializeWebViewAsync(initialUrl);
        }

        private async Task InitializeWebViewAsync(string targetUrl)
        {
            try
            {
                string userDataFolder;
                if (IsPrivate)
                {
                    _ephemeralDataFolder = SecurityManager.CreateEphemeralUserDataFolder();
                    userDataFolder = _ephemeralDataFolder;
                }
                else
                {
                    userDataFolder = SecurityManager.GetDefaultUserDataFolder();
                }

                // Check Hardware Acceleration setting
                string? hwAccStr = await DatabaseContext.Instance.GetSecureSettingAsync("HardwareAcceleration");
                bool useHwAcc = true;
                if (bool.TryParse(hwAccStr, out bool parsedHwAcc))
                {
                    useHwAcc = parsedHwAcc;
                }

                var options = new CoreWebView2EnvironmentOptions();
                if (!useHwAcc)
                {
                    options.AdditionalBrowserArguments = "--disable-gpu";
                }

                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder,
                    options: options
                );

                await WebView.EnsureCoreWebView2Async(environment);

                // Configure security & privacy settings
                WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                WebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                WebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

                // Subscribe to CoreWebView2 events
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
                WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                WebView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                WebView.CoreWebView2.FaviconChanged += CoreWebView2_FaviconChanged;

                // Audio events
                WebView.CoreWebView2.IsDocumentPlayingAudioChanged += CoreWebView2_IsDocumentPlayingAudioChanged;
                WebView.CoreWebView2.IsMutedChanged += CoreWebView2_IsMutedChanged;

                // Context Menu
                WebView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

                // Downloads
                WebView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

                // Configure Network Request Interception for AdBlocker / Shields
                WebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                WebView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;

                // Subscribe to ZoomFactor changes
                WebView.ZoomFactorChanged += WebView_ZoomFactorChanged;

                // 1. Sync PreferredColorScheme (prefers-color-scheme) & DefaultBackgroundColor
                ApplyThemeColorScheme(ThemeManager.CurrentTheme);

                // 2. Block JavaScript according to settings
                WebView.CoreWebView2.Settings.IsScriptEnabled = !SettingsManager.Instance.BlockJavaScript;

                // 3. Apply default zoom and font size
                try
                {
                    WebView.ZoomFactor = SettingsManager.Instance.DefaultZoomFactor;
                }
                catch { }
                ApplyFontSize(SettingsManager.Instance.SelectedFontSize);

                // 4. Inject Anti-Fingerprinting protection script
                if (SettingsManager.Instance.FingerprintingProtection)
                {
                    await InjectFingerprintingProtectionAsync();
                }

                _initTcs.TrySetResult(true);

                // Safely load initial destination without racing user actions
                if (_url == targetUrl && NavigationHelper.IsHubUrl(targetUrl))
                {
                    WebView.CoreWebView2.Navigate("about:blank");
                }
                else if (_url == targetUrl && NavigationHelper.IsSettingsUrl(targetUrl))
                {
                    WebView.CoreWebView2.Navigate("about:blank");
                }
                else if (!NavigationHelper.IsHubUrl(_url) && !NavigationHelper.IsSettingsUrl(_url))
                {
                    WebView.CoreWebView2.Navigate(_url);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize WebView2: {ex.Message}");
                _initTcs.TrySetException(ex);
            }
        }

        private void CoreWebView2_IsDocumentPlayingAudioChanged(object? sender, object e)
        {
            if (WebView.CoreWebView2 != null)
            {
                IsPlayingAudio = WebView.CoreWebView2.IsDocumentPlayingAudio;
            }
        }

        private void CoreWebView2_IsMutedChanged(object? sender, object e)
        {
            if (WebView.CoreWebView2 != null)
            {
                IsMuted = WebView.CoreWebView2.IsMuted;
            }
        }

        public void ToggleMute()
        {
            if (WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.IsMuted = !WebView.CoreWebView2.IsMuted;
                IsMuted = WebView.CoreWebView2.IsMuted;
            }
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            if (SettingsManager.Instance.AskWhereToSave)
            {
                e.Handled = false; // Show standard system save dialog
            }
            else
            {
                try
                {
                    string targetDir = SettingsManager.Instance.DownloadFolderPath;
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    string baseName = Path.GetFileName(e.ResultFilePath);
                    if (string.IsNullOrWhiteSpace(baseName))
                        baseName = "download";
                    e.ResultFilePath = Path.Combine(targetDir, baseName);
                    e.Handled = true; // Auto-save silently to target directory
                }
                catch
                {
                    e.Handled = false;
                }
            }

            string fileName = Path.GetFileName(e.ResultFilePath);
            var item = new DownloadItem
            {
                FileName = string.IsNullOrWhiteSpace(fileName) ? "download" : fileName,
                FilePath = e.ResultFilePath,
                TotalBytes = (long)(e.DownloadOperation.TotalBytesToReceive ?? 0),
                Operation = e.DownloadOperation,
                State = DownloadState.Downloading
            };

            e.DownloadOperation.BytesReceivedChanged += (s, ev) =>
            {
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    item.ReceivedBytes = (long)e.DownloadOperation.BytesReceived;
                    if (item.TotalBytes > 0)
                    {
                        item.ProgressPercent = (int)((item.ReceivedBytes * 100) / item.TotalBytes);
                    }
                });
            };

            e.DownloadOperation.StateChanged += (s, ev) =>
            {
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    switch (e.DownloadOperation.State)
                    {
                        case CoreWebView2DownloadState.Completed:
                            item.State = DownloadState.Completed;
                            item.StatusText = "Завершено";
                            item.ProgressPercent = 100;
                            break;
                        case CoreWebView2DownloadState.Interrupted:
                            if (e.DownloadOperation.InterruptReason == CoreWebView2DownloadInterruptReason.UserCanceled)
                            {
                                item.State = DownloadState.Canceled;
                                item.StatusText = "Отменено";
                            }
                            else
                            {
                                item.State = DownloadState.Failed;
                                item.StatusText = "Ошибка загрузки";
                            }
                            break;
                    }
                });
            };

            DownloadStarted?.Invoke(item, e.DownloadOperation);
        }

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            if (WebView.CoreWebView2 == null)
                return;

            try
            {
                var env = WebView.CoreWebView2.Environment;
                var menuList = e.MenuItems;
                menuList.Clear();

                // 1. Text Selection context
                if (!string.IsNullOrWhiteSpace(e.ContextMenuTarget.SelectionText))
                {
                    string selText = e.ContextMenuTarget.SelectionText;
                    string truncated = selText.Length > 24 ? selText.Substring(0, 24) + "..." : selText;

                    var copyItem = env.CreateContextMenuItem("Копировать", null, CoreWebView2ContextMenuItemKind.Command);
                    copyItem.CustomItemSelected += (s, ev) =>
                    {
                        try { Clipboard.SetText(selText); } catch { }
                    };
                    menuList.Add(copyItem);

                    var searchItem = env.CreateContextMenuItem($"Поиск «{truncated}» в Paradoxus", null, CoreWebView2ContextMenuItemKind.Command);
                    searchItem.CustomItemSelected += (s, ev) =>
                    {
                        OpenNewTabRequested?.Invoke(NavigationHelper.GetSearchUrl(selText), IsPrivate);
                    };
                    menuList.Add(searchItem);

                    menuList.Add(env.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
                }

                // 2. Link context
                if (e.ContextMenuTarget.HasLinkUri && !string.IsNullOrWhiteSpace(e.ContextMenuTarget.LinkUri))
                {
                    string linkUri = e.ContextMenuTarget.LinkUri;

                    var openLinkTabItem = env.CreateContextMenuItem("Открыть ссылку в новой вкладке", null, CoreWebView2ContextMenuItemKind.Command);
                    openLinkTabItem.CustomItemSelected += (s, ev) =>
                    {
                        OpenNewTabRequested?.Invoke(linkUri, IsPrivate);
                    };
                    menuList.Add(openLinkTabItem);

                    var copyLinkItem = env.CreateContextMenuItem("Копировать адрес ссылки", null, CoreWebView2ContextMenuItemKind.Command);
                    copyLinkItem.CustomItemSelected += (s, ev) =>
                    {
                        try { Clipboard.SetText(linkUri); } catch { }
                    };
                    menuList.Add(copyLinkItem);

                    menuList.Add(env.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
                }

                // 3. Media / Image context
                if (e.ContextMenuTarget.HasSourceUri && e.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.Image)
                {
                    string srcUri = e.ContextMenuTarget.SourceUri;

                    var openImgTabItem = env.CreateContextMenuItem("Открыть изображение в новой вкладке", null, CoreWebView2ContextMenuItemKind.Command);
                    openImgTabItem.CustomItemSelected += (s, ev) =>
                    {
                        OpenNewTabRequested?.Invoke(srcUri, IsPrivate);
                    };
                    menuList.Add(openImgTabItem);

                    var copyImgLinkItem = env.CreateContextMenuItem("Копировать адрес изображения", null, CoreWebView2ContextMenuItemKind.Command);
                    copyImgLinkItem.CustomItemSelected += (s, ev) =>
                    {
                        try { Clipboard.SetText(srcUri); } catch { }
                    };
                    menuList.Add(copyImgLinkItem);

                    menuList.Add(env.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
                }

                // 4. Navigation & Page context
                var backItem = env.CreateContextMenuItem("Назад", null, CoreWebView2ContextMenuItemKind.Command);
                backItem.IsEnabled = WebView.CanGoBack;
                backItem.CustomItemSelected += (s, ev) => GoBack();
                menuList.Add(backItem);

                var fwdItem = env.CreateContextMenuItem("Вперед", null, CoreWebView2ContextMenuItemKind.Command);
                fwdItem.IsEnabled = WebView.CanGoForward;
                fwdItem.CustomItemSelected += (s, ev) => GoForward();
                menuList.Add(fwdItem);

                var reloadItem = env.CreateContextMenuItem("Перезагрузить", null, CoreWebView2ContextMenuItemKind.Command);
                reloadItem.CustomItemSelected += (s, ev) => Reload();
                menuList.Add(reloadItem);

                var saveItem = env.CreateContextMenuItem("Сохранить страницу как...", null, CoreWebView2ContextMenuItemKind.Command);
                saveItem.CustomItemSelected += async (s, ev) =>
                {
                    try
                    {
                        await WebView.CoreWebView2.ShowSaveAsUIAsync();
                    }
                    catch
                    {
                        _ = WebView.CoreWebView2.ExecuteScriptAsync("window.print()");
                    }
                };
                menuList.Add(saveItem);

                menuList.Add(env.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));

                // 5. Developer Tools / Inspect Element
                var inspectItem = env.CreateContextMenuItem("Просмотреть код элемента", null, CoreWebView2ContextMenuItemKind.Command);
                inspectItem.CustomItemSelected += (s, ev) =>
                {
                    WebView.CoreWebView2.OpenDevToolsWindow();
                };
                menuList.Add(inspectItem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContextMenuRequested error: {ex.Message}");
            }
        }

        private void CoreWebView2_FaviconChanged(object? sender, object e)
        {
            if (WebView.CoreWebView2 != null)
            {
                FaviconUrl = WebView.CoreWebView2.FaviconUri;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            IsLoading = true;
            LoadingProgress = 25;
            AdBlockCount = 0; // Reset counter for new page load
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            IsLoading = false;
            LoadingProgress = 100;

            // Speedreader auto-trigger if enabled in settings
            if (SettingsManager.Instance.SpeedreaderEnabled && e.IsSuccess && !IsHubVisible && !IsSettingsVisible)
            {
                _ = CheckAndAutoTriggerSpeedreaderAsync();
            }

            // Check if page failed or returned 404 and offer Wayback Machine archive
            if (SettingsManager.Instance.WaybackMachineEnabled &&
                !NavigationHelper.IsHubUrl(Url) &&
                !NavigationHelper.IsSettingsUrl(Url) &&
                !Url.StartsWith("about:") &&
                Uri.TryCreate(Url, UriKind.Absolute, out Uri? pageUri) &&
                (pageUri.Scheme == Uri.UriSchemeHttp || pageUri.Scheme == Uri.UriSchemeHttps))
            {
                if ((!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) || e.HttpStatusCode == 404)
                {
                    WaybackNoticeUrl = $"https://web.archive.org/web/*/{Url}";
                    ShowWaybackNotice = true;
                }
                else
                {
                    ShowWaybackNotice = false;
                }
            }
            else
            {
                ShowWaybackNotice = false;
            }
        }

        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (WebView.CoreWebView2 != null)
            {
                string source = WebView.CoreWebView2.Source;
                if (!string.IsNullOrEmpty(source) && source != "about:blank")
                {
                    Url = source;
                    if (NavigationHelper.IsHubUrl(source))
                    {
                        IsHubVisible = true;
                        IsSettingsVisible = false;
                        FaviconUrl = null;
                    }
                    else if (NavigationHelper.IsSettingsUrl(source))
                    {
                        IsSettingsVisible = true;
                        IsHubVisible = false;
                        FaviconUrl = null;
                    }
                    else
                    {
                        IsHubVisible = false;
                        IsSettingsVisible = false;
                    }
                }
            }
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            if (WebView.CoreWebView2 != null)
            {
                string title = WebView.CoreWebView2.DocumentTitle;
                if (!IsHubVisible && !IsSettingsVisible)
                {
                    Title = string.IsNullOrWhiteSpace(title) ? (IsPrivate ? "Приватная вкладка" : "Новая вкладка") : title;

                    // If not in private mode, persist to SQLite History
                    if (!IsPrivate && !string.IsNullOrWhiteSpace(Url) && !Url.StartsWith("about:") && !NavigationHelper.IsHubUrl(Url))
                    {
                        _ = DatabaseContext.Instance.AddHistoryAsync(Url, Title, FaviconUrl ?? string.Empty);
                    }
                }
            }
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            CanGoBack = WebView.CanGoBack;
            CanGoForward = WebView.CanGoForward;
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true; // Prevent unhandled popup windows

            // Only allow safe HTTP/HTTPS navigations to open as tabs
            if (SecurityManager.IsSafeNavigationScheme(e.Uri))
            {
                OpenNewTabRequested?.Invoke(e.Uri, IsPrivate);
            }
        }

        private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!ShieldsEnabled || WebView.CoreWebView2 == null)
                return;

            if (Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out Uri? reqUri))
            {
                if (AdBlocker.Instance.ShouldBlock(reqUri, CurrentDomain, out _))
                {
                    // Respond with empty body and 200 OK or 403 Forbidden to neutralize tracker/ad
                    e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                        new MemoryStream(),
                        200,
                        "Blocked by Paradoxus Shields",
                        "Content-Type: text/plain"
                    );

                    Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        AdBlockCount++;
                    });
                }
            }
        }

        public async void Navigate(string destination)
        {
            string cleanUrl = NavigationHelper.ProcessInput(destination);
            if (!SecurityManager.IsSafeNavigationScheme(cleanUrl))
                return;

            if (NavigationHelper.IsHubUrl(cleanUrl))
            {
                IsHubVisible = true;
                IsSettingsVisible = false;
                Url = NavigationHelper.HubUrl;
                InputUrl = string.Empty;
                Title = IsPrivate ? "Приватная вкладка" : "Табло";
                IsLoading = false;

                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Navigate("about:blank");
                }
                return;
            }

            if (NavigationHelper.IsSettingsUrl(cleanUrl))
            {
                IsSettingsVisible = true;
                IsHubVisible = false;
                Url = NavigationHelper.SettingsUrl;
                InputUrl = NavigationHelper.SettingsUrl;
                Title = "Настройки";
                IsLoading = false;
                FaviconUrl = null;

                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Navigate("about:blank");
                }
                return;
            }

            // Regular web navigation: instantly hide Hub/Settings
            IsHubVisible = false;
            IsSettingsVisible = false;
            Url = cleanUrl;
            InputUrl = cleanUrl;
            IsLoading = true;
            LoadingProgress = 25;

            try
            {
                if (WebView.CoreWebView2 == null)
                {
                    await _initTcs.Task;
                }

                if (WebView.CoreWebView2 != null && Url == cleanUrl)
                {
                    WebView.CoreWebView2.Navigate(cleanUrl);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigate error: {ex.Message}");
            }
        }

        public void GoBack()
        {
            if (WebView.CanGoBack)
                WebView.GoBack();
        }

        public void GoForward()
        {
            if (WebView.CanGoForward)
                WebView.GoForward();
        }

        public void Reload()
        {
            WebView.Reload();
        }

        public void Stop()
        {
            WebView.Stop();
            IsLoading = false;
        }

        public double ZoomFactor
        {
            get => WebView.ZoomFactor;
            set
            {
                try
                {
                    WebView.ZoomFactor = value;
                }
                catch { }
            }
        }

        public bool IsScriptEnabled
        {
            get => WebView.CoreWebView2?.Settings.IsScriptEnabled ?? true;
            set
            {
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Settings.IsScriptEnabled = value;
                }
            }
        }

        private string? _fontSizeScriptId;

        public async void ApplyFontSize(string sizeCategory)
        {
            if (WebView.CoreWebView2 == null) return;

            string percent = sizeCategory switch
            {
                "Мелкий" => "85",
                "Средний (рекомендуется)" => "100",
                "Средний" => "100",
                "Крупный" => "120",
                "Очень крупный" => "135",
                _ => "100"
            };

            string script = $@"(function() {{
                function applyFont() {{
                    let style = document.getElementById('paradoxus-font-style');
                    if (!style) {{
                        style = document.createElement('style');
                        style.id = 'paradoxus-font-style';
                        if (document.head) {{
                            document.head.appendChild(style);
                        }} else if (document.documentElement) {{
                            document.documentElement.appendChild(style);
                        }}
                    }}
                    if (style) {{
                        style.innerHTML = 'html, body, p, span, li, a, article {{ font-size: {percent}% !important; }}';
                    }}
                }}
                if (document.readyState === 'loading') {{
                    document.addEventListener('DOMContentLoaded', applyFont);
                }} else {{
                    applyFont();
                }}
            }})();";

            try
            {
                if (!string.IsNullOrEmpty(_fontSizeScriptId))
                {
                    try { WebView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(_fontSizeScriptId); } catch { }
                }
                _fontSizeScriptId = await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
                await WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying font size: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            ThemeManager.ThemeChanged -= OnThemeChanged;
            _zoomBadgeTimer?.Stop();
            _zoomFadeTimer?.Stop();

            try
            {
                if (WebView.CoreWebView2 != null)
                {
                    WebView.ZoomFactorChanged -= WebView_ZoomFactorChanged;
                    WebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    WebView.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
                    WebView.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
                    WebView.CoreWebView2.HistoryChanged -= CoreWebView2_HistoryChanged;
                    WebView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
                    WebView.CoreWebView2.FaviconChanged -= CoreWebView2_FaviconChanged;
                    WebView.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;

                    WebView.CoreWebView2.IsDocumentPlayingAudioChanged -= CoreWebView2_IsDocumentPlayingAudioChanged;
                    WebView.CoreWebView2.IsMutedChanged -= CoreWebView2_IsMutedChanged;
                    WebView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                    WebView.CoreWebView2.DownloadStarting -= CoreWebView2_DownloadStarting;
                }

                WebView.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing WebView: {ex.Message}");
            }

            // Clean up ephemeral private browsing data folder
            if (IsPrivate && !string.IsNullOrEmpty(_ephemeralDataFolder))
            {
                SecurityManager.CleanupEphemeralFolder(_ephemeralDataFolder);
            }
        }

        private void OnThemeChanged(AppTheme newTheme)
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                ApplyThemeColorScheme(newTheme);
            });
        }

        public void ApplyThemeColorScheme(AppTheme theme)
        {
            if (WebView.CoreWebView2 == null) return;
            try
            {
                if (theme == AppTheme.ParadoxusDark || theme == AppTheme.OledMidnight)
                {
                    WebView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
                    WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(24, 25, 30);
                }
                else
                {
                    WebView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
                    WebView.DefaultBackgroundColor = System.Drawing.Color.White;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme color scheme: {ex.Message}");
            }
        }

        private async Task InjectFingerprintingProtectionAsync()
        {
            if (WebView.CoreWebView2 == null) return;
            try
            {
                string script = @"
                (() => {
                    try {
                        // 1. Noise in Canvas toDataURL
                        const origToDataURL = HTMLCanvasElement.prototype.toDataURL;
                        HTMLCanvasElement.prototype.toDataURL = function() {
                            try {
                                const ctx = this.getContext('2d');
                                if (ctx && this.width > 0 && this.height > 0) {
                                    const imgData = ctx.getImageData(0, 0, Math.min(this.width, 16), Math.min(this.height, 16));
                                    for (let i = 0; i < imgData.data.length; i += 4) {
                                        imgData.data[i] ^= (Math.random() < 0.05 ? 1 : 0);
                                    }
                                    ctx.putImageData(imgData, 0, 0);
                                }
                            } catch (e) {}
                            return origToDataURL.apply(this, arguments);
                        };

                        // 2. Hardware concurrency normalization
                        Object.defineProperty(navigator, 'hardwareConcurrency', {
                            get: () => 8,
                            configurable: true
                        });

                        // 3. AudioContext channel data noise
                        if (window.AudioBuffer) {
                            const origGetChannelData = AudioBuffer.prototype.getChannelData;
                            AudioBuffer.prototype.getChannelData = function() {
                                const data = origGetChannelData.apply(this, arguments);
                                for (let i = 0; i < Math.min(data.length, 100); i += 10) {
                                    data[i] += (Math.random() - 0.5) * 0.0000001;
                                }
                                return data;
                            };
                        }
                    } catch (e) {}
                })();
                ";
                await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error injecting anti-fingerprinting script: {ex.Message}");
            }
        }

        public async Task ToggleSpeedreaderAsync()
        {
            if (WebView.CoreWebView2 == null) return;
            IsSpeedreaderActive = !IsSpeedreaderActive;
            if (IsSpeedreaderActive)
            {
                string readerScript = @"
                (() => {
                    let article = document.querySelector('article') || document.querySelector('main') || document.body;
                    let contentHtml = article ? article.innerHTML : document.body.innerHTML;
                    let title = document.title || 'Режим чтения';
                    let readerContainer = document.getElementById('paradoxus-speedreader-view');
                    if (!readerContainer) {
                        readerContainer = document.createElement('div');
                        readerContainer.id = 'paradoxus-speedreader-view';
                        readerContainer.style.cssText = 'position:fixed;top:0;left:0;width:100vw;height:100vh;overflow-y:auto;background:#18191E;color:#E0E0E6;z-index:2147483647;padding:40px 20%;box-sizing:border-box;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;font-size:18px;line-height:1.75;letter-spacing:0.3px;';
                        readerContainer.innerHTML = '<h1 style=""color:#FF7600;font-size:32px;margin-bottom:24px;border-bottom:1px solid #333;padding-bottom:12px;"">' + title + '</h1><div style=""max-width:760px;margin:0 auto;"">' + contentHtml + '</div>';
                        document.body.appendChild(readerContainer);
                        readerContainer.querySelectorAll('nav, footer, aside, script, iframe, header, button').forEach(el => el.remove());
                        readerContainer.querySelectorAll('img').forEach(img => {
                            img.style.maxWidth = '100%';
                            img.style.height = 'auto';
                            img.style.borderRadius = '8px';
                            img.style.margin = '20px 0';
                        });
                    } else {
                        readerContainer.style.display = 'block';
                    }
                })();
                ";
                try { await WebView.CoreWebView2.ExecuteScriptAsync(readerScript); } catch { }
            }
            else
            {
                try
                {
                    WebView.Reload();
                }
                catch { }
            }
        }
    
        public void ResetZoom()
        {
            ZoomFactor = 1.0;
        }

        public void ZoomIn()
        {
            ZoomFactor = Math.Min(Math.Round(ZoomFactor + 0.1, 2), 5.0);
        }

        public void ZoomOut()
        {
            ZoomFactor = Math.Max(Math.Round(ZoomFactor - 0.1, 2), 0.25);
        }

        private void WebView_ZoomFactorChanged(object? sender, EventArgs e)
        {
            double zoom = WebView.ZoomFactor;
            int percent = (int)Math.Round(zoom * 100);
            ZoomBadgeText = $"{percent}%";

            _zoomBadgeTimer?.Stop();
            _zoomFadeTimer?.Stop();

            if (percent != 100)
            {
                // If zoom is not 100% — badge is always visible
                ZoomBadgeOpacity = 1.0;
                IsZoomBadgeVisible = true;
            }
            else
            {
                // If returned to 100% from another zoom level:
                if (Math.Abs(_previousZoomFactor - 1.0) > 0.01)
                {
                    IsZoomBadgeVisible = true;
                    ZoomBadgeOpacity = 1.0;

                    // Show for exactly 5 seconds, then smoothly fade out
                    _zoomBadgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    _zoomBadgeTimer.Tick += (s, args) =>
                    {
                        _zoomBadgeTimer.Stop();
                        AnimateFadeOutZoomBadge();
                    };
                    _zoomBadgeTimer.Start();
                }
                else
                {
                    // If page initially opened at 100% — badge is hidden
                    IsZoomBadgeVisible = false;
                }
            }

            _previousZoomFactor = zoom;
            OnPropertyChanged(nameof(ZoomFactor));
        }

        private void AnimateFadeOutZoomBadge()
        {
            _zoomFadeTimer?.Stop();
            _zoomFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            int steps = 10;
            int step = 0;
            _zoomFadeTimer.Tick += (s, ev) =>
            {
                step++;
                ZoomBadgeOpacity = Math.Max(0, 1.0 - (double)step / steps);
                if (step >= steps)
                {
                    _zoomFadeTimer.Stop();
                    IsZoomBadgeVisible = false;
                    ZoomBadgeOpacity = 1.0;
                }
            };
            _zoomFadeTimer.Start();
        }

        private async Task CheckAndAutoTriggerSpeedreaderAsync()
        {
            if (WebView.CoreWebView2 == null) return;
            string checkScript = @"
            (() => {
                let article = document.querySelector('article');
                let paragraphs = document.querySelectorAll('p');
                return article !== null || (paragraphs && paragraphs.length >= 4);
            })();";
            try
            {
                string res = await WebView.CoreWebView2.ExecuteScriptAsync(checkScript);
                if (res == "true" && !IsSpeedreaderActive)
                {
                    await ToggleSpeedreaderAsync();
                }
            }
            catch { }
        }

    }
}
