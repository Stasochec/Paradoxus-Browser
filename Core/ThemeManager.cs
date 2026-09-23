using System;
using System.Linq;
using System.Windows;
using ParadoxusBrowser.Data;

namespace ParadoxusBrowser.Core
{
    public enum AppTheme
    {
        ParadoxusDark,
        BraveDark = ParadoxusDark,
        OledMidnight,
        LightMinimal
    }

    public static class ThemeManager
    {
        public static AppTheme CurrentTheme { get; private set; } = AppTheme.ParadoxusDark;

        public static void ApplyTheme(AppTheme theme)
        {
            CurrentTheme = theme;

            string themeFile = theme switch
            {
                AppTheme.OledMidnight => "Styles/Themes/OledMidnightTheme.xaml",
                AppTheme.LightMinimal => "Styles/Themes/LightMinimalTheme.xaml",
                _ => "Styles/Themes/ParadoxusDarkTheme.xaml"
            };

            try
            {
                var newDict = new ResourceDictionary
                {
                    Source = new Uri(themeFile, UriKind.RelativeOrAbsolute)
                };

                var appResources = Application.Current.Resources;
                var merged = appResources.MergedDictionaries;

                // Find existing theme dictionary
                var existingThemeDict = merged.FirstOrDefault(d =>
                    d.Source != null && (
                        d.Source.OriginalString.Contains("ParadoxusDarkTheme.xaml") ||
                        d.Source.OriginalString.Contains("BraveDarkTheme.xaml") ||
                        d.Source.OriginalString.Contains("OledMidnightTheme.xaml") ||
                        d.Source.OriginalString.Contains("LightMinimalTheme.xaml") ||
                        d.Source.OriginalString.Contains("Colors.xaml")
                    ));

                if (existingThemeDict != null)
                {
                    int index = merged.IndexOf(existingThemeDict);
                    merged.RemoveAt(index);
                    merged.Insert(index, newDict);
                }
                else
                {
                    merged.Insert(0, newDict);
                }

                // Save selected theme in database
                _ = DatabaseContext.Instance.SetSecureSettingAsync("SelectedTheme", theme.ToString());

                // Notify subscribers (e.g. TabViewModel for prefers-color-scheme)
                ThemeChanged?.Invoke(theme);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme {theme}: {ex.Message}");
            }
        }

        public static event Action<AppTheme>? ThemeChanged;
    }
}
