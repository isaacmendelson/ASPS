namespace Common.Models.Alerts;

/// <summary>
/// Sent by the desktop agent (forwarding from the Chrome extension) when a
/// browser tab is CLOSED while the agent is in ImmediateDanger mode.
///
/// Why we care: during an ImmediateDanger event (incoming remote-access
/// session + sensitive site open), a tab close is a meaningful signal —
/// either the user closed the sensitive site themselves (danger may be
/// clearing) or the remote attacker closed something (e.g., a tab they
/// opened to redirect the user). The backend's UDUserAnalyzer re-runs
/// DetectImmediateDanger when this alert arrives, so the danger state is
/// re-evaluated immediately rather than waiting for the next polling tick.
/// </summary>
public class TabClosedAlert : DeviceAlert
{
    public TabClosedAlert()
    {
        // Parameterless constructor for deserialization
    }

    public TabClosedAlert(Key key, string tabId, string url)
    {
        Key = key;
        TabId = tabId;
        Url = url;
    }

    public Key Key { get; set; } = new Key();

    /// <summary>The browser-tab id (extension-assigned, stringified).</summary>
    public string TabId { get; set; } = string.Empty;

    /// <summary>The URL of the tab at the moment it was closed.</summary>
    public string Url { get; set; } = string.Empty;
}
