using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.Messaging
{
    public class AnalysisResultNotification
    {
        public AnalysisResultNotification(string alertType, string severity, RiskAssessmentVm riskAssessment,
            IAnalysisResult analysisResult, List<IProtectiveAction> protectiveActions, List<IIndicator> indicators, DateTime analysisTimestamp)
        {
            this.AlertType = alertType;
            this.Severity = severity;
            this.AnalysisTimestamp = analysisTimestamp;
            this.RiskAssessment = riskAssessment;
            this.protectiveActions = protectiveActions;
            this.AnalysisResult = analysisResult;
            this.Indicators = indicators;
        }


        protected AnalysisResultNotification()
        {

        }

        [DataMember]
        public string TypeName
        {
            get { return "AnalysisResultNotification"; }
        }
        //public override string TypeName => "UrlAnalysisResult";

        [DataMember]
        public string AlertType { get; set; }

        [DataMember]
        public RiskAssessmentVm RiskAssessment { get; set; }

        [DataMember]
        public List<IProtectiveAction> protectiveActions { get; set; }

        [DataMember]
        public List<IIndicator> Indicators { get; set; }

        [DataMember]
        public IAnalysisResult AnalysisResult { get; set; }


        [DataMember]
        public string Severity { get; set; }
        //[DataMember] 
        //public string Message { get; set; }

        //[DataMember] 
        //public Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>> AnalyzerResults { get; set; }
        //[DataMember] 
        //public Dictionary<string, object> Details { get; set; }

        [DataMember]
        public string DeviceAlertKey { get; set; }
        [DataMember]
        public DateTime AnalysisTimestamp { get; set; }

    }
}
