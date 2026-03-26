namespace Business.RealtimeAnalysis.UserDomain;

/// <summary>
/// Determines protective actions based on risk score
/// ASPS-372: Protective Actions Matrix
/// 
/// Risk Score Ranges:
/// 0-20: Passive monitoring only
/// 21-40: Warning banner
/// 41-60: Push + Modal + Detailed tracking
/// 61-80: Block page, disconnect remote access, SMS to contact
/// 81-100: Cross-platform lock, black screen, lock browser
/// </summary>
public class ProtectiveActionsMatrix
{
    /// <summary>
    /// Determine protective actions based on risk score
    /// </summary>
    /// <param name="riskScore">Risk score (0-100)</param>
    /// <param name="hasRemoteAccess">Whether remote access is currently active</param>
    /// <param name="isTargeted">Whether user is marked as targeted</param>
    /// <returns>Combined protective actions to execute</returns>
    public ProtectiveActionFlags DetermineActions(
        double riskScore,
        bool hasRemoteAccess = false,
        bool isTargeted = false)
    {
        var actions = ProtectiveActionFlags.None;

        // Always log events for analysis
        if (riskScore > 0)
            actions |= ProtectiveActionFlags.LogEvent;

        // 0-20: Passive monitoring
        if (riskScore <= 20)
        {
            return actions; // Just logging
        }

        // 21-40: Warning Banner
        if (riskScore <= 40)
        {
            actions |= ProtectiveActionFlags.WarningBanner;
            return actions;
        }

        // 41-60: Push + Modal + Detailed Tracking
        if (riskScore <= 60)
        {
            actions |= ProtectiveActionFlags.WarningBanner;
            actions |= ProtectiveActionFlags.PushNotification;
            actions |= ProtectiveActionFlags.ModalDialog;
            actions |= ProtectiveActionFlags.DetailedTracking;
            actions |= ProtectiveActionFlags.AlertGuardian;
            return actions;
        }

        // 61-80: Block Page + Disconnect Remote Access + SMS to Contact
        if (riskScore <= 80)
        {
            actions |= ProtectiveActionFlags.WarningBanner;
            actions |= ProtectiveActionFlags.PushNotification;
            actions |= ProtectiveActionFlags.ModalDialog;
            actions |= ProtectiveActionFlags.DetailedTracking;
            actions |= ProtectiveActionFlags.BlockPage;
            actions |= ProtectiveActionFlags.AlertGuardian;
            actions |= ProtectiveActionFlags.SmsEmergencyContact;

            if (hasRemoteAccess)
            {
                actions |= ProtectiveActionFlags.DisconnectRemoteAccess;
            }

            return actions;
        }

        // 81-100: Cross-Platform Lock + Black Screen + Lock Browser
        actions |= ProtectiveActionFlags.WarningBanner;
        actions |= ProtectiveActionFlags.PushNotification;
        actions |= ProtectiveActionFlags.ModalDialog;
        actions |= ProtectiveActionFlags.DetailedTracking;
        actions |= ProtectiveActionFlags.BlockPage;
        actions |= ProtectiveActionFlags.AlertGuardian;
        actions |= ProtectiveActionFlags.SmsEmergencyContact;
        actions |= ProtectiveActionFlags.CrossPlatformLock;
        actions |= ProtectiveActionFlags.LockBrowser;

        if (hasRemoteAccess)
        {
            actions |= ProtectiveActionFlags.DisconnectRemoteAccess;
            actions |= ProtectiveActionFlags.BlackScreen;
        }

        return actions;
    }

    /// <summary>
    /// Check if specific action should be taken
    /// </summary>
    public bool ShouldTakeAction(ProtectiveActionFlags actions, ProtectiveActionFlags specificAction)
    {
        return actions.HasFlag(specificAction);
    }

    /// <summary>
    /// Get human-readable description of actions
    /// </summary>
    public List<string> DescribeActions(ProtectiveActionFlags actions)
    {
        var descriptions = new List<string>();

        if (actions.HasFlag(ProtectiveActionFlags.LogEvent))
            descriptions.Add("Log event for analysis");

        if (actions.HasFlag(ProtectiveActionFlags.WarningBanner))
            descriptions.Add("Display warning banner");

        if (actions.HasFlag(ProtectiveActionFlags.PushNotification))
            descriptions.Add("Send push notification");

        if (actions.HasFlag(ProtectiveActionFlags.ModalDialog))
            descriptions.Add("Show blocking modal dialog");

        if (actions.HasFlag(ProtectiveActionFlags.DetailedTracking))
            descriptions.Add("Enable detailed session tracking");

        if (actions.HasFlag(ProtectiveActionFlags.BlockPage))
            descriptions.Add("Block access to page");

        if (actions.HasFlag(ProtectiveActionFlags.DisconnectRemoteAccess))
            descriptions.Add("Disconnect remote access session");

        if (actions.HasFlag(ProtectiveActionFlags.SmsEmergencyContact))
            descriptions.Add("Send SMS to emergency contact");

        if (actions.HasFlag(ProtectiveActionFlags.CrossPlatformLock))
            descriptions.Add("Lock all user devices");

        if (actions.HasFlag(ProtectiveActionFlags.BlackScreen))
            descriptions.Add("Activate black screen protection");

        if (actions.HasFlag(ProtectiveActionFlags.LockBrowser))
            descriptions.Add("Lock browser completely");

        if (actions.HasFlag(ProtectiveActionFlags.AlertGuardian))
            descriptions.Add("Alert guardian/administrator");

        return descriptions;
    }

    /// <summary>
    /// Get severity level based on actions
    /// </summary>
    public RiskSeverity GetSeverity(ProtectiveActionFlags actions)
    {
        if (actions.HasFlag(ProtectiveActionFlags.LockBrowser) ||
            actions.HasFlag(ProtectiveActionFlags.CrossPlatformLock))
            return RiskSeverity.Critical;

        if (actions.HasFlag(ProtectiveActionFlags.BlockPage) ||
            actions.HasFlag(ProtectiveActionFlags.DisconnectRemoteAccess))
            return RiskSeverity.High;

        if (actions.HasFlag(ProtectiveActionFlags.ModalDialog))
            return RiskSeverity.Medium;

        if (actions.HasFlag(ProtectiveActionFlags.WarningBanner))
            return RiskSeverity.Low;

        return RiskSeverity.Info;
    }
}

/// <summary>
/// Risk severity levels
/// </summary>
public enum RiskSeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
