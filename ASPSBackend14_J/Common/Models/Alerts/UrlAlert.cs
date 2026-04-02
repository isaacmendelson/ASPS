namespace Common.Models.Alerts;

public class UrlAlert : DeviceAlert
{
    public UrlAlert()
    {
        // Parameterless constructor for deserialization
    }

    public UrlAlert(Key key, string url, Key[]? trackers, string[]? iFrameDomains, string userAgent, string tabId, string iPAddress)
    {
        Key = key;
        Url = url;
        Trackers = trackers ?? [];
        IFrameDomains = iFrameDomains ?? [];
        UserAgent = userAgent;
        TabId = tabId;
        IPAddress = iPAddress;
    }

    public Key Key { get; set; }
    public string Url { get; set; } = string.Empty;
    public Key[] Trackers { get; set; } = Array.Empty<Key>();
    public string[] IFrameDomains { get; set; } = Array.Empty<string>();
    public string UserAgent { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;

    public string IPAddress { get; set; } = string.Empty;

}
