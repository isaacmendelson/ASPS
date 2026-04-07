#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;
using Common.Models;

namespace Common.Entities;

public abstract class UserDevice : Entity
{
    private Tag? tag;
    
    public int AggregateVersionField { get; set; }
    public string? UserKeyField { get; set; }  // Changed from Guid to string
    public DeviceType DeviceType { get; set; }
    public string DeviceUid { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public OperatingSystemType OperatingSystem { get; set; }
    public string? MAC { get; set; }
    public string? IMEI { get; set; }
    public string? BiosSerial { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
    public DeviceMonitoringStatus MonitoringStatus { get; set; }

    // Navigation property removed - use repository to fetch user if needed
    
    // Computed UserKey property for compatibility
    [NotMapped]
    public Key? UserKey
    {
        get => !string.IsNullOrEmpty(UserKeyField) ? new Key("User", UserKeyField) : null;
        set => UserKeyField = value?.Value;
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
        var name = !string.IsNullOrWhiteSpace(DeviceUid) ? DeviceUid 
            : !string.IsNullOrWhiteSpace(Make) && !string.IsNullOrWhiteSpace(Model) ? $"{Make} {Model}"
            : "Unknown Device";
        return new Tag(this.Key, name, this.TypeName);
    }
}
