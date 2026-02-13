using Common.Enums;

namespace Common.ViewModels;

public class RemoteAccessAlertVM
{
    public string DeviceUid { get; set; } = string.Empty;
    public RemoteAccessApp RemoteAccessApp { get; set; }
    public int RunningProcesses { get; set; }
    public string ConnectionUrl { get; set; } = string.Empty;
    public ConnectionStatus ConnectionStatus { get; set; }
    public int ConnectionsCount { get; set; }
    public int SessionStatus { get; set; }
    public Severity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PhishingAlertVM
{
    public string DeviceUid { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

public class VoiceCallAlertVM
{
    public string DeviceUid { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public byte[] VoiceSample { get; set; } = Array.Empty<byte>();
    public Severity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}
