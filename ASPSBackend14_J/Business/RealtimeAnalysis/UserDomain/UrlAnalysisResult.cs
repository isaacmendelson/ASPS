using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.UserDomain
{
    [Serializable]
    [DataContract]
    public class UrlAnalysisResult : AnalysisResult, IAnalysisResult
    {
        public UrlAnalysisResult() { }

        [DataMember]
        public string TypeName => "UrlAnalysisResult";

        [DataMember]
        public string Url { get; set; } = string.Empty;

        [DataMember]
        public string Domain { get; set; } = string.Empty;


        [DataMember]
        public int analysis_time_ms { get; set; }

        [DataMember]
        public bool IsFromCache { get; set; } = false;

        [DataMember]
        public bool? IsWhitelisted { get; set; } = false;

        [DataMember]
        public Purpose? Purpose { get; set; }

        [DataMember]
        public WebsiteCategoryResult? WebsiteCategory { get; set; }


        [DataMember]
        public WhoisVm? Whois { get; set; }

        [DataMember]
        public ContentAnalysisVm? content_analysis { get; set; }

        [DataMember]
        public MlAnalysis? ml_analysis { get; set; }

        [DataMember]
        public PhishingCheckResultVm? phishing_check { get; set; }

        [DataMember]
        public string[] red_flags { get; set; } = Array.Empty<string>();

        [DataMember]
        public string? Recommendation { get; set; }

        [DataMember]
        public ScrapingStatusVm? scraping_status { get; set; } = new ScrapingStatusVm();

        [DataMember]
        public RiskAssessment? risk_assessment { get; set; }

        [DataMember]
        public WebsiteCategoryResult? website_category { get; set; }

        [DataMember]
        public Reputation? Reputation { get; set; }

        [DataMember]
        public string[]? Warnings { get; set; }

        [DataMember]
        public string[]? missing_data { get; set; }


    }
}
