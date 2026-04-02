namespace Common.Models.Alerts;

public class UrlAlert : DeviceAlert
{
    public UrlAlert()
    {
        // Parameterless constructor for deserialization
    }

    public string Url { get; set; } = string.Empty;
    public Key[] Trackers { get; set; } = Array.Empty<Key>();
    public string[] IFrameDomains { get; set; } = Array.Empty<string>();
    public string UserAgent { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;

    public string IPAddress { get; set; } = string.Empty;

}
