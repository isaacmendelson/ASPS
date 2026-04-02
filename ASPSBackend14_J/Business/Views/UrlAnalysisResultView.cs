using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Models;
using Common.Models.Alerts;

namespace Business.Views
{
    public class UrlAnalysisResultView : AnalysisResultView
    {
        public UrlAnalysisResultView(AnalysisResultContainer analysisResultContainer)
            : base(analysisResultContainer)
        {
            this.Timestamp = analysisResultContainer.Timestamp;


            var json = analysisResultContainer.JsonValue ?? string.Empty;
            var jObject = Newtonsoft.Json.Linq.JObject.Parse(json);
            var analyzerResults = jObject["AnalyzerResults"] ?? jObject["analyzerResults"];
            if (analyzerResults is null)
            {
                analyzerResults = jObject["AnalysisResult"];
            }
            var ind = jObject["Indicators"];
            if (ind is not null)
            {
                var indX = ind.ToObject<Indicator[]>();
                this.Indicators = indX;
            }
            var pa = jObject["ProtectiveActions"];
            if (pa is not null)
            {
                var paX = pa.ToObject<ProtectiveAction[]>();
                this.ProtectiveActions = paX;
            }
            if (analyzerResults != null)
            {
                this.AnalysisResult = analyzerResults.ToObject<UrlAnalysisResult>();
                if (this.AnalysisResult is not null)
                {
                    this.AnalysisResult.analyzed_at = analysisResultContainer.Timestamp;
                }

                // Set the base class property so polymorphic access works
                ((AnalysisResultView)this).AnalysisResult = AnalysisResult;
            }

            // Reconstruct Alert from parsed data so consumers (cache lookup, admin pages) can access it
            if (AnalysisResult != null && !string.IsNullOrEmpty(AnalysisResult.Url))
            {
                var deviceUid = jObject["DeviceUid"]?.ToString() ?? string.Empty;
                Alert = new UrlAlert
                {
                    
                    Url = AnalysisResult.Url,
                    Key = analysisResultContainer?.DeviceAlertKey,
                    AlertType = nameof(UrlAlert),
                    AlertId = this.DeviceAlertKey?.Value,
                    Trackers = Array.Empty<Key>(),
                    IFrameDomains = Array.Empty<string>(),
                    UserAgent = string.Empty,
                    DeviceInfo = new DeviceInfo { DeviceUid = deviceUid, UserKey = analysisResultContainer?.UserKey }
                };
            }
        }

        public UrlAnalysisResult? AnalysisResult { get; set; }
    }
}
