using Common.Enums;

namespace Common.Models;

public abstract class DeviceMessage
{
    public Priority Priority { get; set; }
    public DeviceInfo DeviceInfo { get; set; } = new DeviceInfo();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Token { get; set; } = string.Empty;
}

public interface IDeviceAlert
{
    Priority Priority { get; }
    DeviceInfo DeviceInfo { get; }
    DateTime Timestamp { get; }
}

public abstract class DeviceAlert : DeviceMessage, IDeviceAlert
{

    public string AlertType { get; set; } = string.Empty;

    public string? AlertId { get; set; }

    public AlertFlagStatus Status { get; private set; } = AlertFlagStatus.Unknown;

    public void SetStatus(AlertFlagStatus afs)
    {
        this.Status = afs;
    }

}
