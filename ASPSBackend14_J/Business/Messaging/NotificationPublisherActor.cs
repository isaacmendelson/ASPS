using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Logging;

namespace Business.Messaging;

/// <summary>
/// Listens for AnalysisResultReceived events and publishes notifications to subscribers
/// </summary>
public class NotificationPublisherActor : IDomainEventHandler
{
    private readonly NotificationPublisher _notificationPublisher;
    private readonly ILogger<NotificationPublisherActor> _logger;

    public NotificationPublisherActor(
        NotificationPublisher notificationPublisher,
        ILogger<NotificationPublisherActor> logger)
    {
        _notificationPublisher = notificationPublisher;
        _logger = logger;
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
            default:
                _logger.LogWarning($"[NotificationPublisherActor] Received unhandled event type: {evt.GetType().Name}");
                break;
            }
    }

    public Type[] GetHandleableEvents()
    {
        return new[] { typeof(AnalysisResultReceived), typeof(ImmediateDangerEvent) };
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
            _notificationPublisher.PublishAnalysisResult(
                analysisEvent.DeviceUid,
                analysisEvent.UserKeyField,
                notification
            );

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



