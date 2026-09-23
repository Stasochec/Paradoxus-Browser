using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using ParadoxusBrowser.Data;

namespace ParadoxusBrowser.Core
{
    /// <summary>
    /// Centralized singleton managing all browser settings with auto-persistence in SQLite (DPAPI encrypted)
    /// and reactive notifications for tabs and UI components.
    /// </summary>
    public class SettingsManager
    {
        private static readonly Lazy<SettingsManager> _instance = new(() => new SettingsManager());
        public static SettingsManager Instance => _instance.Value;

        public event Action<string>? SettingChanged;

        // Appearance
        private AppTheme _theme = AppTheme.ParadoxusDark;

        // Shields & Security
        private bool _globalShields = true;
        private string _adBlockMode = "Стандартная";
        private string _httpsUpgradeMode = "Стандартная";
        private bool _blockJavaScript = false;
        private bool _fingerprintingProtection = true;
        private string _cookieBlockingMode = "Блокировать межсайтовые куки";
        private bool _clearDataOnExit = false;

        // Content
        private string _selectedFontSize = "Средний (рекомендуется)";
        private double _defaultZoomFactor = 1.0;
        private bool _cycleTabsMru = false;
        private bool _waybackMachineEnabled = true;
        private bool _speedreaderEnabled = false;

        // Downloads
        private string _downloadFolderPath;
        private bool _askWhereToSave = false;
        private bool _notifyDownloadCompleted = true;

        // Search & System
        private SearchEngineType _searchEngine = SearchEngineType.Yandex;
        private bool _hardwareAcceleration = true;
        private bool _sleepingTabs = true;
        private bool _incognitoAutofill = false;

        public SettingsManager()
        {
            string defaultDl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            _downloadFolderPath = defaultDl;
        }

        #region Properties with Auto-Save

        public AppTheme Theme
        {
            get => _theme;
            set
            {
                if (_theme != value)
                {
                    _theme = value;
                    SaveSettingAsync("SelectedTheme", value.ToString());
                    SettingChanged?.Invoke(nameof(Theme));
                }
            }
        }

        public bool GlobalShields
        {
            get => _globalShields;
            set
            {
                if (_globalShields != value)
                {
                    _globalShields = value;
                    SaveSettingAsync("GlobalShields", value.ToString());
                    SettingChanged?.Invoke(nameof(GlobalShields));
                }
            }
        }

        public string AdBlockMode
        {
            get => _adBlockMode;
            set
            {
                if (_adBlockMode != value)
                {
                    _adBlockMode = value;
                    SaveSettingAsync("AdBlockMode", value);
                    SettingChanged?.Invoke(nameof(AdBlockMode));
                }
            }
        }

        public string HttpsUpgradeMode
        {
            get => _httpsUpgradeMode;
            set
            {
                if (_httpsUpgradeMode != value)
                {
                    _httpsUpgradeMode = value;
                    SaveSettingAsync("HttpsUpgradeMode", value);
                    SettingChanged?.Invoke(nameof(HttpsUpgradeMode));
                }
            }
        }

        public bool BlockJavaScript
        {
            get => _blockJavaScript;
            set
            {
                if (_blockJavaScript != value)
                {
                    _blockJavaScript = value;
                    SaveSettingAsync("BlockScripts", value.ToString());
                    SettingChanged?.Invoke(nameof(BlockJavaScript));
                }
            }
        }

        public bool FingerprintingProtection
        {
            get => _fingerprintingProtection;
            set
            {
                if (_fingerprintingProtection != value)
                {
                    _fingerprintingProtection = value;
                    SaveSettingAsync("FingerprintingProtection", value.ToString());
                    SettingChanged?.Invoke(nameof(FingerprintingProtection));
                }
            }
        }

        public string CookieBlockingMode
        {
            get => _cookieBlockingMode;
            set
            {
                if (_cookieBlockingMode != value)
                {
                    _cookieBlockingMode = value;
                    SaveSettingAsync("CookieBlockingMode", value);
                    SettingChanged?.Invoke(nameof(CookieBlockingMode));
                }
            }
        }

        public bool ClearDataOnExit
        {
            get => _clearDataOnExit;
            set
            {
                if (_clearDataOnExit != value)
                {
                    _clearDataOnExit = value;
                    SaveSettingAsync("ClearDataOnExit", value.ToString());
                    SettingChanged?.Invoke(nameof(ClearDataOnExit));
                }
            }
        }

        public string SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (_selectedFontSize != value)
                {
                    _selectedFontSize = value;
                    SaveSettingAsync("FontSize", value);
                    SettingChanged?.Invoke(nameof(SelectedFontSize));
                }
            }
        }

        public double DefaultZoomFactor
        {
            get => _defaultZoomFactor;
            set
            {
                if (Math.Abs(_defaultZoomFactor - value) > 0.001)
                {
                    _defaultZoomFactor = value;
                    int percent = (int)Math.Round(value * 100);
                    SaveSettingAsync("PageZoom", $"{percent}%");
                    SettingChanged?.Invoke(nameof(DefaultZoomFactor));
                }
            }
        }

        public bool CycleTabsMru
        {
            get => _cycleTabsMru;
            set
            {
                if (_cycleTabsMru != value)
                {
                    _cycleTabsMru = value;
                    SaveSettingAsync("CycleTabsMru", value.ToString());
                    SettingChanged?.Invoke(nameof(CycleTabsMru));
                }
            }
        }

        public bool WaybackMachineEnabled
        {
            get => _waybackMachineEnabled;
            set
            {
                if (_waybackMachineEnabled != value)
                {
                    _waybackMachineEnabled = value;
                    SaveSettingAsync("WaybackMachine", value.ToString());
                    SettingChanged?.Invoke(nameof(WaybackMachineEnabled));
                }
            }
        }

        public bool SpeedreaderEnabled
        {
            get => _speedreaderEnabled;
            set
            {
                if (_speedreaderEnabled != value)
                {
                    _speedreaderEnabled = value;
                    SaveSettingAsync("Speedreader", value.ToString());
                    SettingChanged?.Invoke(nameof(SpeedreaderEnabled));
                }
            }
        }

        public string DownloadFolderPath
        {
            get => _downloadFolderPath;
            set
            {
                if (_downloadFolderPath != value)
                {
                    _downloadFolderPath = value;
                    SaveSettingAsync("DownloadFolderPath", value);
                    SettingChanged?.Invoke(nameof(DownloadFolderPath));
                }
            }
        }

        public bool AskWhereToSave
        {
            get => _askWhereToSave;
            set
            {
                if (_askWhereToSave != value)
                {
                    _askWhereToSave = value;
                    SaveSettingAsync("AskWhereToSave", value.ToString());
                    SettingChanged?.Invoke(nameof(AskWhereToSave));
                }
            }
        }

        public bool NotifyDownloadCompleted
        {
            get => _notifyDownloadCompleted;
            set
            {
                if (_notifyDownloadCompleted != value)
                {
                    _notifyDownloadCompleted = value;
                    SaveSettingAsync("NotifyDownloadCompleted", value.ToString());
                    SettingChanged?.Invoke(nameof(NotifyDownloadCompleted));
                }
            }
        }

        public SearchEngineType SearchEngine
        {
            get => _searchEngine;
            set
            {
                if (_searchEngine != value)
                {
                    _searchEngine = value;
                    SaveSettingAsync("SearchEngine", value.ToString());
                    SettingChanged?.Invoke(nameof(SearchEngine));
                }
            }
        }

        public bool HardwareAcceleration
        {
            get => _hardwareAcceleration;
            set
            {
                if (_hardwareAcceleration != value)
                {
                    _hardwareAcceleration = value;
                    SaveSettingAsync("HardwareAcceleration", value.ToString());
                    SettingChanged?.Invoke(nameof(HardwareAcceleration));
                }
            }
        }

        public bool SleepingTabs
        {
            get => _sleepingTabs;
            set
            {
                if (_sleepingTabs != value)
                {
                    _sleepingTabs = value;
                    SaveSettingAsync("SleepingTabs", value.ToString());
                    SettingChanged?.Invoke(nameof(SleepingTabs));
                }
            }
        }

        public bool IncognitoAutofill
        {
            get => _incognitoAutofill;
            set
            {
                if (_incognitoAutofill != value)
                {
                    _incognitoAutofill = value;
                    SaveSettingAsync("IncognitoAutofill", value.ToString());
                    SettingChanged?.Invoke(nameof(IncognitoAutofill));
                }
            }
        }

        #endregion

        #region Persistence

        private static void SaveSettingAsync(string key, string value)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await DatabaseContext.Instance.SetSecureSettingAsync(key, value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save setting {key}: {ex.Message}");
                }
            });
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                // Theme
                string? themeStr = await DatabaseContext.Instance.GetSecureSettingAsync("SelectedTheme");
                if (Enum.TryParse<AppTheme>(themeStr, out var th))
                {
                    _theme = th;
                }

                // Global Shields
                string? shieldsStr = await DatabaseContext.Instance.GetSecureSettingAsync("GlobalShields");
                if (bool.TryParse(shieldsStr, out bool shields))
                {
                    _globalShields = shields;
                }

                // AdBlock Mode
                string? adMode = await DatabaseContext.Instance.GetSecureSettingAsync("AdBlockMode");
                if (!string.IsNullOrWhiteSpace(adMode))
                {
                    _adBlockMode = adMode;
                }

                // HTTPS Upgrade Mode
                string? httpsMode = await DatabaseContext.Instance.GetSecureSettingAsync("HttpsUpgradeMode");
                if (!string.IsNullOrWhiteSpace(httpsMode))
                {
                    _httpsUpgradeMode = httpsMode;
                }

                // Block JavaScript
                string? blockJs = await DatabaseContext.Instance.GetSecureSettingAsync("BlockScripts");
                if (bool.TryParse(blockJs, out bool bjs))
                {
                    _blockJavaScript = bjs;
                }

                // Fingerprinting Protection
                string? fpStr = await DatabaseContext.Instance.GetSecureSettingAsync("FingerprintingProtection");
                if (bool.TryParse(fpStr, out bool fp))
                {
                    _fingerprintingProtection = fp;
                }

                // Cookie Blocking Mode
                string? cookieStr = await DatabaseContext.Instance.GetSecureSettingAsync("CookieBlockingMode");
                if (!string.IsNullOrWhiteSpace(cookieStr))
                {
                    _cookieBlockingMode = cookieStr;
                }

                // Clear Data On Exit
                string? clearExitStr = await DatabaseContext.Instance.GetSecureSettingAsync("ClearDataOnExit");
                if (bool.TryParse(clearExitStr, out bool clearExit))
                {
                    _clearDataOnExit = clearExit;
                }

                // Font Size
                string? fontStr = await DatabaseContext.Instance.GetSecureSettingAsync("FontSize");
                if (!string.IsNullOrWhiteSpace(fontStr))
                {
                    _selectedFontSize = fontStr == "Средний" ? "Средний (рекомендуется)" : fontStr;
                }

                // Page Zoom
                string? zoomStr = await DatabaseContext.Instance.GetSecureSettingAsync("PageZoom");
                if (!string.IsNullOrWhiteSpace(zoomStr) && double.TryParse(zoomStr.Replace("%", "").Trim(), NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double percent))
                {
                    _defaultZoomFactor = percent / 100.0;
                }

                // MRU Tabs
                string? mruStr = await DatabaseContext.Instance.GetSecureSettingAsync("CycleTabsMru");
                if (bool.TryParse(mruStr, out bool mru))
                {
                    _cycleTabsMru = mru;
                }

                // Wayback Machine
                string? waybackStr = await DatabaseContext.Instance.GetSecureSettingAsync("WaybackMachine");
                if (bool.TryParse(waybackStr, out bool wb))
                {
                    _waybackMachineEnabled = wb;
                }

                // Speedreader
                string? speedStr = await DatabaseContext.Instance.GetSecureSettingAsync("Speedreader");
                if (bool.TryParse(speedStr, out bool sp))
                {
                    _speedreaderEnabled = sp;
                }

                // Download Folder
                string? dlStr = await DatabaseContext.Instance.GetSecureSettingAsync("DownloadFolderPath");
                if (!string.IsNullOrWhiteSpace(dlStr))
                {
                    _downloadFolderPath = dlStr;
                }

                // Ask Where To Save
                string? askDlStr = await DatabaseContext.Instance.GetSecureSettingAsync("AskWhereToSave");
                if (bool.TryParse(askDlStr, out bool askDl))
                {
                    _askWhereToSave = askDl;
                }

                // Notify Download Completed
                string? notifyDlStr = await DatabaseContext.Instance.GetSecureSettingAsync("NotifyDownloadCompleted");
                if (bool.TryParse(notifyDlStr, out bool notDl))
                {
                    _notifyDownloadCompleted = notDl;
                }

                // Search Engine
                string? engineStr = await DatabaseContext.Instance.GetSecureSettingAsync("SearchEngine");
                if (Enum.TryParse<SearchEngineType>(engineStr, out var engine))
                {
                    _searchEngine = engine;
                    NavigationHelper.CurrentSearchEngine = engine;
                }

                // Hardware Acceleration
                string? hwAccStr = await DatabaseContext.Instance.GetSecureSettingAsync("HardwareAcceleration");
                if (bool.TryParse(hwAccStr, out bool hwAcc))
                {
                    _hardwareAcceleration = hwAcc;
                }

                // Sleeping Tabs
                string? sleepStr = await DatabaseContext.Instance.GetSecureSettingAsync("SleepingTabs");
                if (bool.TryParse(sleepStr, out bool slp))
                {
                    _sleepingTabs = slp;
                }

                // Incognito Autofill
                string? incognitoStr = await DatabaseContext.Instance.GetSecureSettingAsync("IncognitoAutofill");
                if (bool.TryParse(incognitoStr, out bool incAuto))
                {
                    _incognitoAutofill = incAuto;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }

        #endregion
    }
}
