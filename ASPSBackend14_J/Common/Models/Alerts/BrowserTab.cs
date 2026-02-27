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

        public BrowserTab(string title, string userAgent, string url, DateTime? timestamp, bool isActive)
        {
            Title = title;
            UserAgent = userAgent;
            Url = url;
            Timestamp = timestamp;
            IsActive = isActive;
        }



        public string Title { get; set; }
        public string UserAgent { get; set; }
        public string Url { get; set; }
        public DateTime? Timestamp { get; set; }
        public bool IsActive { get; set; }
        
    }
}
