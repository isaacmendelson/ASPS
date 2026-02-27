namespace Common.Entities;

public class DeviceTokenEntity
{
    public string DeviceUid { get; set; } = string.Empty;
    public string TokenValue { get; set; } = string.Empty;
    public string UserKeyField { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime Expiration { get; set; }
}
