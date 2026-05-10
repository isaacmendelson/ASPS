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
public class TabChangedAlert : DeviceAlert
{
    public TabChangedAlert()
    {
        // Parameterless constructor for deserialization
    }

    public TabChangedAlert(Key key, string tabId, string url, DateTime Timestamp, bool? isSensitiveWebsite, bool? isLoggedIn)
    {
        this.Key = key;
        this.AlertId = this.Key.ToString(); // AlertId is required by the base class, so we can set it to the stringified Key
        this.AlertType = Key.Type;

        this.TabId = tabId;
        this.Url = url;
        this.Timestamp = Timestamp;
        this.IsSensitiveWebsite = isSensitiveWebsite;
        this.IsLoggedIn = isLoggedIn;
    }

    public TabChangedAlert(string alertId, string tabId, string url, DateTime Timestamp, bool? isSensitiveWebsite, bool? isLoggedIn)
    {
        this.Key = new Key(nameof(TabChangedAlert), alertId);
        this.AlertId = alertId; // AlertId is required by the base class
        this.AlertType = nameof(TabChangedAlert);

        this.TabId = tabId;
        this.Url = url;
        this.Timestamp = Timestamp;
        this.IsSensitiveWebsite = isSensitiveWebsite;
        this.IsLoggedIn = isLoggedIn;
    }
    public Key Key { get; set; } = new Key();

    /// <summary>The browser-tab id (extension-assigned, stringified).</summary>
    public string TabId { get; set; } = string.Empty;

    /// <summary>The URL of the tab at the moment it was closed.</summary>
    public string Url { get; set; } = string.Empty;
    public bool? IsSensitiveWebsite { get; set; }
    public bool? IsLoggedIn { get; set; }

}
