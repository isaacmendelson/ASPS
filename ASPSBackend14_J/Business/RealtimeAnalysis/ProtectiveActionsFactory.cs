using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Models;
using NetTopologySuite.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Business.RealtimeAnalysis
{
    public class ProtectiveActionsFactory : IProtectiveActionsFactory
    {

        public IProtectiveAction[] CreateProtectiveActions(AnalysisResult analysisResult, AnalyzerResult analyzerResult, string alertId, DeviceInfo deviceInfo)
        {
            List<IProtectiveAction> protectiveActions = new List<IProtectiveAction>();

            string msg = "";
            switch (analysisResult)
            {
                case UrlAnalysisResult urlAnalysisResult:
                    var phishingIndicator = analyzerResult.Indicators?.FirstOrDefault(i => i is KnownPhishingIndicator) as KnownPhishingIndicator;
                    if (phishingIndicator is not null)
                    {
                        //msg = $"Known phishing detected: {phishingIndicator.Url}";
                        msg = $"Known phishing detected: {phishingIndicator?.Url} Phishing source: {phishingIndicator.PhishingSource}, IsKnownPhishing: {phishingIndicator.IsKnownPhishing}  Score:{phishingIndicator.Score} Level: {phishingIndicator.Level} Name:{phishingIndicator.Name} Time: {phishingIndicator.timestamp} Confidence: {phishingIndicator.Confidence} Source: {phishingIndicator.Source}";
                        var action = new ProtectiveAction(deviceInfo.Key, ProtectiveActionType.UserDisplayNotification, AnalysisLevel.Device, msg, alertId);
                        protectiveActions.Add(action);
                    }

                    msg = "test message";
                    var a1 = new ProtectiveAction(deviceInfo.Key, ProtectiveActionType.DisplayNotification, AnalysisLevel.Device, msg, null);
                    var a2 = new ProtectiveAction(deviceInfo.Key, ProtectiveActionType.SoundAlert, AnalysisLevel.Device, msg, null);
                    var a3 = new ProtectiveAction(deviceInfo.UserKey!, ProtectiveActionType.EmailNotification, AnalysisLevel.Device, msg, null);

                    protectiveActions.AddRange(new IProtectiveAction[] { a1, a2, a3 });

                    break;
            }

            
            return protectiveActions.ToArray();
        }
    }
}
