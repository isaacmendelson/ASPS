using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Alerts
{
    public class BrowserTab
    {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected BrowserTab() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public BrowserTab(string title, string tabId, string userAgent, string url, DateTime? timestamp, bool isActive)
        {
            Title = title;
            TabId = tabId;
            UserAgent = userAgent;
            Url = url;
            Timestamp = timestamp;
            IsActive = isActive;
        }

        public string Title { get; set; }
        public string TabId { get; set; }
        public string UserAgent { get; set; }
        public string Url { get; set; }
        public DateTime? Timestamp { get; set; }
        public bool IsActive { get; set; }

        // Logged-in detection results from the Chrome extension (extension v0.0.1.0+).
        // The extension combines two signals:
        //   - chrome.cookies API: presence of session-like cookies for the URL
        //   - DOM scan: logout/sign-out element matched (en/he/fr/ru regex)
        //
        // Tri-state: true / false / null. Null means "unknown" — neither signal
        // could be evaluated (e.g., content script not yet injected, or chrome
        // restricted URL). Treat null as "unknown" — do NOT default to false.
        public bool? LoggedIn { get; set; }

        // 'high' | 'medium' | 'low' | null — confidence level of LoggedIn.
        // 'high'   = both signals agree
        // 'medium' = signals conflict OR only one signal is conclusive
        // 'low'    = one signal says false, the other is unknown
        // null     = no signal could be evaluated
        public string? LoggedInConfidence { get; set; }

        // Which signals contributed to the decision: subset of ["cookie", "dom"].
        public string[]? LoggedInSignals { get; set; }
    }
}
