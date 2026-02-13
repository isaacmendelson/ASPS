using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;

namespace Business.DomainEvents;

public abstract class DomainEvent : IDomainEvent
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
}

public class DeviceAlertReceived : DomainEvent
{
    protected DeviceAlertReceived()
    {
        EventType = nameof(DeviceAlertReceived);
    }

    public DeviceAlertReceived(DeviceAlert alert, Priority priority, string deviceUid, DateTime receiveTimestamp, DateTime messageTimestamp, string deviceAlertEntityKey)
    {
        EventType = nameof(DeviceAlertReceived);
        Alert = alert;
        Priority = priority;
        DeviceUid = deviceUid;
        ReceiveTimestamp = receiveTimestamp;
        MessageTimestamp = messageTimestamp;
        DeviceAlertEntityKey = deviceAlertEntityKey;
    }
    public DeviceAlert Alert { get; set; } = null!;
    public Priority Priority { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public DateTime ReceiveTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime MessageTimestamp { get; set; }
    public string DeviceAlertEntityKey { get; set; } = string.Empty;  // Key of the entity in DB


}

public class AnalysisResultReceived : DomainEvent
{
    public string UserKeyField { get; set; } = string.Empty;
    public string DeviceAlertKeyField { get; set; } = string.Empty;
    public string DeviceUid { get; set; } = string.Empty;

    public string AlertType { get; set; } = string.Empty;

    //public UDAnalysisResult AnalysisResult { get; set; } = string.Empty;

    public Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>> AnalyzerResults { get; set; } = new();

    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; }

    public AnalysisResultReceived()
    {
        EventType = nameof(AnalysisResultReceived);

    }
}

public class SpecificAnalyzerResultReceived : DomainEvent
{
    public string UserKeyField { get; set; } = string.Empty;
    public string DeviceAlertKeyField { get; set; } = string.Empty;
    public string DeviceUid { get; set; } = string.Empty;
    public string AnalyzerName { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; }

    public SpecificAnalyzerResultReceived()
    {
        EventType = nameof(AnalysisResultReceived);
    }
}

public class UserAdded : DomainEvent
{
    public User User { get; set; }

    public UserAdded(User user)
    {
        EventType = nameof(UserAdded);
        User = user;
    }
}

public class UserUpdated : DomainEvent
{
    public User User { get; set; }

    public UserUpdated(User user)
    {
        EventType = nameof(UserUpdated);
        User = user;
    }
}

public class UserDeleted : DomainEvent
{
    public string UserKeyField { get; set; }

    public UserDeleted(string userKeyField)
    {
        EventType = nameof(UserDeleted);
        UserKeyField = userKeyField;
    }
}
