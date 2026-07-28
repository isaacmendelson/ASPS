using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Logging;

namespace Business.Messaging;

/// <summary>
/// Listens for AnalysisResultReceived events and publishes notifications to subscribers
/// </summary>
public class NotificationPublisherActor : IDomainEventHandler
{
    private readonly OutboxNotificationPublisher _notificationPublisher;
    private readonly ILogger<NotificationPublisherActor> _logger;
    private readonly ASView _asView;

    public NotificationPublisherActor(
        OutboxNotificationPublisher notificationPublisher,
        ILogger<NotificationPublisherActor> logger,
        ASView asView)
    {
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _asView = asView;
    }

    public async Task Handle(IDomainEvent evt)
    {
        switch (evt)
        {
            case AnalysisResultReceived analysisEvent:
                HandleAnalysisResultReceived(analysisEvent);
                break;
            case ImmediateDangerEvent immediateDangerEvent:
                this.HandleImmediateDangerEvent(immediateDangerEvent);
                break;
            case ImmediateDangerEnded immediateDangerEnded:
                this.HandleImmediateDangerEnded(immediateDangerEnded);
                break;
            case SetTrackedDomains setTrackedDomains:
                this.HandleSetTrackedDomains(setTrackedDomains);
                break;
            default:
                _logger.LogWarning($"[NotificationPublisherActor] Received unhandled event type: {evt.GetType().Name}");
                break;
            }
    }

    public Type[] GetHandleableEvents()
    {
        return new[] { typeof(AnalysisResultReceived), typeof(ImmediateDangerEvent), typeof(ImmediateDangerEnded), typeof(SetTrackedDomains) };
    }

    private void HandleSetTrackedDomains(SetTrackedDomains evt)
    {
        try
        {
            // SetTrackedDomains is per-user (cross-device). The agent only
            // subscribes to "device:{deviceUid}", so resolve every device of
            // the user and fan the notification out to each device topic.
            var deviceUids = new List<string>();
            if (!string.IsNullOrEmpty(evt.UserKeyField))
            {
                var devices = _asView.GetUserDevices(new Key("User", evt.UserKeyField));
                deviceUids = devices
                    .Select(d => d.DeviceUid)
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList();
            }

            if (deviceUids.Count == 0)
            {
                _logger.LogWarning(
                    "[NotificationPublisherActor] SetTrackedDomains for user {UserKey}: no devices resolved — publishing to user topic only",
                    evt.UserKeyField);
            }

            _notificationPublisher.PublishSetTrackedDomains(deviceUids, evt.UserKeyField, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationPublisherActor] Error publishing SetTrackedDomains for user {UserKey}",
                evt.UserKeyField);
        }
    }

    private void HandleImmediateDangerEnded(ImmediateDangerEnded immediateDangerEnded)
    {
        // Publish notification
        _notificationPublisher.PublishImmediateDangerEnded(
            immediateDangerEnded.DeviceUid,
            immediateDangerEnded.UserKey.Value,
            immediateDangerEnded
        );
    }

    private void HandleImmediateDangerEvent(ImmediateDangerEvent immediateDangerEvent)
    {
        // Publish notification
        _notificationPublisher.PublishImmediateDangerEvent(
            immediateDangerEvent.DeviceUid,
            immediateDangerEvent.UserKey.Value,
            immediateDangerEvent
        );
    }
    private void HandleAnalysisResultReceived(AnalysisResultReceived analysisEvent)
    {
        try
        {
            if (analysisEvent.AnalyzerResults.Count < 1)
            {
                return;
            }

            var analyzerResult = analysisEvent.AnalyzerResults.First().Value;
            RiskAssessment? riskAssessment = null;
            switch (analyzerResult.Item1)
            {
                case UrlAnalysisResult urlAnalyzerResult:
                    riskAssessment = urlAnalyzerResult.risk_assessment;
                    break;
                case TrackUrlAnalysisResult trackUrlAnalyzerResult:
                    riskAssessment = trackUrlAnalyzerResult.risk_assessment;
                    break;
                case RemoteAccessAnalysisResult remoteAccessAnalyzerResult:
                    riskAssessment = remoteAccessAnalyzerResult.risk_assessment;
                    break;
            }

            if (riskAssessment is null)
            {
                return;
            }
            // Create notification payload
            var notification = new AnalysisResultNotification(
                analysisEvent.AlertType,
                analysisEvent.Severity.ToString(),
                riskAssessment,
                analyzerResult.Item1,
                analyzerResult.Item3.ToList(),
                analyzerResult.Item2.ToList(),
                analysisEvent.AnalysisTimestamp
            );


            // Publish notification
            if (analysisEvent.MessagingIdentity is null)
            {
                _notificationPublisher.PublishAnalysisResult(
                    analysisEvent.DeviceUid,
                    analysisEvent.UserKeyField,
                    notification
                );
            }
            else
            {
                _notificationPublisher.PublishAnalysisResult(
                    analysisEvent.DeviceUid,
                    analysisEvent.UserKeyField,
                    notification,
                    analysisEvent.MessagingIdentity
                );
            }

            _logger.LogInformation(
                $"[NotificationPublisherActor] Published notification for device {analysisEvent.DeviceUid}, " +
                $"AlertType: {analysisEvent.AlertType}, Severity: {analysisEvent.Severity}" + 
                $"Analysis Result: {analysisEvent.AnalyzerResults.First().Value.Item1}") ;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[NotificationPublisherActor] Error publishing notification for device {analysisEvent.DeviceUid}");
        }
    }
}



