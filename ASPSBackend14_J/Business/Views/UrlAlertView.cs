using Common.Entities;
using Common.Enums;
using Common.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class UrlAlertView: DeviceAlertView
    {
        protected UrlAlertView() { }
        public UrlAlertView(UrlAlertEntity entity)
            : base(entity)
        {
            this.Url = entity.Url;
            this.TrackerKeys = JsonConvert.DeserializeObject<Key[]>(entity.TrackerKeys);
            this.IFrameDomains = entity.IFrameDomains.Split(",");
            this.UserAgent = entity.UserAgent;
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
