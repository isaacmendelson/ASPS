namespace Common.Models;

public class DeviceRegistrationRequest
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserToken { get; set; } = string.Empty;
    public DeviceInfo DeviceInfo { get; set; } = new DeviceInfo();
    public string RequestId { get; set; } = string.Empty;
}

public class DeviceRegistrationResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string? DeviceUid { get; set; }
    public bool? HasError { get; set; }
    public string? ErrorMessage { get; set; }
}
