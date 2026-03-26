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
    public ProtectiveAction DetermineActions(
        double riskScore,
        bool hasRemoteAccess = false,
        bool isTargeted = false)
    {
        var actions = ProtectiveAction.None;

        // Always log events for analysis
        if (riskScore > 0)
            actions |= ProtectiveAction.LogEvent;

        // 0-20: Passive monitoring
        if (riskScore <= 20)
        {
            return actions; // Just logging
        }

        // 21-40: Warning Banner
        if (riskScore <= 40)
        {
            actions |= ProtectiveAction.WarningBanner;
            return actions;
        }

        // 41-60: Push + Modal + Detailed Tracking
        if (riskScore <= 60)
        {
            actions |= ProtectiveAction.WarningBanner;
            actions |= ProtectiveAction.PushNotification;
            actions |= ProtectiveAction.ModalDialog;
            actions |= ProtectiveAction.DetailedTracking;
            actions |= ProtectiveAction.AlertGuardian;
            return actions;
        }

        // 61-80: Block Page + Disconnect Remote Access + SMS to Contact
        if (riskScore <= 80)
        {
            actions |= ProtectiveAction.WarningBanner;
            actions |= ProtectiveAction.PushNotification;
            actions |= ProtectiveAction.ModalDialog;
            actions |= ProtectiveAction.DetailedTracking;
            actions |= ProtectiveAction.BlockPage;
            actions |= ProtectiveAction.AlertGuardian;
            actions |= ProtectiveAction.SmsEmergencyContact;

            if (hasRemoteAccess)
            {
                actions |= ProtectiveAction.DisconnectRemoteAccess;
            }

            return actions;
        }

        // 81-100: Cross-Platform Lock + Black Screen + Lock Browser
        actions |= ProtectiveAction.WarningBanner;
        actions |= ProtectiveAction.PushNotification;
        actions |= ProtectiveAction.ModalDialog;
        actions |= ProtectiveAction.DetailedTracking;
        actions |= ProtectiveAction.BlockPage;
        actions |= ProtectiveAction.AlertGuardian;
        actions |= ProtectiveAction.SmsEmergencyContact;
        actions |= ProtectiveAction.CrossPlatformLock;
        actions |= ProtectiveAction.LockBrowser;

        if (hasRemoteAccess)
        {
            actions |= ProtectiveAction.DisconnectRemoteAccess;
            actions |= ProtectiveAction.BlackScreen;
        }

        return actions;
    }

    /// <summary>
    /// Check if specific action should be taken
    /// </summary>
    public bool ShouldTakeAction(ProtectiveAction actions, ProtectiveAction specificAction)
    {
        return actions.HasFlag(specificAction);
    }

    /// <summary>
    /// Get human-readable description of actions
    /// </summary>
    public List<string> DescribeActions(ProtectiveAction actions)
    {
        var descriptions = new List<string>();

        if (actions.HasFlag(ProtectiveAction.LogEvent))
            descriptions.Add("Log event for analysis");

        if (actions.HasFlag(ProtectiveAction.WarningBanner))
            descriptions.Add("Display warning banner");

        if (actions.HasFlag(ProtectiveAction.PushNotification))
            descriptions.Add("Send push notification");

        if (actions.HasFlag(ProtectiveAction.ModalDialog))
            descriptions.Add("Show blocking modal dialog");

        if (actions.HasFlag(ProtectiveAction.DetailedTracking))
            descriptions.Add("Enable detailed session tracking");

        if (actions.HasFlag(ProtectiveAction.BlockPage))
            descriptions.Add("Block access to page");

        if (actions.HasFlag(ProtectiveAction.DisconnectRemoteAccess))
            descriptions.Add("Disconnect remote access session");

        if (actions.HasFlag(ProtectiveAction.SmsEmergencyContact))
            descriptions.Add("Send SMS to emergency contact");

        if (actions.HasFlag(ProtectiveAction.CrossPlatformLock))
            descriptions.Add("Lock all user devices");

        if (actions.HasFlag(ProtectiveAction.BlackScreen))
            descriptions.Add("Activate black screen protection");

        if (actions.HasFlag(ProtectiveAction.LockBrowser))
            descriptions.Add("Lock browser completely");

        if (actions.HasFlag(ProtectiveAction.AlertGuardian))
            descriptions.Add("Alert guardian/administrator");

        return descriptions;
    }

    /// <summary>
    /// Get severity level based on actions
    /// </summary>
    public RiskSeverity GetSeverity(ProtectiveAction actions)
    {
        if (actions.HasFlag(ProtectiveAction.LockBrowser) ||
            actions.HasFlag(ProtectiveAction.CrossPlatformLock))
            return RiskSeverity.Critical;

        if (actions.HasFlag(ProtectiveAction.BlockPage) ||
            actions.HasFlag(ProtectiveAction.DisconnectRemoteAccess))
            return RiskSeverity.High;

        if (actions.HasFlag(ProtectiveAction.ModalDialog))
            return RiskSeverity.Medium;

        if (actions.HasFlag(ProtectiveAction.WarningBanner))
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
