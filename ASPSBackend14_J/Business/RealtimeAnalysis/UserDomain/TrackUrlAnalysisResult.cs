using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.UserDomain
{
    /// <summary>
    /// Analysis result for TrackUrlAlert
    /// </summary>
    [Serializable]
    [DataContract]
    public class TrackUrlAnalysisResult : AnalysisResult
    {
        public TrackUrlAnalysisResult(
            string url,
            string fromUrl,
            int duration,
            string scamInProgressKey,
            string ipAddress,
            string userAgent,
            string tabId,
            string timezone,
            string domain,
            bool isSafeDomain,
            bool isFromCache,
            RiskAssessment? riskAssessment = null,
            TrackedDomainInfo? trackedDomain = null)
        {
            Url = url;
            FromUrl = fromUrl;
            Duration = duration;
            ScamInProgressKey = scamInProgressKey;
            IPAddress = ipAddress;
            UserAgent = userAgent;
            TabId = tabId;
            Timezone = timezone;
            Domain = domain;
            IsSafeDomain = isSafeDomain;
            IsFromCache = isFromCache;
            risk_assessment = riskAssessment;
            TrackedDomain = trackedDomain;
            this.analyzed_at = DateTime.UtcNow;
        }
        public TrackUrlAnalysisResult(TrackUrlAlert alert,
           bool isFromCache,
           RiskAssessment? riskAssessment,
           TrackedDomainInfo? trackedDomain = null)
        {
            Url = alert.Url;
            FromUrl = alert.FromUrl;
            Duration = alert.Duration;
            ScamInProgressKey = alert.ScamInProgressKey;
            IPAddress = alert.IPAddress;
            UserAgent = alert.UserAgent;
            TabId = alert.TabId;
            Timezone = alert.Timezone;
            Domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(alert.Url);
            IsSafeDomain = false;
            IsFromCache = isFromCache;
            risk_assessment = riskAssessment;
            TrackedDomain = trackedDomain;
            this.analyzed_at = DateTime.UtcNow;
        }
        protected TrackUrlAnalysisResult() { }

        [DataMember]
        public string TypeName => "TrackUrlAnalysisResult";

        [DataMember]
        public string Url { get; set; } = string.Empty;

        [DataMember]
        public string FromUrl { get; set; } = string.Empty;

        [DataMember]
        public int Duration { get; set; }

        [DataMember]
        public string ScamInProgressKey { get; set; } = string.Empty;

        [DataMember]
        public string IPAddress { get; set; } = string.Empty;

        [DataMember]
        public string UserAgent { get; set; } = string.Empty;

        [DataMember]
        public string TabId { get; set; } = string.Empty;

        [DataMember]
        public string Timezone { get; set; } = string.Empty;

        [DataMember]
        public string Domain { get; set; } = string.Empty;

        [DataMember]
        public bool IsSafeDomain { get; set; }

        [DataMember]
        public bool IsFromCache { get; set; } = false;

        [DataMember]
        public RiskAssessment? risk_assessment { get; set; }

        /// <summary>
        /// Information about matched TrackedDomain (null if no match)
        /// </summary>
        [DataMember]
        public TrackedDomainInfo? TrackedDomain { get; set; }
    }
}
