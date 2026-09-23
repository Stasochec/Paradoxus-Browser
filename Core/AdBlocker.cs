using System;
using System.Collections.Generic;
using System.Linq;

namespace ParadoxusBrowser.Core
{
    /// <summary>
    /// Core Ad and Tracker blocker engine that inspects requested resources
    /// against known advertising, tracking, and telemetry patterns.
    /// </summary>
    public class AdBlocker
    {
        private static readonly Lazy<AdBlocker> _instance = new(() => new AdBlocker());
        public static AdBlocker Instance => _instance.Value;

        private readonly HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _urlKeywords = new();
        private readonly object _lock = new();

        public AdBlocker()
        {
            InitializeBlacklist();
        }

        private void InitializeBlacklist()
        {
            // Google Ads & Analytics
            AddBlockedDomain("google-analytics.com");
            AddBlockedDomain("googletagmanager.com");
            AddBlockedDomain("analytics.google.com");
            AddBlockedDomain("doubleclick.net");
            AddBlockedDomain("ad.doubleclick.net");
            AddBlockedDomain("googlesyndication.com");
            AddBlockedDomain("pagead2.googlesyndication.com");
            AddBlockedDomain("adservice.google.com");
            AddBlockedDomain("googleadservices.com");

            // Yandex Metrika & Direct
            AddBlockedDomain("mc.yandex.ru");
            AddBlockedDomain("an.yandex.ru");
            AddBlockedDomain("metrika.yandex.ru");
            AddBlockedDomain("metrika.yandex.com");
            AddBlockedDomain("awaps.yandex.ru");
            AddBlockedDomain("adfox.ru");
            AddBlockedDomain("yabs.yandex.ru");

            // Meta / Facebook Trackers
            AddBlockedDomain("connect.facebook.net");
            AddBlockedDomain("pixel.facebook.com");
            AddBlockedDomain("an.facebook.com");

            // Microsoft Telemetry & Advertising
            AddBlockedDomain("clarity.ms");
            AddBlockedDomain("bat.bing.com");
            AddBlockedDomain("telemetry.microsoft.com");

            // Major Ad Networks & Header Bidding
            AddBlockedDomain("criteo.com");
            AddBlockedDomain("criteo.net");
            AddBlockedDomain("outbrain.com");
            AddBlockedDomain("taboola.com");
            AddBlockedDomain("adnxs.com");
            AddBlockedDomain("advertising.com");
            AddBlockedDomain("rubiconproject.com");
            AddBlockedDomain("pubmatic.com");
            AddBlockedDomain("openx.net");
            AddBlockedDomain("smartadserver.com");
            AddBlockedDomain("adroll.com");
            AddBlockedDomain("casalemedia.com");
            AddBlockedDomain("scorecardresearch.com");
            AddBlockedDomain("quantserve.com");
            AddBlockedDomain("popads.net");
            AddBlockedDomain("popcash.net");
            AddBlockedDomain("zergnet.com");
            AddBlockedDomain("revcontent.com");
            AddBlockedDomain("exponential.com");
            AddBlockedDomain("propellerads.com");
            AddBlockedDomain("mgid.com");
            AddBlockedDomain("yieldmo.com");
            AddBlockedDomain("sharethrough.com");

            // Analytics & Session Recording Trackers
            AddBlockedDomain("hotjar.com");
            AddBlockedDomain("mouseflow.com");
            AddBlockedDomain("mixpanel.com");
            AddBlockedDomain("segment.io");
            AddBlockedDomain("segment.com");
            AddBlockedDomain("amplitude.com");
            AddBlockedDomain("fullstory.com");
            AddBlockedDomain("crazyegg.com");
            AddBlockedDomain("branch.io");
            AddBlockedDomain("appsflyer.com");
            AddBlockedDomain("adjust.com");

            // Path Keywords
            _urlKeywords.AddRange(new[]
            {
                "/ads.js",
                "/adframe.js",
                "/ad-banner.",
                "/adserver/",
                "/advertisement/",
                "/pagead/",
                "/gtm.js",
                "/analytics.js",
                "/yandex_metrika",
                "/watch.js",
                "/telemetry/",
                "/tracker.js",
                "/pixel.js"
            });
        }

        public void AddBlockedDomain(string domain)
        {
            lock (_lock)
            {
                _blockedDomains.Add(domain.Trim().ToLowerInvariant());
            }
        }

        /// <summary>
        /// Evaluates whether the requested resource URI should be blocked according to current AdBlockMode.
        /// </summary>
        public bool ShouldBlock(Uri? uri, string? pageHost, out string ruleTriggered)
        {
            ruleTriggered = string.Empty;

            if (uri == null)
                return false;

            string mode = SettingsManager.Instance.AdBlockMode;
            if (mode.Contains("Отключ") || mode.Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string host = uri.Host.ToLowerInvariant();
            string absoluteUri = uri.AbsoluteUri.ToLowerInvariant();

            lock (_lock)
            {
                // Exact domain match
                if (_blockedDomains.Contains(host))
                {
                    ruleTriggered = $"Domain: {host}";
                    return true;
                }

                // Subdomain matching (e.g. sub.doubleclick.net matches doubleclick.net)
                foreach (var blocked in _blockedDomains)
                {
                    if (host.EndsWith("." + blocked, StringComparison.OrdinalIgnoreCase))
                    {
                        ruleTriggered = $"Subdomain match: {blocked}";
                        return true;
                    }
                }

                // URL Keyword heuristics
                foreach (var keyword in _urlKeywords)
                {
                    if (absoluteUri.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        ruleTriggered = $"Pattern match: {keyword}";
                        return true;
                    }
                }

                // Aggressive mode: block third-party tracking pixels, beacons, analytics and trackers
                if (mode.Contains("Агрессив") || mode.Equals("Aggressive", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(pageHost))
                    {
                        string cleanPageHost = pageHost.ToLowerInvariant().Replace("www.", "");
                        string cleanReqHost = host.Replace("www.", "");

                        bool isThirdParty = !cleanReqHost.EndsWith(cleanPageHost, StringComparison.OrdinalIgnoreCase) &&
                                            !cleanPageHost.EndsWith(cleanReqHost, StringComparison.OrdinalIgnoreCase);

                        if (isThirdParty)
                        {
                            if (absoluteUri.Contains("/pixel") ||
                                absoluteUri.Contains("/beacon") ||
                                absoluteUri.Contains("/collect") ||
                                absoluteUri.Contains("/event") ||
                                absoluteUri.Contains("/track") ||
                                absoluteUri.Contains("/telemetry") ||
                                absoluteUri.Contains("/stats") ||
                                absoluteUri.Contains("/analytic") ||
                                absoluteUri.Contains("/ads") ||
                                absoluteUri.Contains("/tag") ||
                                (absoluteUri.EndsWith(".gif") && (absoluteUri.Contains("1x1") || absoluteUri.Contains("pixel"))))
                            {
                                ruleTriggered = $"Aggressive 3rd-party tracker: {host}";
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public bool ShouldBlock(Uri? uri, out string ruleTriggered)
        {
            return ShouldBlock(uri, null, out ruleTriggered);
        }
    }
}
