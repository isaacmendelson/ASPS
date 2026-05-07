using Newtonsoft.Json;
using System;

namespace Common.Models.Alerts
{
    public class BrowserTab
    {
        // Public parameterless ctor — Newtonsoft prefers it; properties are
        // populated by setters after construction.
        public BrowserTab() { }

        public BrowserTab(string title, string tabId, string userAgent, string url, DateTime? timestamp, bool isActive)
        {
            Title = title;
            TabId = tabId;
            UserAgent = userAgent;
            Url = url;
            Timestamp = timestamp;
            IsActive = isActive;
        }

        // Full ctor including the v0.0.1.0 logged-in fields. Optional so existing
        // call sites continue to compile.
        public BrowserTab(
            string title, string tabId, string userAgent, string url,
            DateTime? timestamp, bool isActive,
            bool? loggedIn, string? loggedInConfidence, string[]? loggedInSignals)
            : this(title, tabId, userAgent, url, timestamp, isActive)
        {
            LoggedIn = loggedIn;
            LoggedInConfidence = loggedInConfidence;
            LoggedInSignals = loggedInSignals;
        }

        // [JsonProperty] tags are added explicitly so deserialization succeeds
        // regardless of the agent / extension casing (camelCase vs PascalCase).
        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string? Title { get; set; }

        [JsonProperty("tabId", NullValueHandling = NullValueHandling.Ignore)]
        public string? TabId { get; set; }

        [JsonProperty("userAgent", NullValueHandling = NullValueHandling.Ignore)]
        public string? UserAgent { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string? Url { get; set; }

        [JsonProperty("timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Timestamp { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        // ─── v0.0.1.0 logged-in detection (from Chrome extension) ────────
        // The extension combines two signals:
        //   - chrome.cookies API: presence of session-like cookies for the URL
        //   - DOM scan: logout/sign-out element matched (en/he/fr/ru regex)
        //
        // Tri-state: true / false / null. Null means "unknown" — neither signal
        // could be evaluated (e.g., content script not yet injected, or chrome
        // restricted URL). Treat null as "unknown" — do NOT default to false.
        [JsonProperty("loggedIn")]
        public bool? LoggedIn { get; set; }

        // 'high' | 'medium' | 'low' | null — confidence level of LoggedIn.
        // 'high'   = both signals agree
        // 'medium' = signals conflict OR only one signal is conclusive
        // 'low'    = one signal says false, the other is unknown
        // null     = no signal could be evaluated
        [JsonProperty("loggedInConfidence")]
        public string? LoggedInConfidence { get; set; }

        // Which signals contributed to the decision: subset of ["cookie", "dom"].
        [JsonProperty("loggedInSignals")]
        public string[]? LoggedInSignals { get; set; }
    }
}
