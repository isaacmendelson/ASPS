#nullable enable

using Common.Entities;
using Common.Enums;
using Common.Models;
using Newtonsoft.Json;

namespace Business.Views
{
    public class TrackUrlAlertView : DeviceAlertView
    {
        protected TrackUrlAlertView() { }

        public TrackUrlAlertView(TrackUrlAlertEntity entity)
            :base(entity)
        {
            Url = entity.Url;
            FromUrl = entity.FromUrl;
            Duration = entity.Duration;
            ScamInProgressKey = entity.ScamInProgressKey;
            IPAddress = entity.IPAddress;
            UserAgent = entity.UserAgent;
            TabId = entity.TabId;
            Timezone = entity.Timezone;
        }

        public TrackUrlAlertView(
            Key key, string? alertId, string alertType, Priority priority,
            DateTime timestamp, string token, string deviceUid,
            DeviceType deviceType, OperatingSystemType operatingSystem, string? mAC, Key? userKey, string url, string fromUrl, int duration, string? scamInProgressKey, string iPAddress, string userAgent, string tabId, string timezone)
            : base(key, alertId, alertType, priority, timestamp, token, deviceUid, deviceType, operatingSystem, mAC, userKey)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            FromUrl = fromUrl ?? throw new ArgumentNullException(nameof(fromUrl));
            UserAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
            Url = url;
            Duration = duration;
            ScamInProgressKey = scamInProgressKey;
            IPAddress = iPAddress;
            TabId = tabId;
            Timezone = timezone;
        }

        /// <summary>
        /// The current URL being visited
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The previous URL (referrer)
        /// </summary>
        public string FromUrl { get; set; } = string.Empty;

        /// <summary>
        /// Duration spent on the page in seconds
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Key for identifying scam-in-progress scenarios
        /// </summary>
        public string? ScamInProgressKey { get; set; } = string.Empty;

        /// <summary>
        /// IP address of the request
        /// </summary>
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>
        /// User agent string from the browser
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Browser tab identifier
        /// </summary>
        public string TabId { get; set; } = string.Empty;

        /// <summary>
        /// User's timezone
        /// </summary>
        public string Timezone { get; set; } = string.Empty;

    }
}
