using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using NetTopologySuite.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Business.RealtimeAnalysis.ProtectivActions
{
    public class ProtectiveActionsFactory : IProtectiveActionsFactory
    {

        public IProtectiveAction[] CreateProtectiveActions(AnalysisResult analysisResult, AnalyzerResult analyzerResult, string alertId, DeviceInfo deviceInfo, float trackUrlThreshold)
        {
            List<IProtectiveAction> protectiveActions = new List<IProtectiveAction>();
            int trackingDurationMinutes = 300;
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

                    // Enable URL tracking if risk score exceeds threshold (higher score = higher risk)
                    // Risk score of 40 or above means elevated risk, enable tracking

                    if (urlAnalysisResult.risk_assessment?.risk_score >= trackUrlThreshold)
                    {
                        var domain = Common.Entities.KnownPhishingWebsite.GetDomainFromUrl(urlAnalysisResult.Url);
                        //_logger.LogInformation($"⚠️ Risk threshold exceeded for {domain}. Risk score: {urlAnalysisResult.risk_assessment.risk_score} >= {trackUrlThreshold}. Enabling URL tracking for {0} minutes.");

                        var trackingAction = new ProtectiveAction(
                            deviceInfo.Key,
                            ProtectiveActionType.EnableUrlTracking,
                            AnalysisLevel.Device,
                            $"EnableUrlTracking|{domain}|{trackingDurationMinutes}",
                            alertId);
                        protectiveActions.Add(trackingAction);
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
