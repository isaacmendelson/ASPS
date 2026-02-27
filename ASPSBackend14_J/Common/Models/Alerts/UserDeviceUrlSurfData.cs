using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models.Alerts
{
    public class UserDeviceUrlSurfData
    {
        public UserDeviceUrlSurfData(Key userKey, string url, string deviceUid, MessagingApp messagingApp, 
            RiskAssessment? riskAssessment, IEnumerable<SurfHistoryItem>? surfHistory)
        {
            UserKey = userKey;
            Url = url;
            DeviceUid = deviceUid;
            MessagingApp = messagingApp;
            RiskAssessment = riskAssessment;
            SurfHistory = surfHistory?? new List<SurfHistoryItem>();
            CreatedAt = DateTime.UtcNow;
        }

        public Key UserKey { get; set; }
        public string Url { get; set; }
        public string DeviceUid { get; set; }
        public DateTime CreatedAt { get; private set; }
        //public DateTime? ReceivedAt { get; set; }
        public MessagingApp MessagingApp { get; set; }
        public RiskAssessment? RiskAssessment { get; set; }
        public IEnumerable<SurfHistoryItem> SurfHistory { get; set; }
        public int ViewHistoryCount { get
            {
                return SurfHistory.Count();
            }
        } 
        public string Domain         {
            get
            {
                try
                {
                    var uri = new Uri(Url);
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
