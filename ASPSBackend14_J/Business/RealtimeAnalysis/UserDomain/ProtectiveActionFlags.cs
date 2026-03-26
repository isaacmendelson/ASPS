namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Protective actions that can be triggered based on risk score
/// ASPS-372: Protective Actions Matrix
/// </summary>
[Flags]
public enum ProtectiveActionFlags
{
    /// <summary>
    /// No action - just passive monitoring
    /// </summary>
    None = 0,

    /// <summary>
    /// Log event for later analysis
    /// </summary>
    LogEvent = 1 << 0,

    /// <summary>
    /// Show warning banner to user
    /// </summary>
    WarningBanner = 1 << 1,

    /// <summary>
    /// Send push notification
    /// </summary>
    PushNotification = 1 << 2,

    /// <summary>
    /// Show modal dialog (blocking UI)
    /// </summary>
    ModalDialog = 1 << 3,

    /// <summary>
    /// Enable detailed tracking for this session
    /// </summary>
    DetailedTracking = 1 << 4,

    /// <summary>
    /// Block access to specific page/domain
    /// </summary>
    BlockPage = 1 << 5,

    /// <summary>
    /// Disconnect remote access session
    /// </summary>
    DisconnectRemoteAccess = 1 << 6,

    /// <summary>
    /// Send SMS to emergency contact
    /// </summary>
    SmsEmergencyContact = 1 << 7,

    /// <summary>
    /// Enable cross-platform lock (all devices)
    /// </summary>
    CrossPlatformLock = 1 << 8,

    /// <summary>
    /// Activate black screen protection
    /// </summary>
    BlackScreen = 1 << 9,

    /// <summary>
    /// Lock browser completely
    /// </summary>
    LockBrowser = 1 << 10,

    /// <summary>
    /// Alert guardian/administrator
    /// </summary>
    AlertGuardian = 1 << 11
}
