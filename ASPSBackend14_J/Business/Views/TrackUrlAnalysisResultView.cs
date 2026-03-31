using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class TrackUrlAnalysisResultView : AnalysisResultView
    {
        public TrackUrlAnalysisResultView(AnalysisResultContainer analysisResultContainer)
        : base(analysisResultContainer)
        {
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
                AnalysisResult = analyzerResults.ToObject<TrackUrlAnalysisResult>();
                // Set the base class property so polymorphic access works
                ((AnalysisResultView)this).AnalysisResult = AnalysisResult;
            }

        } 
        public new TrackUrlAnalysisResult AnalysisResult { get; set; }
    }
}
