using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.UserDomain;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Business.RealtimeAnalysis;
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
        EventType = nameof(SpecificAnalyzerResultReceived);
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

/// <summary>
/// Event triggered when OTP interception is detected
/// ASPS-368: OTP Interception
/// Correlation between SMS received on mobile device and browser input attempt under Remote Access
/// </summary>
public class OtpInterceptionTriggered : DomainEvent
{
    public string UserKeyField { get; set; } = string.Empty;
    public string MobileDeviceUid { get; set; } = string.Empty;
    public string BrowserDeviceUid { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public DateTime SmsReceivedTimestamp { get; set; }
    public DateTime BrowserInputTimestamp { get; set; }
    public string RemoteAccessApp { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public double CorrelationConfidence { get; set; }

    public OtpInterceptionTriggered()
    {
        EventType = nameof(OtpInterceptionTriggered);
    }

    public OtpInterceptionTriggered(
        string userKeyField,
        string mobileDeviceUid,
        string browserDeviceUid,
        string otpCode,
        DateTime smsReceivedTimestamp,
        DateTime browserInputTimestamp,
        string remoteAccessApp,
        string targetUrl,
        bool isBlocked,
        double correlationConfidence)
    {
        EventType = nameof(OtpInterceptionTriggered);
        UserKeyField = userKeyField;
        MobileDeviceUid = mobileDeviceUid;
        BrowserDeviceUid = browserDeviceUid;
        OtpCode = otpCode;
        SmsReceivedTimestamp = smsReceivedTimestamp;
        BrowserInputTimestamp = browserInputTimestamp;
        RemoteAccessApp = remoteAccessApp;
        TargetUrl = targetUrl;
        IsBlocked = isBlocked;
        CorrelationConfidence = correlationConfidence;
    }
}

/// <summary>
/// Event triggered when Black Screen protection is activated
/// ASPS-369: Black Screen Implementation
/// DOM manipulation to hide sensitive information from remote viewer while keeping local user visibility
/// </summary>
public class BlackScreenActivated : DomainEvent
{
    public string UserKeyField { get; set; } = string.Empty;
    public string DeviceUid { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string RemoteAccessApp { get; set; } = string.Empty;
    public List<string> HiddenElements { get; set; } = new();
    public string InjectedScript { get; set; } = string.Empty;
    public DateTime ActivationTimestamp { get; set; }
    public string Reason { get; set; } = string.Empty;

    public BlackScreenActivated()
    {
        EventType = nameof(BlackScreenActivated);
        ActivationTimestamp = DateTime.UtcNow;
    }

    public BlackScreenActivated(
        string userKeyField,
        string deviceUid,
        string targetUrl,
        string remoteAccessApp,
        List<string> hiddenElements,
        string injectedScript,
        string reason)
    {
        EventType = nameof(BlackScreenActivated);
        UserKeyField = userKeyField;
        DeviceUid = deviceUid;
        TargetUrl = targetUrl;
        RemoteAccessApp = remoteAccessApp;
        HiddenElements = hiddenElements;
        InjectedScript = injectedScript;
        ActivationTimestamp = DateTime.UtcNow;
        Reason = reason;
    }
}

/// <summary>
/// Event triggered when user's details are found in marketing/scam lead lists for the first time
/// ASPS-370: UserIsTargetedAlert
/// Fires only once per user - when IsTargeted flag transitions from false to true
/// </summary>
public class UserIsTargetedAlertReceived : DomainEvent
{
    public string UserKeyField { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserPhoneNumber { get; set; } = string.Empty;
    public List<string> FoundInLists { get; set; } = new();
    public DateTime DiscoveryTimestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }

    public UserIsTargetedAlertReceived()
    {
        EventType = nameof(UserIsTargetedAlertReceived);
        DiscoveryTimestamp = DateTime.UtcNow;
    }

    public UserIsTargetedAlertReceived(
        string userKeyField,
        string userEmail,
        string userPhoneNumber,
        List<string> foundInLists,
        string source,
        double confidenceScore)
    {
        EventType = nameof(UserIsTargetedAlertReceived);
        UserKeyField = userKeyField;
        UserEmail = userEmail;
        UserPhoneNumber = userPhoneNumber;
        FoundInLists = foundInLists;
        DiscoveryTimestamp = DateTime.UtcNow;
        Source = source;
        ConfidenceScore = confidenceScore;
    }
}

/// <summary>
/// Event to distribute tracked domains to all user devices for cross-platform monitoring
/// ASPS-371: SetTrackedDomains Event
/// Synchronizes all agents to high alert status for specific domains
/// </summary>
public class SetTrackedDomains : DomainEvent
{
    public string UserKeyField { get; set; } = string.Empty;
    public List<TrackedDomainCommand> TrackedDomains { get; set; } = new();
    public bool IsCrossPlatformLock { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime DistributionTimestamp { get; set; }

    public SetTrackedDomains()
    {
        EventType = nameof(SetTrackedDomains);
        DistributionTimestamp = DateTime.UtcNow;
    }

    public SetTrackedDomains(
        string userKeyField,
        List<TrackedDomainCommand> trackedDomains,
        bool isCrossPlatformLock,
        string reason)
    {
        EventType = nameof(SetTrackedDomains);
        UserKeyField = userKeyField;
        TrackedDomains = trackedDomains;
        IsCrossPlatformLock = isCrossPlatformLock;
        Reason = reason;
        DistributionTimestamp = DateTime.UtcNow;
    }
}
