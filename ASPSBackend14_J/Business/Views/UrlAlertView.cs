#nullable enable

using Common.Entities;
using Common.Enums;
using Common.Models;
using Newtonsoft.Json;


namespace Business.Views
{
    public class UrlAlertView: DeviceAlertView
    {
        protected UrlAlertView() { }
        public UrlAlertView(UrlAlertEntity entity)
            : base(entity)
        {
            this.Url = entity.Url ?? string.Empty;
            this.TrackerKeys = string.IsNullOrEmpty(entity.TrackerKeys) 
                ? Array.Empty<Key>() 
                : JsonConvert.DeserializeObject<Key[]>(entity.TrackerKeys) ?? Array.Empty<Key>();
            this.IFrameDomains = string.IsNullOrEmpty(entity.IFrameDomains) 
                ? Array.Empty<string>() 
                : entity.IFrameDomains.Split(",");
            this.UserAgent = entity.UserAgent ?? string.Empty;
            this.AlertId = entity.AlertId;
        }

        public UrlAlertView(
            Key key, string? alertId, string alertType, Priority priority,
            DateTime timestamp, string token, string deviceUid,
            DeviceType deviceType, OperatingSystemType operatingSystem, string? mAC, Key? userKey,
            string url, Key[] trackerKeys, string[] iFrameDomains, string userAgent)
            : base(key,alertId,alertType,priority, timestamp,token, deviceUid,deviceType, operatingSystem, mAC,userKey)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            TrackerKeys = trackerKeys ?? throw new ArgumentNullException(nameof(trackerKeys));
            IFrameDomains = iFrameDomains ?? throw new ArgumentNullException(nameof(iFrameDomains));
            UserAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
        }

        public string Url { get; private set; }
        public Key[] TrackerKeys { get; private set; }
        public string[] IFrameDomains { get; private set; }
        public string UserAgent { get; private set; }


        public string Domain
        {
            get
            {
                try
                {
                    var uri = new Uri(this.Url);
                    return uri.Host;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}
