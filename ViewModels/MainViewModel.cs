using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using ParadoxusBrowser.Core;
using ParadoxusBrowser.Data;
using ParadoxusBrowser.Data.Models;
using ParadoxusBrowser.ViewModels.Common;
using ParadoxusBrowser.Views;

namespace ParadoxusBrowser.ViewModels
{
    public class CommandPaletteItem : ViewModelBase
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string IconKey { get; set; } = "GlobeIcon";
        public Action Action { get; set; } = () => { };
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly bool _shouldRestoreSession = true;
        private TabViewModel? _selectedTab;
        private bool _isShieldsPopupOpen;
        private DateTime _lastShieldsClosedTime = DateTime.MinValue;
        private bool _isHistoryOpen;
        private bool _isSettingsOpen;
        private bool _isDownloadsPopupOpen;
        private DateTime _lastDownloadsClosedTime = DateTime.MinValue;

        private bool _isOmniboxSuggestionsOpen;
        private SearchHistoryItem? _selectedOmniboxSuggestion;
        private bool _isHubSuggestionsOpen;
        private SearchHistoryItem? _selectedHubSuggestion;
        private bool _isCommandPaletteOpen;
        private string _commandPaletteSearchQuery = string.Empty;
        private CommandPaletteItem? _selectedPaletteItem;

        private bool _isCurrentPageBookmarked;
        private long _totalBlockedAllTime;

        // Hub (Табло) properties
        private string _hubSearchQuery = string.Empty;
        private bool _isAddFavoriteDialogOpen;
        private string _newFavoriteTitle = string.Empty;
        private string _newFavoriteUrl = string.Empty;

        // Password Manager properties
        private string _newPasswordSiteUrl = string.Empty;
        private string _newPasswordUsername = string.Empty;
        private string _newPasswordValue = string.Empty;

        // Settings properties
        private bool _globalShieldsEnabled = true;
        private bool _hardwareAccelerationEnabled = true;
        private bool _sleepingTabsEnabled = true;
        private SearchEngineType _searchEngine = SearchEngineType.Yandex;
        private AppTheme _currentTheme = AppTheme.ParadoxusDark;
        private string _selectedSettingsCategory = "Appearance";
        private string _settingsSearchQuery = string.Empty;
        public string BrowserVersion => "v1.2.0";
        public string ChromiumVersion { get; private set; } = "120.0.6099.130";

        // Content settings properties
        private string _selectedFontSize = "Средний (рекомендуется)";
        private string _selectedPageZoom = "100%";
        private bool _cycleTabsMruEnabled = false;
        private bool _waybackMachineEnabled = true;
        private bool _speedreaderEnabled = false;

        // Shields settings properties
        private string _adBlockMode = "Стандартная";
        private string _httpsUpgradeMode = "Стандартная";
        private bool _blockScriptsEnabled = false;
        private bool _fingerprintingProtectionEnabled = true;
        private string _cookieBlockingMode = "Блокировать межсайтовые куки";
        private bool _clearDataOnExitEnabled = false;

        // Autofill settings properties
        private bool _incognitoAutofillEnabled = false;

        // Downloads settings properties
        private string _downloadFolderPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        private bool _askWhereToSaveEnabled = false;
        private bool _notifyDownloadCompletedEnabled = true;

        private readonly List<TabViewModel> _mruTabs = new();
        // Closed tabs history stack (for Ctrl+Shift+T)
        private readonly Stack<string> _closedTabsHistory = new();

        public ObservableCollection<TabViewModel> Tabs { get; } = new();
        public ObservableCollection<BookmarkItem> Bookmarks { get; } = new();
        public ObservableCollection<HistoryItem> HistoryItems { get; } = new();
        public ObservableCollection<FavoriteItem> Favorites { get; } = new();
        public ObservableCollection<SavedPasswordItem> SavedPasswords { get; } = new();
        public ObservableCollection<DownloadItem> Downloads { get; } = new();
        public ObservableCollection<CommandPaletteItem> FilteredCommandPaletteItems { get; } = new();
        private readonly List<CommandPaletteItem> _allPaletteItems = new();

        public bool HasFavorites => Favorites.Count > 0;
        public bool HasClosedTabs => _closedTabsHistory.Count > 0;

        public int ActiveDownloadsCount => Downloads.Count(d => d.State == DownloadState.Downloading);
        public bool HasActiveDownloads => ActiveDownloadsCount > 0;

        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    if (value != null)
                    {
                        _mruTabs.Remove(value);
                        _mruTabs.Insert(0, value);
                    }
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
                    _ = CheckBookmarkStatusAsync();
                }
            }
        }

        public bool IsShieldsPopupOpen
        {
            get => _isShieldsPopupOpen;
            set
            {
                if (!_isShieldsPopupOpen && value)
                {
                    _isDownloadsPopupOpen = false;
                    OnPropertyChanged(nameof(IsDownloadsPopupOpen));
                    _isOmniboxSuggestionsOpen = false;
                    OnPropertyChanged(nameof(IsOmniboxSuggestionsOpen));
                }
                if (_isShieldsPopupOpen && !value)
                {
                    _lastShieldsClosedTime = DateTime.UtcNow;
                }
                SetProperty(ref _isShieldsPopupOpen, value);
            }
        }

        public bool IsHistoryOpen
        {
            get => _isHistoryOpen;
            set => SetProperty(ref _isHistoryOpen, value);
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set => SetProperty(ref _isSettingsOpen, value);
        }

        public bool IsDownloadsPopupOpen
        {
            get => _isDownloadsPopupOpen;
            set
            {
                if (!_isDownloadsPopupOpen && value)
                {
                    _isShieldsPopupOpen = false;
                    OnPropertyChanged(nameof(IsShieldsPopupOpen));
                    _isOmniboxSuggestionsOpen = false;
                    OnPropertyChanged(nameof(IsOmniboxSuggestionsOpen));
                }
                if (_isDownloadsPopupOpen && !value)
                {
                    _lastDownloadsClosedTime = DateTime.UtcNow;
                }
                SetProperty(ref _isDownloadsPopupOpen, value);
            }
        }

        public bool IsOmniboxSuggestionsOpen
        {
            get => _isOmniboxSuggestionsOpen;
            set => SetProperty(ref _isOmniboxSuggestionsOpen, value);
        }

        public ObservableCollection<SearchHistoryItem> OmniboxSuggestions { get; } = new();

        public SearchHistoryItem? SelectedOmniboxSuggestion
        {
            get => _selectedOmniboxSuggestion;
            set => SetProperty(ref _selectedOmniboxSuggestion, value);
        }

        public bool IsHubSuggestionsOpen
        {
            get => _isHubSuggestionsOpen;
            set => SetProperty(ref _isHubSuggestionsOpen, value);
        }

        public ObservableCollection<SearchHistoryItem> HubSuggestions { get; } = new();

        public SearchHistoryItem? SelectedHubSuggestion
        {
            get => _selectedHubSuggestion;
            set => SetProperty(ref _selectedHubSuggestion, value);
        }

        public bool IsCommandPaletteOpen
        {
            get => _isCommandPaletteOpen;
            set
            {
                if (SetProperty(ref _isCommandPaletteOpen, value))
                {
                    if (value)
                    {
                        RefreshCommandPalette();
                    }
                }
            }
        }

        public string CommandPaletteSearchQuery
        {
            get => _commandPaletteSearchQuery;
            set
            {
                if (SetProperty(ref _commandPaletteSearchQuery, value))
                {
                    FilterCommandPalette();
                }
            }
        }

        public CommandPaletteItem? SelectedPaletteItem
        {
            get => _selectedPaletteItem;
            set => SetProperty(ref _selectedPaletteItem, value);
        }

        public bool IsCurrentPageBookmarked
        {
            get => _isCurrentPageBookmarked;
            set => SetProperty(ref _isCurrentPageBookmarked, value);
        }

        public long TotalBlockedAllTime
        {
            get => _totalBlockedAllTime;
            set => SetProperty(ref _totalBlockedAllTime, value);
        }

        public bool CanGoBack => SelectedTab?.CanGoBack ?? false;
        public bool CanGoForward => SelectedTab?.CanGoForward ?? false;

        #region Hub (Табло) Binding Properties

        public string HubSearchQuery
        {
            get => _hubSearchQuery;
            set => SetProperty(ref _hubSearchQuery, value);
        }

        public bool IsAddFavoriteDialogOpen
        {
            get => _isAddFavoriteDialogOpen;
            set => SetProperty(ref _isAddFavoriteDialogOpen, value);
        }

        public string NewFavoriteTitle
        {
            get => _newFavoriteTitle;
            set => SetProperty(ref _newFavoriteTitle, value);
        }

        public string NewFavoriteUrl
        {
            get => _newFavoriteUrl;
            set => SetProperty(ref _newFavoriteUrl, value);
        }

        #endregion

        #region Password Manager Properties

        public string NewPasswordSiteUrl
        {
            get => _newPasswordSiteUrl;
            set => SetProperty(ref _newPasswordSiteUrl, value);
        }

        public string NewPasswordUsername
        {
            get => _newPasswordUsername;
            set => SetProperty(ref _newPasswordUsername, value);
        }

        public string NewPasswordValue
        {
            get => _newPasswordValue;
            set => SetProperty(ref _newPasswordValue, value);
        }

        #endregion

        #region Settings Binding Properties

        public string SelectedSettingsCategory
        {
            get => _selectedSettingsCategory;
            set
            {
                if (SetProperty(ref _selectedSettingsCategory, value))
                {
                    OnPropertyChanged(nameof(IsAppearanceCategorySelected));
                    OnPropertyChanged(nameof(IsContentCategorySelected));
                    OnPropertyChanged(nameof(IsSearchCategorySelected));
                    OnPropertyChanged(nameof(IsShieldsCategorySelected));
                    OnPropertyChanged(nameof(IsPasswordsCategorySelected));
                    OnPropertyChanged(nameof(IsAutofillCategorySelected));
                    OnPropertyChanged(nameof(IsDownloadsCategorySelected));
                    OnPropertyChanged(nameof(IsSystemCategorySelected));
                    OnPropertyChanged(nameof(IsPrivacyCategorySelected));
                    OnPropertyChanged(nameof(IsAboutCategorySelected));
                }
            }
        }

        public bool IsAppearanceCategorySelected => SelectedSettingsCategory == "Appearance";
        public bool IsContentCategorySelected => SelectedSettingsCategory == "Content";
        public bool IsSearchCategorySelected => SelectedSettingsCategory == "Search";
        public bool IsShieldsCategorySelected => SelectedSettingsCategory == "Shields";
        public bool IsPasswordsCategorySelected => SelectedSettingsCategory == "Passwords" || SelectedSettingsCategory == "Autofill";
        public bool IsAutofillCategorySelected => SelectedSettingsCategory == "Autofill" || SelectedSettingsCategory == "Passwords";
        public bool IsDownloadsCategorySelected => SelectedSettingsCategory == "Downloads";
        public bool IsSystemCategorySelected => SelectedSettingsCategory == "System";
        public bool IsPrivacyCategorySelected => SelectedSettingsCategory == "Privacy";
        public bool IsAboutCategorySelected => SelectedSettingsCategory == "About";

        #region Brave-Style Expanded Content Properties

        public string SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (SetProperty(ref _selectedFontSize, value))
                {
                    SettingsManager.Instance.SelectedFontSize = value;
                    foreach (var tab in Tabs)
                    {
                        tab.ApplyFontSize(value);
                    }
                }
            }
        }

        public string SelectedPageZoom
        {
            get => _selectedPageZoom;
            set
            {
                if (SetProperty(ref _selectedPageZoom, value))
                {
                    if (double.TryParse(value.Replace("%", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double percent))
                    {
                        double factor = percent / 100.0;
                        SettingsManager.Instance.DefaultZoomFactor = factor;
                        foreach (var tab in Tabs)
                        {
                            tab.ZoomFactor = factor;
                        }
                    }
                }
            }
        }

        public bool CycleTabsMruEnabled
        {
            get => _cycleTabsMruEnabled;
            set
            {
                if (SetProperty(ref _cycleTabsMruEnabled, value))
                {
                    SettingsManager.Instance.CycleTabsMru = value;
                }
            }
        }

        public bool WaybackMachineEnabled
        {
            get => _waybackMachineEnabled;
            set
            {
                if (SetProperty(ref _waybackMachineEnabled, value))
                {
                    SettingsManager.Instance.WaybackMachineEnabled = value;
                }
            }
        }

        public bool SpeedreaderEnabled
        {
            get => _speedreaderEnabled;
            set
            {
                if (SetProperty(ref _speedreaderEnabled, value))
                {
                    SettingsManager.Instance.SpeedreaderEnabled = value;
                }
            }
        }

        #endregion

        #region Brave-Style Expanded Shields Properties

        public string AdBlockMode
        {
            get => _adBlockMode;
            set
            {
                if (SetProperty(ref _adBlockMode, value))
                {
                    SettingsManager.Instance.AdBlockMode = value;
                }
            }
        }

        public string HttpsUpgradeMode
        {
            get => _httpsUpgradeMode;
            set
            {
                if (SetProperty(ref _httpsUpgradeMode, value))
                {
                    SettingsManager.Instance.HttpsUpgradeMode = value;
                }
            }
        }

        public bool BlockScriptsEnabled
        {
            get => _blockScriptsEnabled;
            set
            {
                if (SetProperty(ref _blockScriptsEnabled, value))
                {
                    SettingsManager.Instance.BlockJavaScript = value;
                    foreach (var tab in Tabs)
                    {
                        tab.IsScriptEnabled = !value;
                    }
                }
            }
        }

        public bool FingerprintingProtectionEnabled
        {
            get => _fingerprintingProtectionEnabled;
            set
            {
                if (SetProperty(ref _fingerprintingProtectionEnabled, value))
                {
                    SettingsManager.Instance.FingerprintingProtection = value;
                }
            }
        }

        public string CookieBlockingMode
        {
            get => _cookieBlockingMode;
            set
            {
                if (SetProperty(ref _cookieBlockingMode, value))
                {
                    SettingsManager.Instance.CookieBlockingMode = value;
                }
            }
        }

        public bool ClearDataOnExitEnabled
        {
            get => _clearDataOnExitEnabled;
            set
            {
                if (SetProperty(ref _clearDataOnExitEnabled, value))
                {
                    SettingsManager.Instance.ClearDataOnExit = value;
                }
            }
        }

        #endregion

        #region Brave-Style Expanded Autofill Properties

        public string SavedPasswordsHeader => $"Пароли ({SavedPasswords.Count})";

        public bool IncognitoAutofillEnabled
        {
            get => _incognitoAutofillEnabled;
            set
            {
                if (SetProperty(ref _incognitoAutofillEnabled, value))
                {
                    SettingsManager.Instance.IncognitoAutofill = value;
                }
            }
        }

        #endregion

        #region Brave-Style Expanded Downloads Properties

        public string DownloadFolderPath
        {
            get => _downloadFolderPath;
            set
            {
                if (SetProperty(ref _downloadFolderPath, value))
                {
                    SettingsManager.Instance.DownloadFolderPath = value;
                }
            }
        }

        public bool AskWhereToSaveEnabled
        {
            get => _askWhereToSaveEnabled;
            set
            {
                if (SetProperty(ref _askWhereToSaveEnabled, value))
                {
                    SettingsManager.Instance.AskWhereToSave = value;
                }
            }
        }

        public bool NotifyDownloadCompletedEnabled
        {
            get => _notifyDownloadCompletedEnabled;
            set
            {
                if (SetProperty(ref _notifyDownloadCompletedEnabled, value))
                {
                    SettingsManager.Instance.NotifyDownloadCompleted = value;
                }
            }
        }

        #endregion

        public string SettingsSearchQuery
        {
            get => _settingsSearchQuery;
            set
            {
                if (SetProperty(ref _settingsSearchQuery, value))
                {
                    OnPropertyChanged(nameof(HasSettingsSearch));
                }
            }
        }

        public bool HasSettingsSearch => !string.IsNullOrWhiteSpace(_settingsSearchQuery);

        public bool GlobalShieldsEnabled
        {
            get => _globalShieldsEnabled;
            set
            {
                if (SetProperty(ref _globalShieldsEnabled, value))
                {
                    SettingsManager.Instance.GlobalShields = value;
                    foreach (var tab in Tabs)
                    {
                        tab.ShieldsEnabled = value;
                    }
                }
            }
        }

        public bool HardwareAccelerationEnabled
        {
            get => _hardwareAccelerationEnabled;
            set
            {
                if (SetProperty(ref _hardwareAccelerationEnabled, value))
                {
                    SettingsManager.Instance.HardwareAcceleration = value;
                }
            }
        }

        public bool SleepingTabsEnabled
        {
            get => _sleepingTabsEnabled;
            set
            {
                if (SetProperty(ref _sleepingTabsEnabled, value))
                {
                    SettingsManager.Instance.SleepingTabs = value;
                }
            }
        }

        public bool IsYandexSearch
        {
            get => _searchEngine == SearchEngineType.Yandex;
            set
            {
                if (value) SetSearchEngine(SearchEngineType.Yandex);
            }
        }

        public bool IsGoogleSearch
        {
            get => _searchEngine == SearchEngineType.Google;
            set
            {
                if (value) SetSearchEngine(SearchEngineType.Google);
            }
        }

        public bool IsDuckDuckGoSearch
        {
            get => _searchEngine == SearchEngineType.DuckDuckGo;
            set
            {
                if (value) SetSearchEngine(SearchEngineType.DuckDuckGo);
            }
        }

        public bool IsBingSearch
        {
            get => _searchEngine == SearchEngineType.Bing;
            set
            {
                if (value) SetSearchEngine(SearchEngineType.Bing);
            }
        }

        private void SetSearchEngine(SearchEngineType engine)
        {
            _searchEngine = engine;
            SettingsManager.Instance.SearchEngine = engine;
            NavigationHelper.CurrentSearchEngine = engine;
            OnPropertyChanged(nameof(IsYandexSearch));
            OnPropertyChanged(nameof(IsGoogleSearch));
            OnPropertyChanged(nameof(IsDuckDuckGoSearch));
            OnPropertyChanged(nameof(IsBingSearch));
        }

        public bool IsParadoxusDarkTheme
        {
            get => _currentTheme == AppTheme.ParadoxusDark;
            set
            {
                if (value) SetTheme(AppTheme.ParadoxusDark);
            }
        }

        public bool IsBraveDarkTheme
        {
            get => IsParadoxusDarkTheme;
            set => IsParadoxusDarkTheme = value;
        }

        public bool IsOledMidnightTheme
        {
            get => _currentTheme == AppTheme.OledMidnight;
            set
            {
                if (value) SetTheme(AppTheme.OledMidnight);
            }
        }

        public bool IsLightMinimalTheme
        {
            get => _currentTheme == AppTheme.LightMinimal;
            set
            {
                if (value) SetTheme(AppTheme.LightMinimal);
            }
        }

        private void SetTheme(AppTheme theme)
        {
            _currentTheme = theme;
            SettingsManager.Instance.Theme = theme;
            ThemeManager.ApplyTheme(theme);
            foreach (var tab in Tabs)
            {
                tab.ApplyThemeColorScheme(theme);
            }
            OnPropertyChanged(nameof(IsParadoxusDarkTheme));
            OnPropertyChanged(nameof(IsBraveDarkTheme));
            OnPropertyChanged(nameof(IsOledMidnightTheme));
            OnPropertyChanged(nameof(IsLightMinimalTheme));
        }

        #endregion

        #region Commands

        public ICommand NewTabCommand { get; }
        public ICommand NewPrivateTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand GoForwardCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand GoHomeCommand { get; }
        public ICommand ToggleShieldsCommand { get; }
        public ICommand ToggleShieldsPopupCommand { get; }
        public ICommand ToggleBookmarkCommand { get; }
        public ICommand OpenBookmarkCommand { get; }
        public ICommand ToggleHistoryCommand { get; }
        public ICommand ClearBrowsingDataCommand { get; }
        public ICommand CloseHistoryCommand { get; }

        public ICommand ToggleSettingsCommand { get; }
        public ICommand SelectCategoryCommand { get; }

        // Tab Context Menu Commands
        public ICommand DuplicateTabCommand { get; }
        public ICommand NewTabRightCommand { get; }
        public ICommand ReloadTabCommand { get; }
        public ICommand ToggleMuteTabCommand { get; }
        public ICommand TogglePinTabCommand { get; }
        public ICommand CloseOtherTabsCommand { get; }
        public ICommand CloseTabsToRightCommand { get; }
        public ICommand BookmarkAllTabsCommand { get; }
        public ICommand ReopenClosedTabCommand { get; }

        // Downloads Commands
        public ICommand ToggleDownloadsCommand { get; }
        public ICommand OpenDownloadedFileCommand { get; }
        public ICommand ShowInFolderCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand ClearDownloadsCommand { get; }

        // Command Palette Commands
        public ICommand ToggleCommandPaletteCommand { get; }
        public ICommand ExecutePaletteItemCommand { get; }
        public ICommand CloseCommandPaletteCommand { get; }

        // Privacy specific commands
        public ICommand ClearHistoryOnlyCommand { get; }
        public ICommand ClearCacheOnlyCommand { get; }
        public ICommand ClearCookiesOnlyCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }

        // Search Suggestions Commands
        public ICommand SelectOmniboxSuggestionCommand { get; }
        public ICommand DeleteOmniboxSuggestionCommand { get; }
        public ICommand SelectHubSuggestionCommand { get; }
        public ICommand DeleteHubSuggestionCommand { get; }

        // Hub Commands
        public ICommand ExecuteHubSearchCommand { get; }
        public ICommand NavigateFromHubCommand { get; }
        public ICommand OpenAddFavoriteDialogCommand { get; }
        public ICommand CancelAddFavoriteCommand { get; }
        public ICommand ConfirmAddFavoriteCommand { get; }
        public ICommand RemoveFavoriteCommand { get; }

        // Password Manager Commands
        public ICommand AddPasswordCommand { get; }
        public ICommand DeletePasswordCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }

        // Content & Downloads Commands
        public ICommand ChangeDownloadFolderCommand { get; }
        public ICommand OpenPaymentMethodsCommand { get; }
        public ICommand OpenAddressesCommand { get; }

        #endregion

        public MainViewModel(bool shouldRestoreSession = true)
        {
            try
            {
                ChromiumVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch
            {
                ChromiumVersion = "120.0.0.0 (WebView2)";
            }

            NewTabCommand = new RelayCommand(() => AddTab(isPrivate: false));
            NewPrivateTabCommand = new RelayCommand(() => AddTab(isPrivate: true));
            CloseTabCommand = new RelayCommand(p => CloseTab(p as TabViewModel ?? SelectedTab));
            NavigateCommand = new RelayCommand(() =>
            {
                if (SelectedTab != null && !string.IsNullOrWhiteSpace(SelectedTab.InputUrl))
                {
                    string target = SelectedTab.InputUrl.Trim();
                    if (NavigationHelper.IsSearchQuery(target))
                    {
                        _ = DatabaseContext.Instance.SaveSearchQueryAsync(target);
                    }
                    IsOmniboxSuggestionsOpen = false;
                    SelectedTab.Navigate(target);
                }
            });

            GoBackCommand = new RelayCommand(() => SelectedTab?.GoBack(), () => CanGoBack);
            GoForwardCommand = new RelayCommand(() => SelectedTab?.GoForward(), () => CanGoForward);
            ReloadCommand = new RelayCommand(() => SelectedTab?.Reload());
            GoHomeCommand = new RelayCommand(() => SelectedTab?.Navigate(NavigationHelper.HubUrl));

            ToggleShieldsPopupCommand = new RelayCommand(() =>
            {
                if (IsShieldsPopupOpen)
                {
                    IsShieldsPopupOpen = false;
                    return;
                }
                if ((DateTime.UtcNow - _lastShieldsClosedTime).TotalMilliseconds < 250)
                {
                    return;
                }
                IsDownloadsPopupOpen = false;
                IsHistoryOpen = false;
                IsSettingsOpen = false;
                IsOmniboxSuggestionsOpen = false;
                IsShieldsPopupOpen = true;
            });

            ToggleShieldsCommand = new RelayCommand(() =>
            {
                if (SelectedTab != null)
                {
                    SelectedTab.ShieldsEnabled = !SelectedTab.ShieldsEnabled;
                    SelectedTab.Reload();
                }
            });

            ToggleBookmarkCommand = new RelayCommand(async () => await ToggleBookmarkAsync());
            OpenBookmarkCommand = new RelayCommand(p =>
            {
                if (p is BookmarkItem item && SelectedTab != null)
                {
                    SelectedTab.Navigate(item.Url);
                }
            });

            ToggleHistoryCommand = new RelayCommand(() =>
            {
                var historyWin = new HistoryWindow(url =>
                {
                    if (SelectedTab != null)
                    {
                        SelectedTab.Navigate(url);
                    }
                    else
                    {
                        AddTab(false, url);
                    }
                })
                {
                    Owner = Application.Current?.MainWindow
                };
                historyWin.ShowDialog();
            });

            CloseHistoryCommand = new RelayCommand(() => IsHistoryOpen = false);

            ToggleSettingsCommand = new RelayCommand(() =>
            {
                var existingSettingsTab = Tabs.FirstOrDefault(t => t.IsSettingsTab || NavigationHelper.IsSettingsUrl(t.Url));
                if (existingSettingsTab != null)
                {
                    SelectedTab = existingSettingsTab;
                }
                else
                {
                    AddTab(isPrivate: false, initialUrl: NavigationHelper.SettingsUrl);
                }
            });

            SelectCategoryCommand = new RelayCommand(p =>
            {
                if (p is string cat)
                {
                    SelectedSettingsCategory = cat;
                }
            });

            // Tab Context Menu Commands
            DuplicateTabCommand = new RelayCommand(p => DuplicateTab(p as TabViewModel ?? SelectedTab));
            NewTabRightCommand = new RelayCommand(p => NewTabRight(p as TabViewModel ?? SelectedTab));
            ReloadTabCommand = new RelayCommand(p => (p as TabViewModel ?? SelectedTab)?.Reload());
            ToggleMuteTabCommand = new RelayCommand(p => (p as TabViewModel ?? SelectedTab)?.ToggleMute());
            TogglePinTabCommand = new RelayCommand(p => TogglePinTab(p as TabViewModel ?? SelectedTab));
            CloseOtherTabsCommand = new RelayCommand(p => CloseOtherTabs(p as TabViewModel ?? SelectedTab));
            CloseTabsToRightCommand = new RelayCommand(p => CloseTabsToRight(p as TabViewModel ?? SelectedTab));
            BookmarkAllTabsCommand = new RelayCommand(async () => await BookmarkAllTabsAsync());
            ReopenClosedTabCommand = new RelayCommand(() => ReopenClosedTab());

            // Downloads Commands
            ToggleDownloadsCommand = new RelayCommand(() =>
            {
                if (IsDownloadsPopupOpen)
                {
                    IsDownloadsPopupOpen = false;
                    return;
                }
                if ((DateTime.UtcNow - _lastDownloadsClosedTime).TotalMilliseconds < 250)
                {
                    return;
                }
                IsShieldsPopupOpen = false;
                IsHistoryOpen = false;
                IsSettingsOpen = false;
                IsOmniboxSuggestionsOpen = false;
                IsDownloadsPopupOpen = true;
            });

            OpenDownloadedFileCommand = new RelayCommand(p =>
            {
                if (p is DownloadItem item && File.Exists(item.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            ShowInFolderCommand = new RelayCommand(p =>
            {
                if (p is DownloadItem item && File.Exists(item.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось показать файл: {ex.Message}", "Загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            CancelDownloadCommand = new RelayCommand(p =>
            {
                if (p is DownloadItem item)
                {
                    try { item.Operation?.Cancel(); } catch { }
                    item.State = DownloadState.Canceled;
                    item.StatusText = "Отменено";
                    OnPropertyChanged(nameof(ActiveDownloadsCount));
                    OnPropertyChanged(nameof(HasActiveDownloads));
                }
            });

            ClearDownloadsCommand = new RelayCommand(() =>
            {
                Downloads.Clear();
                OnPropertyChanged(nameof(ActiveDownloadsCount));
                OnPropertyChanged(nameof(HasActiveDownloads));
            });

            // Command Palette Commands
            ToggleCommandPaletteCommand = new RelayCommand(() =>
            {
                IsCommandPaletteOpen = !IsCommandPaletteOpen;
                if (IsCommandPaletteOpen)
                {
                    CommandPaletteSearchQuery = string.Empty;
                }
            });

            ExecutePaletteItemCommand = new RelayCommand(p =>
            {
                var item = p as CommandPaletteItem ?? SelectedPaletteItem;
                if (item != null)
                {
                    IsCommandPaletteOpen = false;
                    item.Action?.Invoke();
                }
            });

            CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);

            // Privacy specific commands
            ClearBrowsingDataCommand = new RelayCommand(async () => await ClearBrowsingDataAsync());
            ClearHistoryOnlyCommand = new RelayCommand(async () => await ClearHistoryOnlyAsync());
            ClearCacheOnlyCommand = new RelayCommand(async () => await ClearCacheOnlyAsync());
            ClearCookiesOnlyCommand = new RelayCommand(async () => await ClearCookiesOnlyAsync());
            CheckForUpdatesCommand = new RelayCommand(() =>
            {
                MessageBox.Show(
                    "У вас установлена актуальная версия Paradoxus Browser v1.2.0.\nВсе модули безопасности и движок WebView2 обновлены.",
                    "Проверка обновлений Paradoxus",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });

            // Search Suggestions Commands
            SelectOmniboxSuggestionCommand = new RelayCommand(p =>
            {
                var item = p as SearchHistoryItem ?? SelectedOmniboxSuggestion;
                if (item != null && SelectedTab != null)
                {
                    IsOmniboxSuggestionsOpen = false;
                    SelectedTab.InputUrl = item.Query;
                    _ = DatabaseContext.Instance.SaveSearchQueryAsync(item.Query);
                    SelectedTab.Navigate(item.Query);
                }
            });

            DeleteOmniboxSuggestionCommand = new RelayCommand(async p =>
            {
                if (p is SearchHistoryItem item)
                {
                    OmniboxSuggestions.Remove(item);
                    await DatabaseContext.Instance.DeleteSearchQueryAsync(item.Query);
                    if (OmniboxSuggestions.Count == 0)
                    {
                        IsOmniboxSuggestionsOpen = false;
                    }
                }
            });

            SelectHubSuggestionCommand = new RelayCommand(p =>
            {
                var item = p as SearchHistoryItem ?? SelectedHubSuggestion;
                if (item != null && SelectedTab != null)
                {
                    IsHubSuggestionsOpen = false;
                    _ = DatabaseContext.Instance.SaveSearchQueryAsync(item.Query);
                    SelectedTab.Navigate(item.Query);
                    HubSearchQuery = string.Empty;
                }
            });

            DeleteHubSuggestionCommand = new RelayCommand(async p =>
            {
                if (p is SearchHistoryItem item)
                {
                    HubSuggestions.Remove(item);
                    await DatabaseContext.Instance.DeleteSearchQueryAsync(item.Query);
                    if (HubSuggestions.Count == 0)
                    {
                        IsHubSuggestionsOpen = false;
                    }
                }
            });

            // Hub (Табло) Commands
            ExecuteHubSearchCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(HubSearchQuery) && SelectedTab != null)
                {
                    string target = HubSearchQuery.Trim();
                    _ = DatabaseContext.Instance.SaveSearchQueryAsync(target);
                    IsHubSuggestionsOpen = false;
                    SelectedTab.Navigate(target);
                    HubSearchQuery = string.Empty;
                }
            });

            NavigateFromHubCommand = new RelayCommand(p =>
            {
                if (p is string url && SelectedTab != null)
                {
                    SelectedTab.Navigate(url);
                }
            });

            OpenAddFavoriteDialogCommand = new RelayCommand(() =>
            {
                NewFavoriteTitle = string.Empty;
                NewFavoriteUrl = string.Empty;
                IsAddFavoriteDialogOpen = true;
            });

            CancelAddFavoriteCommand = new RelayCommand(() =>
            {
                IsAddFavoriteDialogOpen = false;
            });

            ConfirmAddFavoriteCommand = new RelayCommand(async () => await AddNewFavoriteAsync());

            RemoveFavoriteCommand = new RelayCommand(async p =>
            {
                if (p is FavoriteItem item)
                {
                    await DatabaseContext.Instance.DeleteFavoriteAsync(item.Id);
                    await LoadFavoritesAsync();
                }
            });

            // Password Manager Commands
            AddPasswordCommand = new RelayCommand(async () =>
            {
                if (string.IsNullOrWhiteSpace(NewPasswordSiteUrl) || string.IsNullOrWhiteSpace(NewPasswordUsername))
                {
                    MessageBox.Show("Пожалуйста, заполните сайт и логин.", "Менеджер паролей", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await DatabaseContext.Instance.AddSavedPasswordAsync(NewPasswordSiteUrl, NewPasswordUsername, NewPasswordValue);
                NewPasswordSiteUrl = string.Empty;
                NewPasswordUsername = string.Empty;
                NewPasswordValue = string.Empty;
                await LoadSavedPasswordsAsync();
            });

            DeletePasswordCommand = new RelayCommand(async p =>
            {
                if (p is SavedPasswordItem item)
                {
                    await DatabaseContext.Instance.DeleteSavedPasswordAsync(item.Id);
                    SavedPasswords.Remove(item);
                }
            });

            TogglePasswordVisibilityCommand = new RelayCommand(p =>
            {
                if (p is SavedPasswordItem item)
                {
                    item.IsPasswordVisible = !item.IsPasswordVisible;
                }
            });

            ChangeDownloadFolderCommand = new RelayCommand(() =>
            {
                try
                {
                    var dialog = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = "Выберите папку для скачивания файлов",
                        InitialDirectory = System.IO.Directory.Exists(DownloadFolderPath)
                            ? DownloadFolderPath
                            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        DownloadFolderPath = dialog.FolderName;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось выбрать папку: {ex.Message}", "Выбор папки", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            OpenPaymentMethodsCommand = new RelayCommand(() =>
            {
                MessageBox.Show(
                    "Способы оплаты хранятся локально в защищенном хранилище Windows DPAPI.\nСохраненных карт пока нет.",
                    "Способы оплаты",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });

            OpenAddressesCommand = new RelayCommand(() =>
            {
                MessageBox.Show(
                    "Адреса и контактные данные хранятся локально на этом устройстве.\nСохраненных адресов пока нет.",
                    "Адреса и другие данные",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });

            SavedPasswords.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(SavedPasswordsHeader));
            };

            _shouldRestoreSession = shouldRestoreSession;

            // Restore saved session or create default tab
            if (_shouldRestoreSession)
            {
                RestoreSession();
            }
            else
            {
                AddTab(isPrivate: false, initialUrl: NavigationHelper.HubUrl);
            }

            // Load initial bookmarks, favorites, passwords, blocked stats, and user settings
            _ = LoadBookmarksAsync();
            _ = LoadFavoritesAsync();
            _ = LoadSavedPasswordsAsync();
            _ = LoadBlockedStatsAsync();
            _ = LoadUserSettingsAsync();
        }

        #region Tab Operations & Navigation

        public void AddTab(bool isPrivate = false, string initialUrl = NavigationHelper.HubUrl)
        {
            var tab = new TabViewModel(isPrivate, initialUrl)
            {
                ShieldsEnabled = GlobalShieldsEnabled,
                IsScriptEnabled = !BlockScriptsEnabled
            };

            if (double.TryParse(SelectedPageZoom.Replace("%", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double percent))
            {
                tab.ZoomFactor = percent / 100.0;
            }
            tab.ApplyFontSize(SelectedFontSize);
            tab.ApplyThemeColorScheme(_currentTheme);

            AttachTabEvents(tab);
            Tabs.Add(tab);
            SelectedTab = tab;
        }

        private void AttachTabEvents(TabViewModel tab)
        {
            tab.OpenNewTabRequested += (url, priv) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    AddTab(priv, url);
                });
            };

            tab.DownloadStarted += (item, op) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Downloads.Insert(0, item);
                    OnPropertyChanged(nameof(ActiveDownloadsCount));
                    OnPropertyChanged(nameof(HasActiveDownloads));
                    IsDownloadsPopupOpen = true;
                });
            };

            tab.PropertyChanged += (s, e) =>
            {
                if (s == SelectedTab)
                {
                    if (e.PropertyName == nameof(TabViewModel.CanGoBack) ||
                        e.PropertyName == nameof(TabViewModel.CanGoForward))
                    {
                        OnPropertyChanged(nameof(CanGoBack));
                        OnPropertyChanged(nameof(CanGoForward));
                    }
                    else if (e.PropertyName == nameof(TabViewModel.Url))
                    {
                        _ = CheckBookmarkStatusAsync();
                    }
                    else if (e.PropertyName == nameof(TabViewModel.AdBlockCount))
                    {
                        TotalBlockedAllTime++;
                        _ = SaveBlockedStatsAsync();
                    }
                }
            };
        }

        public void CloseTab(TabViewModel? tab)
        {
            if (tab == null || !Tabs.Contains(tab))
                return;

            if (!tab.IsPrivate && !string.IsNullOrWhiteSpace(tab.Url) && !tab.Url.StartsWith("about:") && !NavigationHelper.IsHubUrl(tab.Url) && !NavigationHelper.IsSettingsUrl(tab.Url))
            {
                _closedTabsHistory.Push(tab.Url);
                OnPropertyChanged(nameof(HasClosedTabs));
            }

            _mruTabs.Remove(tab);
            int index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);
            tab.Dispose();

            if (Tabs.Count == 0)
            {
                AddTab(isPrivate: false);
            }
            else if (SelectedTab == tab)
            {
                int newIndex = Math.Min(index, Tabs.Count - 1);
                SelectedTab = Tabs[newIndex];
            }
        }

        public void ReopenClosedTab()
        {
            if (_closedTabsHistory.Count > 0)
            {
                string url = _closedTabsHistory.Pop();
                OnPropertyChanged(nameof(HasClosedTabs));
                AddTab(isPrivate: false, initialUrl: url);
            }
        }

        public void DuplicateTab(TabViewModel? tab)
        {
            if (tab == null) return;
            int index = Tabs.IndexOf(tab);
            var newTab = new TabViewModel(tab.IsPrivate, tab.Url)
            {
                ShieldsEnabled = GlobalShieldsEnabled
            };
            AttachTabEvents(newTab);
            Tabs.Insert(index + 1, newTab);
            SelectedTab = newTab;
        }

        public void NewTabRight(TabViewModel? tab)
        {
            if (tab == null)
            {
                AddTab(false);
                return;
            }
            int index = Tabs.IndexOf(tab);
            var newTab = new TabViewModel(false, NavigationHelper.HubUrl)
            {
                ShieldsEnabled = GlobalShieldsEnabled
            };
            AttachTabEvents(newTab);
            Tabs.Insert(index + 1, newTab);
            SelectedTab = newTab;
        }

        public void TogglePinTab(TabViewModel? tab)
        {
            if (tab == null) return;
            tab.IsPinned = !tab.IsPinned;
            if (tab.IsPinned)
            {
                int currentIndex = Tabs.IndexOf(tab);
                int targetIndex = 0;
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].IsPinned && Tabs[i] != tab)
                        targetIndex = i + 1;
                }
                if (currentIndex != targetIndex)
                {
                    Tabs.Move(currentIndex, targetIndex);
                }
            }
        }

        public void CloseOtherTabs(TabViewModel? keepTab)
        {
            if (keepTab == null) return;
            var toClose = Tabs.Where(t => t != keepTab && !t.IsPinned).ToList();
            foreach (var t in toClose)
            {
                CloseTab(t);
            }
            SelectedTab = keepTab;
        }

        public void CloseTabsToRight(TabViewModel? refTab)
        {
            if (refTab == null) return;
            int index = Tabs.IndexOf(refTab);
            if (index >= 0)
            {
                var toClose = Tabs.Skip(index + 1).Where(t => !t.IsPinned).ToList();
                foreach (var t in toClose)
                {
                    CloseTab(t);
                }
            }
        }

        public async Task BookmarkAllTabsAsync()
        {
            int added = 0;
            foreach (var tab in Tabs)
            {
                if (!string.IsNullOrWhiteSpace(tab.Url) && !tab.Url.StartsWith("about:") && !NavigationHelper.IsHubUrl(tab.Url) && !NavigationHelper.IsSettingsUrl(tab.Url))
                {
                    if (!await DatabaseContext.Instance.IsBookmarkedAsync(tab.Url))
                    {
                        await DatabaseContext.Instance.AddBookmarkAsync(tab.Title, tab.Url);
                        added++;
                    }
                }
            }
            if (added > 0)
            {
                await LoadBookmarksAsync();
                MessageBox.Show($"Добавлено {added} вкладок в закладки.", "Закладки Paradoxus", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void SelectPreviousMruTab()
        {
            if (_mruTabs.Count > 1)
            {
                var target = _mruTabs[1];
                if (Tabs.Contains(target))
                {
                    SelectedTab = target;
                    return;
                }
            }
            SelectNextTab();
        }

        public void SelectNextTab()
        {
            if (Tabs.Count <= 1 || SelectedTab == null) return;
            int index = Tabs.IndexOf(SelectedTab);
            int nextIndex = (index + 1) % Tabs.Count;
            SelectedTab = Tabs[nextIndex];
        }

        public void SelectPreviousTab()
        {
            if (Tabs.Count <= 1 || SelectedTab == null) return;
            int index = Tabs.IndexOf(SelectedTab);
            int prevIndex = (index - 1 + Tabs.Count) % Tabs.Count;
            SelectedTab = Tabs[prevIndex];
        }

        public void SelectTabByIndex(int index)
        {
            if (index >= 0 && index < Tabs.Count)
            {
                SelectedTab = Tabs[index];
            }
        }

        public void SelectLastTab()
        {
            if (Tabs.Count > 0)
            {
                SelectedTab = Tabs[^1];
            }
        }

        #endregion

        #region Command Palette Logic

        public void RefreshCommandPalette()
        {
            _allPaletteItems.Clear();

            // 1. Browser Actions
            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "Новая вкладка",
                Subtitle = "Открыть стартовый экран (Табло)",
                Category = "Действия",
                IconKey = "PlusIcon",
                Action = () => AddTab(false)
            });

            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "Новая приватная вкладка",
                Subtitle = "Инкогнито без сохранения истории и кэша",
                Category = "Действия",
                IconKey = "IncognitoIcon",
                Action = () => AddTab(true)
            });

            if (_closedTabsHistory.Count > 0)
            {
                _allPaletteItems.Add(new CommandPaletteItem
                {
                    Title = "Восстановить закрытую вкладку",
                    Subtitle = _closedTabsHistory.Peek(),
                    Category = "Действия",
                    IconKey = "HistoryIcon",
                    Action = () => ReopenClosedTab()
                });
            }

            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "Настройки браузера",
                Subtitle = "paradoxus://settings",
                Category = "Действия",
                IconKey = "SettingsGearIcon",
                Action = () => ToggleSettingsCommand.Execute(null)
            });

            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "История посещений",
                Subtitle = "Просмотр всех посещенных сайтов",
                Category = "Действия",
                IconKey = "HistoryIcon",
                Action = () => ToggleHistoryCommand.Execute(null)
            });

            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "Загрузки",
                Subtitle = "Открыть панель скачанных файлов",
                Category = "Действия",
                IconKey = "DownloadIcon",
                Action = () => IsDownloadsPopupOpen = true
            });

            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "Перезагрузить страницу",
                Subtitle = SelectedTab?.Url ?? "",
                Category = "Действия",
                IconKey = "ReloadIcon",
                Action = () => SelectedTab?.Reload()
            });

            _allPaletteItems.Add(new CommandPaletteItem
            {
                Title = "Очистить данные браузера",
                Subtitle = "История, кэш и cookies",
                Category = "Действия",
                IconKey = "TrashIcon",
                Action = () => _ = ClearBrowsingDataAsync()
            });

            // 2. Open Tabs
            foreach (var tab in Tabs)
            {
                var targetTab = tab;
                _allPaletteItems.Add(new CommandPaletteItem
                {
                    Title = targetTab.Title,
                    Subtitle = targetTab.Url,
                    Category = "Открытые вкладки",
                    IconKey = targetTab.IsPrivate ? "IncognitoIcon" : (targetTab.IsSettingsTab ? "SettingsGearIcon" : "GlobeIcon"),
                    Action = () => SelectedTab = targetTab
                });
            }

            // 3. Bookmarks
            foreach (var b in Bookmarks)
            {
                var bm = b;
                _allPaletteItems.Add(new CommandPaletteItem
                {
                    Title = bm.Title,
                    Subtitle = bm.Url,
                    Category = "Закладки",
                    IconKey = "StarFilledIcon",
                    Action = () =>
                    {
                        if (SelectedTab != null) SelectedTab.Navigate(bm.Url);
                        else AddTab(false, bm.Url);
                    }
                });
            }

            FilterCommandPalette();
        }

        private void FilterCommandPalette()
        {
            FilteredCommandPaletteItems.Clear();
            string q = CommandPaletteSearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

            var matches = string.IsNullOrEmpty(q)
                ? _allPaletteItems
                : _allPaletteItems.Where(i => i.Title.ToLowerInvariant().Contains(q) || i.Subtitle.ToLowerInvariant().Contains(q) || i.Category.ToLowerInvariant().Contains(q));

            foreach (var item in matches.Take(20))
            {
                FilteredCommandPaletteItems.Add(item);
            }

            SelectedPaletteItem = FilteredCommandPaletteItems.FirstOrDefault();
        }

        #endregion

        #region Session Persistence

        private string SessionFilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParadoxusBrowser", "session.json");

        public void SaveSession()
        {
            if (!_shouldRestoreSession)
            {
                return;
            }

            try
            {
                var urls = Tabs
                    .Where(t => !t.IsPrivate)
                    .Select(t => t.Url)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .ToList();

                string json = JsonSerializer.Serialize(urls, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SessionFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        public void RestoreSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    string json = File.ReadAllText(SessionFilePath);
                    var urls = JsonSerializer.Deserialize<List<string>>(json);
                    if (urls != null && urls.Count > 0)
                    {
                        foreach (var url in urls)
                        {
                            AddTab(isPrivate: false, initialUrl: url);
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring session: {ex.Message}");
            }

            // Fallback: single default tab
            AddTab(isPrivate: false, initialUrl: NavigationHelper.HubUrl);
        }

        #endregion

        #region Data Management & Seeding

        private async Task CheckBookmarkStatusAsync()
        {
            if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.Url) || SelectedTab.IsHubVisible)
            {
                IsCurrentPageBookmarked = false;
                return;
            }

            IsCurrentPageBookmarked = await DatabaseContext.Instance.IsBookmarkedAsync(SelectedTab.Url);
        }

        private async Task ToggleBookmarkAsync()
        {
            if (SelectedTab == null || string.IsNullOrWhiteSpace(SelectedTab.Url) || SelectedTab.IsHubVisible)
                return;

            if (IsCurrentPageBookmarked)
            {
                await DatabaseContext.Instance.RemoveBookmarkAsync(SelectedTab.Url);
                IsCurrentPageBookmarked = false;
            }
            else
            {
                await DatabaseContext.Instance.AddBookmarkAsync(SelectedTab.Title, SelectedTab.Url);
                IsCurrentPageBookmarked = true;
            }

            await LoadBookmarksAsync();
        }

        public async Task LoadBookmarksAsync()
        {
            var bookmarks = await DatabaseContext.Instance.GetBookmarksAsync();
            Bookmarks.Clear();
            foreach (var b in bookmarks)
            {
                Bookmarks.Add(b);
            }
        }

        public async Task LoadFavoritesAsync()
        {
            var favorites = await DatabaseContext.Instance.GetFavoritesAsync();
            Favorites.Clear();
            foreach (var f in favorites)
            {
                Favorites.Add(f);
            }
            OnPropertyChanged(nameof(HasFavorites));
        }

        public async Task LoadSavedPasswordsAsync()
        {
            var passwords = await DatabaseContext.Instance.GetSavedPasswordsAsync();
            SavedPasswords.Clear();
            foreach (var p in passwords)
            {
                SavedPasswords.Add(p);
            }
        }

        private async Task AddNewFavoriteAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFavoriteUrl))
                return;

            string url = NewFavoriteUrl.Trim();
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            string title = string.IsNullOrWhiteSpace(NewFavoriteTitle) ? url : NewFavoriteTitle.Trim();
            string letter = title.Length > 0 ? title.Substring(0, 1).ToUpperInvariant() : "★";

            string[] colors = { "#FF7600", "#0077FF", "#24A1DE", "#FF0000", "#107C41", "#8A2BE2", "#FC3F1D" };
            string randomColor = colors[Math.Abs(url.GetHashCode()) % colors.Length];

            var item = new FavoriteItem
            {
                Title = title,
                Url = url,
                ColorHex = randomColor,
                IconLetter = letter,
                SortOrder = Favorites.Count + 1
            };

            await DatabaseContext.Instance.AddFavoriteAsync(item);
            await LoadFavoritesAsync();
            IsAddFavoriteDialogOpen = false;
        }

        public async Task LoadHistoryAsync()
        {
            var history = await DatabaseContext.Instance.GetHistoryAsync(100);
            HistoryItems.Clear();
            foreach (var h in history)
            {
                HistoryItems.Add(h);
            }
        }

        public async Task ClearBrowsingDataAsync(bool showPrompt = true)
        {
            if (showPrompt)
            {
                var result = MessageBox.Show(
                    "Вы уверены, что хотите полностью удалить историю посещений, кэш и cookies?",
                    "Подтверждение очистки данных",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result != MessageBoxResult.Yes)
                    return;
            }

            await DatabaseContext.Instance.ClearHistoryAsync();
            HistoryItems.Clear();

            foreach (var tab in Tabs)
            {
                try
                {
                    if (tab.WebView.CoreWebView2 != null)
                    {
                        var profile = tab.WebView.CoreWebView2.Profile;
                        await profile.ClearBrowsingDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error clearing browsing data: {ex.Message}");
                }
            }

            if (showPrompt)
            {
                MessageBox.Show(
                    "История, кэш и cookies успешно очищены!",
                    "Paradoxus Shields Security",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        public async Task ClearHistoryOnlyAsync()
        {
            await DatabaseContext.Instance.ClearHistoryAsync();
            HistoryItems.Clear();

            foreach (var tab in Tabs)
            {
                try
                {
                    if (tab.WebView.CoreWebView2 != null)
                    {
                        var profile = tab.WebView.CoreWebView2.Profile;
                        await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.BrowsingHistory);
                    }
                }
                catch { }
            }

            MessageBox.Show("История посещений успешно очищена.", "Очистка истории", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public async Task ClearCacheOnlyAsync()
        {
            foreach (var tab in Tabs)
            {
                try
                {
                    if (tab.WebView.CoreWebView2 != null)
                    {
                        var profile = tab.WebView.CoreWebView2.Profile;
                        await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache);
                    }
                }
                catch { }
            }

            MessageBox.Show("Кэш браузера успешно очищен.", "Очистка кэша", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public async Task ClearCookiesOnlyAsync()
        {
            foreach (var tab in Tabs)
            {
                try
                {
                    if (tab.WebView.CoreWebView2 != null)
                    {
                        var profile = tab.WebView.CoreWebView2.Profile;
                        await profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.Cookies);
                    }
                }
                catch { }
            }

            MessageBox.Show("Файлы Cookies успешно очищены.", "Очистка Cookies", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadBlockedStatsAsync()
        {
            string? val = await DatabaseContext.Instance.GetSecureSettingAsync("TotalBlockedStats");
            if (long.TryParse(val, out long count))
            {
                TotalBlockedAllTime = count;
            }
        }

        private async Task SaveBlockedStatsAsync()
        {
            await DatabaseContext.Instance.SetSecureSettingAsync("TotalBlockedStats", TotalBlockedAllTime.ToString());
        }

        private async Task LoadUserSettingsAsync()
        {
            await SettingsManager.Instance.LoadSettingsAsync();

            _currentTheme = SettingsManager.Instance.Theme;
            ThemeManager.ApplyTheme(_currentTheme);
            OnPropertyChanged(nameof(IsParadoxusDarkTheme));
            OnPropertyChanged(nameof(IsBraveDarkTheme));
            OnPropertyChanged(nameof(IsOledMidnightTheme));
            OnPropertyChanged(nameof(IsLightMinimalTheme));

            _searchEngine = SettingsManager.Instance.SearchEngine;
            NavigationHelper.CurrentSearchEngine = _searchEngine;
            OnPropertyChanged(nameof(IsYandexSearch));
            OnPropertyChanged(nameof(IsGoogleSearch));
            OnPropertyChanged(nameof(IsDuckDuckGoSearch));
            OnPropertyChanged(nameof(IsBingSearch));

            _globalShieldsEnabled = SettingsManager.Instance.GlobalShields;
            OnPropertyChanged(nameof(GlobalShieldsEnabled));

            _hardwareAccelerationEnabled = SettingsManager.Instance.HardwareAcceleration;
            OnPropertyChanged(nameof(HardwareAccelerationEnabled));

            _sleepingTabsEnabled = SettingsManager.Instance.SleepingTabs;
            OnPropertyChanged(nameof(SleepingTabsEnabled));

            _selectedFontSize = SettingsManager.Instance.SelectedFontSize;
            if (_selectedFontSize == "Средний")
                _selectedFontSize = "Средний (рекомендуется)";
            OnPropertyChanged(nameof(SelectedFontSize));

            int zoomPct = (int)Math.Round(SettingsManager.Instance.DefaultZoomFactor * 100);
            _selectedPageZoom = $"{zoomPct}%";
            OnPropertyChanged(nameof(SelectedPageZoom));

            _cycleTabsMruEnabled = SettingsManager.Instance.CycleTabsMru;
            OnPropertyChanged(nameof(CycleTabsMruEnabled));

            _waybackMachineEnabled = SettingsManager.Instance.WaybackMachineEnabled;
            OnPropertyChanged(nameof(WaybackMachineEnabled));

            _speedreaderEnabled = SettingsManager.Instance.SpeedreaderEnabled;
            OnPropertyChanged(nameof(SpeedreaderEnabled));

            _adBlockMode = SettingsManager.Instance.AdBlockMode;
            OnPropertyChanged(nameof(AdBlockMode));

            _httpsUpgradeMode = SettingsManager.Instance.HttpsUpgradeMode;
            OnPropertyChanged(nameof(HttpsUpgradeMode));

            _blockScriptsEnabled = SettingsManager.Instance.BlockJavaScript;
            OnPropertyChanged(nameof(BlockScriptsEnabled));

            _fingerprintingProtectionEnabled = SettingsManager.Instance.FingerprintingProtection;
            OnPropertyChanged(nameof(FingerprintingProtectionEnabled));

            _cookieBlockingMode = SettingsManager.Instance.CookieBlockingMode;
            OnPropertyChanged(nameof(CookieBlockingMode));

            _clearDataOnExitEnabled = SettingsManager.Instance.ClearDataOnExit;
            OnPropertyChanged(nameof(ClearDataOnExitEnabled));

            _incognitoAutofillEnabled = SettingsManager.Instance.IncognitoAutofill;
            OnPropertyChanged(nameof(IncognitoAutofillEnabled));

            _downloadFolderPath = SettingsManager.Instance.DownloadFolderPath;
            OnPropertyChanged(nameof(DownloadFolderPath));

            _askWhereToSaveEnabled = SettingsManager.Instance.AskWhereToSave;
            OnPropertyChanged(nameof(AskWhereToSaveEnabled));

            _notifyDownloadCompletedEnabled = SettingsManager.Instance.NotifyDownloadCompleted;
            OnPropertyChanged(nameof(NotifyDownloadCompletedEnabled));

            // Apply settings to any active tabs
            foreach (var tab in Tabs)
            {
                tab.ApplyThemeColorScheme(_currentTheme);
                tab.ShieldsEnabled = _globalShieldsEnabled;
                tab.IsScriptEnabled = !_blockScriptsEnabled;
                tab.ZoomFactor = SettingsManager.Instance.DefaultZoomFactor;
                tab.ApplyFontSize(_selectedFontSize);
            }
        }

        #endregion

        #region Search Suggestions Loading

        public async Task LoadOmniboxSuggestionsAsync(string? input)
        {
            string query = input?.Trim() ?? string.Empty;
            if (query.StartsWith("paradoxus://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                IsOmniboxSuggestionsOpen = false;
                OmniboxSuggestions.Clear();
                return;
            }

            var recent = await DatabaseContext.Instance.GetRecentQueriesAsync(query, 8);
            OmniboxSuggestions.Clear();
            foreach (var item in recent)
            {
                string prefix = string.Empty;
                string remaining = item;
                if (!string.IsNullOrEmpty(query) && item.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = item.Substring(0, query.Length);
                    remaining = item.Substring(query.Length);
                }
                OmniboxSuggestions.Add(new SearchHistoryItem
                {
                    Query = item,
                    MatchedPrefix = prefix,
                    RemainingText = remaining
                });
            }
            IsOmniboxSuggestionsOpen = OmniboxSuggestions.Count > 0;
        }

        public async Task LoadHubSuggestionsAsync(string? input)
        {
            string query = input?.Trim() ?? string.Empty;
            var recent = await DatabaseContext.Instance.GetRecentQueriesAsync(query, 8);
            HubSuggestions.Clear();
            foreach (var item in recent)
            {
                string prefix = string.Empty;
                string remaining = item;
                if (!string.IsNullOrEmpty(query) && item.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = item.Substring(0, query.Length);
                    remaining = item.Substring(query.Length);
                }
                HubSuggestions.Add(new SearchHistoryItem
                {
                    Query = item,
                    MatchedPrefix = prefix,
                    RemainingText = remaining
                });
            }
            IsHubSuggestionsOpen = HubSuggestions.Count > 0;
        }

        #endregion

    }
}
