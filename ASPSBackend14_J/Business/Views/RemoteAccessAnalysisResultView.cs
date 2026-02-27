using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class RemoteAccessAnalysisResultView : AnalysisResultView
    {
        public RemoteAccessAnalysisResultView(AnalysisResultContainer analysisResultContainer)
            : base(analysisResultContainer)
        {
            var json = analysisResultContainer.JsonValue ?? string.Empty;
            var jObject = Newtonsoft.Json.Linq.JObject.Parse(json);
            var analyzerResults = jObject["AnalyzerResults"] ?? jObject["analyzerResults"];
            if (analyzerResults is null)
            {
                analyzerResults = jObject["AnalysisResult"];
            }

            if (analyzerResults != null)
            {
                // The RemoteAccess JSON wraps data in AnalyzerResults.AnalysisResult
                // (unlike URL which serializes the VM directly)
                var innerResult = analyzerResults["AnalysisResult"];
                if (innerResult != null)
                {
                    AnalysisResult = innerResult.ToObject<RemoteAccessAnalysisResultVm>();
                }
                else
                {
                    AnalysisResult = analyzerResults.ToObject<RemoteAccessAnalysisResultVm>();
                }
                // Set the base class property so polymorphic access works
                ((AnalysisResultView)this).AnalysisResult = AnalysisResult;

                // Indicators and ProtectiveActions are inside the AnalyzerResults wrapper
                var ind = analyzerResults["Indicators"] ?? jObject["Indicators"];
                if (ind is not null)
                {
                    this.Indicators = ind.ToObject<Indicator[]>();
                }
                var pa = analyzerResults["ProtectiveActions"] ?? jObject["ProtectiveActions"];
                if (pa is not null)
                {
                    this.ProtectiveActions = pa.ToObject<ProtectiveAction[]>();
                }
            }

            // Reconstruct Alert from parsed data so consumers (cache lookup, admin pages) can access it
            if (AnalysisResult != null)
            {
                var deviceUid = jObject["DeviceUid"]?.ToString() ?? string.Empty;
                Alert = new RemoteAccessAlert(
                    AnalysisResult.RemoteAccessApp,
                    AnalysisResult.RunningProcesses,
                    AnalysisResult.ConnectionUrl,
                    AnalysisResult.ConnectionStatus,
                    AnalysisResult.ConnectionsCount,
                    AnalysisResult.SessionStatus,
                    AnalysisResult.BrowserTabs)
                {
                    AlertType = nameof(RemoteAccessAlert),
                    AlertId = this.DeviceAlertKey?.Value,
                    DeviceInfo = new DeviceInfo { DeviceUid = deviceUid }
                };
            }
        }

        public new RemoteAccessAnalysisResultVm? AnalysisResult { get; set; }
    }
}
