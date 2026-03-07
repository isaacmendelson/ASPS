using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;
using Common.Models;
using Common.Models.Alerts;

namespace Common.Entities;

/// <summary>
/// Base class for all device alerts stored in the database.
/// Discriminator column stores the specific alert type.
/// </summary>
public abstract class DeviceAlertEntity : Entity, IDeviceAlert
{
    //public DeviceAlertEntity (string alertType)
    //{
    //    this.AlertType = alertType;

    //}

    private Tag? tag;

    protected DeviceAlertEntity()
    {
        // Parameterless constructor for EF Core
    }
    //public DeviceAlertEntity(Tag? tag, DeviceAlert alert, string alertType, Priority priority, DateTime timestamp, string token, 
    //    string deviceUid, DeviceType deviceType, OperatingSystemType operatingSystem, string mAC, string? userKeyField, 
    //    AlertFlagStatus status, User? user, string? deviceKeyField, UserDevice? device, Key? userKey, Key? deviceKey)
    //{
    //    this.tag = tag;
    //    Alert = alert;
    //    AlertType = alertType;
    //    Priority = priority;
    //    Timestamp = timestamp;
    //    Token = token;
    //    DeviceUid = deviceUid;
    //    DeviceType = deviceType;
    //    OperatingSystem = operatingSystem;
    //    MAC = mAC;
    //    UserKeyField = userKeyField;
    //    Status = status;
    //    User = user;
    //    DeviceKeyField = deviceKeyField;
    //    Device = device;
    //    UserKey = userKey;
    //    DeviceKey = deviceKey;
    //}

    public string? AlertId
    {
        get
        {
            return this.KeyField;
        }
    }

    //public DeviceAlert Alert { get; set; }

    public string AlertType { get; set; }
    public Priority Priority { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Token { get; set; } = string.Empty;
    
    // Device Information (flattened for database storage)
    public string DeviceUid { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }
    public string MAC { get; set; } = string.Empty;
    public string? IPAddress { get; set; }
    
    // Foreign Key: Link to user if device is registered
    public string? UserKeyField { get; set; }  // Foreign key to User.KeyField

    public AlertFlagStatus Status { get; set; } = AlertFlagStatus.Unknown;


    // Navigation property to User (EF Core will use UserKeyField as FK)
    [ForeignKey(nameof(UserKeyField))]
    public User? User { get; set; }
    
    // Foreign Key: Link to device
    public string? DeviceKeyField { get; set; }  // Foreign key to UserDevice.KeyField
    
    // Navigation property to UserDevice (EF Core will use DeviceKeyField as FK)
    [ForeignKey(nameof(DeviceKeyField))]
    public UserDevice? Device { get; set; }
    
    // Computed UserKey property for compatibility
    [NotMapped]
    public Key? UserKey
    {
        get => !string.IsNullOrEmpty(UserKeyField) ? new Key("User", UserKeyField) : null;
        set => UserKeyField = value?.Value;
    }
    
    // Computed DeviceKey property for compatibility
    [NotMapped]
    public Key? DeviceKey
    {
        get => !string.IsNullOrEmpty(DeviceKeyField) ? new Key("UserDevice", DeviceKeyField) : null;
        set => DeviceKeyField = value?.Value;
    }
    
    // IDeviceAlert implementation - DeviceInfo property
    [NotMapped]
    public DeviceInfo DeviceInfo
    {
        get
        {
            if (Device != null)
            {
                // Map from OperatingSystem enum to OperatingSystemType enum
                var osType = (OperatingSystemType)((int)Device.OperatingSystem);
                
                return new DeviceInfo(
                    DeviceKey ?? new Key("UserDevice", DeviceKeyField ?? ""),
                    DeviceUid,
                    Device.AggregateVersionField.ToString(),
                    "", // IP - not available in alert
                    "", // UserAgent - not available in alert
                    0,  // Timezone - not available in alert
                    osType,
                    this.Device.MAC,
                    this.Device?.UserKey
                );
            }
            else
            {
                // Fallback when Device navigation property is not loaded
                return new DeviceInfo(
                    DeviceKey ?? new Key("UserDevice", DeviceKeyField ?? ""),
                    DeviceUid,
                    "0", // AggregateVersion unknown
                    "",
                    "",
                    0,
                    OperatingSystem, // Use the flattened OperatingSystem property
                    this.Device?.MAC,
                    this.Device?.UserKey
                );
            }
        }
    }
    
    [NotMapped]
    public override Tag Tag
    {
        get
        {
            if (this.tag is not null)
                return this.tag;
                
            if (!string.IsNullOrEmpty(this.KeyField))
            {
                this.tag = this.CreateTag();
                return this.tag;
            }
            
            return this.CreateTag();
        }
    }
    
    protected virtual Tag CreateTag()
    {
        var name = $"{AlertType} - {DeviceUid} - {Timestamp:yyyy-MM-dd HH:mm}";
        return new Tag(this.Key, name, this.TypeName);
    }

    public void SetStatus(AlertFlagStatus afs)
    {
        this.Status = afs;
    }
}

/// <summary>
/// Remote access alert stored in database
/// </summary>
public class RemoteAccessAlertEntity : DeviceAlertEntity
{
    public RemoteAccessAlertEntity()
    {
    }

    //public RemoteAccessAlertEntity(Tag? tag, RemoteAccessAlert alert, string alertType, Priority priority, DateTime timestamp, string token, 
    //    string deviceUid, DeviceType deviceType, OperatingSystemType operatingSystem, string mAC, string? userKeyField, 
    //    AlertFlagStatus status, User? user, string? deviceKeyField, UserDevice? device, Key? userKey, Key? deviceKey)
    //    : base(tag, alert, alertType, priority, timestamp, token, deviceUid, deviceType, operatingSystem, mAC, userKeyField, status, user, 
    //        deviceKeyField, device, userKey, deviceKey)
    //{
    //    this.RemoteAccessApp = alert?.RemoteAccessApp ?? new RemoteAccessApp();
    //    this.RunningProcesses = alert?.RunningProcesses ?? 0;
    //    this.ConnectionUrl = alert?.ConnectionUrl ?? string.Empty;
    //    this.ConnectionStatus = alert?.ConnectionStatus ?? ConnectionStatus.Unknown;
    //    this.ConnectionsCount = alert?.ConnectionsCount ?? 0;
    //    this.SessionStatus = alert?.SessionStatus ?? 0;
    //}

    public RemoteAccessApp RemoteAccessApp { get; set; }
    public int RunningProcesses { get; set; }
    public string ConnectionUrl { get; set; } = string.Empty;
    public ConnectionStatus ConnectionStatus { get; set; }
    public int ConnectionsCount { get; set; }
    public int SessionStatus { get; set; }
    
    [NotMapped]
    public override string TypeName => "RemoteAccessAlert";
}

/// <summary>
/// URL alert stored in database
/// </summary>
public class UrlAlertEntity : DeviceAlertEntity
{
    //protected UrlAlertEntity()
    //{
    //}

    //public UrlAlertEntity(Tag? tag, UrlAlert alert, string alertType, Priority priority, DateTime timestamp, string token, string deviceUid, 
    //    DeviceType deviceType, OperatingSystemType operatingSystem, string mAC, string? userKeyField, AlertFlagStatus status, User? user, 
    //    string? deviceKeyField, UserDevice? device, Key? userKey, Key? deviceKey)
    //    : base(tag, alert, alertType, priority, timestamp, token, deviceUid, deviceType, operatingSystem, mAC, userKeyField, 
    //        status, user, deviceKeyField, device, userKey, deviceKey)
    //{
    //    this.Url = (alert as UrlAlert)?.Url ?? string.Empty;
    //    this.IFrameDomains = alert?.IFrameDomains != null ? System.Text.Json.JsonSerializer.Serialize(alert?.IFrameDomains) : string.Empty;
    //    this.TrackerKeys = alert?.Trackers != null ? System.Text.Json.JsonSerializer.Serialize(alert?.Trackers) : string.Empty;
    //    this.UserAgent = alert?.UserAgent ?? string.Empty;

    //}

    public string Url { get; set; } = string.Empty;
    public string TrackerKeys { get; set; } = string.Empty; // JSON array of Keys
    public string IFrameDomains { get; set; } = string.Empty; // JSON array
    public string UserAgent { get; set; } = string.Empty;
    
    [NotMapped]
    public override string TypeName => "UrlAlert";
}
