using System;
using System.Text.RegularExpressions;

namespace ParadoxusBrowser.Core
{
    public enum SearchEngineType
    {
        Yandex,
        Google,
        DuckDuckGo,
        Bing
    }

    /// <summary>
    /// Helps parse address bar input into either a direct URL or a search query using the configured search engine,
    /// formats domain names for Shields and security indicators, and identifies Hub (New Tab) URLs.
    /// </summary>
    public static class NavigationHelper
    {
        public const string HubUrl = "paradoxus://newtab";
        public const string SettingsUrl = "paradoxus://settings";
        public const string AboutBlank = "about:blank";

        public static SearchEngineType CurrentSearchEngine { get; set; } = SearchEngineType.Yandex;

        private static readonly Regex DomainRegex = new(
            @"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}(?::\d+)?(?:/.*)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LocalhostRegex = new(
            @"^(localhost|127\.0\.0\.1|0\.0\.0\.0)(?::\d+)?(?:/.*)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string GetSearchUrlTemplate(SearchEngineType engine)
        {
            return engine switch
            {
                SearchEngineType.Yandex => "https://ya.ru/search/?text={0}",
                SearchEngineType.Google => "https://www.google.com/search?q={0}",
                SearchEngineType.Bing => "https://www.bing.com/search?q={0}",
                _ => "https://duckduckgo.com/?q={0}",
            };
        }

        public static string GetSearchUrl(string query)
        {
            string template = GetSearchUrlTemplate(CurrentSearchEngine);
            return string.Format(template, Uri.EscapeDataString(query));
        }

        public static string GetSearchEngineName(SearchEngineType engine)
        {
            return engine switch
            {
                SearchEngineType.Yandex => "Яндекс",
                SearchEngineType.Google => "Google",
                SearchEngineType.Bing => "Bing",
                _ => "DuckDuckGo",
            };
        }

        /// <summary>
        /// Determines if the URL represents the New Tab Hub ("Табло").
        /// </summary>
        public static bool IsHubUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            string clean = url.Trim().ToLowerInvariant();
            return clean == HubUrl || clean == "brave://newtab" || clean == AboutBlank || clean == "about:tabs" || clean == "about:home";
        }

        public static bool IsSettingsUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            string clean = url.Trim().ToLowerInvariant();
            return clean == SettingsUrl || clean == "brave://settings";
        }

        /// <summary>
        /// Converts user input into a target navigation URL.
        /// </summary>
        public static string ProcessInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return HubUrl;

            string query = input.Trim();

            // Internal Paradoxus & browser protocols must not be routed to search engines
            if (query.StartsWith("paradoxus://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("brave://", StringComparison.OrdinalIgnoreCase))
            {
                if (IsHubUrl(query))
                    return HubUrl;
                if (IsSettingsUrl(query))
                    return SettingsUrl;
                return query;
            }

            // Check if already well-formed URI
            if (Uri.TryCreate(query, UriKind.Absolute, out Uri? uriResult))
            {
                if (uriResult.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    uriResult.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    uriResult.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase) ||
                    uriResult.Scheme.Equals("paradoxus", StringComparison.OrdinalIgnoreCase) ||
                    uriResult.Scheme.Equals("brave", StringComparison.OrdinalIgnoreCase))
                {
                    return uriResult.AbsoluteUri;
                }
            }

            // Check if user entered domain or localhost without scheme
            if (!query.Contains(' ') && (DomainRegex.IsMatch(query) || LocalhostRegex.IsMatch(query)))
            {
                if (LocalhostRegex.IsMatch(query))
                    return "http://" + query;

                return "https://" + query;
            }

            // Otherwise, fallback to configured search engine
            string template = GetSearchUrlTemplate(CurrentSearchEngine);
            return string.Format(template, Uri.EscapeDataString(query));
        }

        /// <summary>
        /// Determines if the given input is a search query rather than a direct URL or domain.
        /// </summary>
        public static bool IsSearchQuery(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string query = input.Trim();
            if (query.StartsWith("paradoxus://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("brave://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (!query.Contains(' ') && (DomainRegex.IsMatch(query) || LocalhostRegex.IsMatch(query)))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Extracts clean domain name (e.g., "github.com") from a URL.
        /// </summary>
        public static string ExtractDomain(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || IsHubUrl(url))
                return "Табло (Новая вкладка)";

            if (IsSettingsUrl(url))
                return "Настройки";

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return string.IsNullOrEmpty(uri.Host) ? url : uri.Host;
            }

            return url;
        }

        /// <summary>
        /// Returns whether the specified URL uses secure HTTPS.
        /// </summary>
        public static bool IsHttps(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || IsHubUrl(url) || IsSettingsUrl(url))
                return true;

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
