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

        public IProtectiveAction[] CreateProtectiveActions(ImmediateDangerDto immediateDangerDto)
        {
            List<IProtectiveAction> protectiveActions = new List<IProtectiveAction>();

            switch (immediateDangerDto)
            {
                case ImmediateDangerByRemoteAccessDto immediateDangerByRemoteAccessDto:

                    // ProtectiveActions for the reporting device:
                    var msg = $"DANGER! DO NOT PROCEED! Your sensitive data online is accessible by remote pary using remote control!\nClose any remote access application running on this device.\nLogout from any website you are logged to.";

                    // Subject key for the actions: prefer the explicit DeviceKey
                    // from the DTO. If it's missing (UserDevices cache not yet
                    // loaded, Device navigation property not fetched, …), fall
                    // back to a synthetic Key from DeviceUid so we still emit
                    // the actions — without this fallback the entire array
                    // came back empty and the agent never showed a toast.
                    Key subjectKey = immediateDangerByRemoteAccessDto.DeviceKey
                        ?? (!string.IsNullOrEmpty(immediateDangerByRemoteAccessDto.DeviceUid)
                                ? new Key(nameof(Common.Entities.UserDevice), immediateDangerByRemoteAccessDto.DeviceUid)
                                : null);

                    if (subjectKey is not null)
                    {
                        var alertId = immediateDangerByRemoteAccessDto.DeviceAlertKey?.Value;
                        protectiveActions.Add(new ProtectiveAction(subjectKey, ProtectiveActionType.DisplayNotification, AnalysisLevel.User, msg, alertId));
                        protectiveActions.Add(new ProtectiveAction(subjectKey, ProtectiveActionType.SoundAlert,         AnalysisLevel.User, msg, alertId));
                        protectiveActions.Add(new ProtectiveAction(subjectKey, ProtectiveActionType.BlockRemoteAccess,  AnalysisLevel.User, msg, alertId));
                    }


                    break;
            }

            return protectiveActions.ToArray();
        }

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

                    break;
            }

            
            return protectiveActions.ToArray();
        }
    }
}
