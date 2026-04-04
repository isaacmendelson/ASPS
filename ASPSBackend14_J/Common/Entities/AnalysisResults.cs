using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;
using Common.Models;

namespace Common.Entities;

public class AnalysisResultContainer : Entity
{
    private Tag? tag;

    public AnalysisResultContainer(string keyField, string userKeyField, string discriminator, DateTime timestamp, string? jsonValue, bool? hasError, string? errorMessage, bool? isFromCache = false, string? deviceAlertKeyField = null)
    {
        this.UserKeyField = userKeyField;
        this.Discriminator = discriminator;
        this.JsonValue = jsonValue;
        this.HasError = hasError;
        this.ErrorMessage = errorMessage;
        this.IsFromCache = isFromCache ?? false;
        this.DeviceAlertKeyField = deviceAlertKeyField;
        this.SetKeyField(keyField);
        this.Timestamp = timestamp;
        this.DateCreated = timestamp;
        this.KeyField = keyField;
        this.UserKeyField = userKeyField;
        this.Discriminator = discriminator;
    }

    protected AnalysisResultContainer()
    {
    }
    
    public string UserKeyField { get; set; } = string.Empty;  // Changed from Guid to string
    public string Discriminator { get; set; } = string.Empty;
    public string? JsonValue { get; set; }
    public bool? HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsFromCache { get; set; }
    public string? DeviceAlertKeyField { get; set; }  // Key of the DeviceAlert that initiated this analysis
    
    // Computed UserKey property for compatibility
    [NotMapped]
    public Key UserKey
    {
        get => new Key(nameof(User), UserKeyField);
        set => UserKeyField = value.Value;
    }
    
    // Computed DeviceAlertKey property
    [NotMapped]
    public Key? DeviceAlertKey
    {
        get => DeviceAlertKeyField != null ? new Key(nameof(DeviceAlertEntity), DeviceAlertKeyField) : null;
        set => DeviceAlertKeyField = value?.Value;
    }

    // Navigation property to User (EF Core will use UserKeyField as FK)
    [ForeignKey(nameof(DeviceAlertKeyField))]
    public DeviceAlertEntity? DeviceAlert { get; set; }


    // Navigation property to User (EF Core will use UserKeyField as FK)
    [ForeignKey(nameof(UserKeyField))]
    public User? User { get; set; }

    [NotMapped]
    public override string TypeName => nameof(AnalysisResultContainer);
    
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
        var name = $"{Discriminator} - {Timestamp:yyyy-MM-dd HH:mm}";
        return new Tag(this.Key, name, this.TypeName);
    }
}

public class UrlAnalysisResultContainer : AnalysisResultContainer
{
    protected UrlAnalysisResultContainer() { }

    public UrlAnalysisResultContainer(string url, string keyField, string userKeyField, string discriminator, DateTime timestamp, string? jsonValue, bool? hasError,
        string? errorMessage, bool? isFromCache = false, string? deviceAlertKeyField = null)
        :base(keyField, userKeyField, discriminator, timestamp, jsonValue, hasError, errorMessage, isFromCache, deviceAlertKeyField)
    {
        this.Url = url;
        this.Domain = KnownPhishingWebsite.GetDomainFromUrl(url);
    }
    public string? Domain { get; set; } = string.Empty;
    public string? Url { get; set; } = string.Empty;
    
    [NotMapped]
    public override string TypeName => "UrlAnalysisResult";
}

public class TrackUrlAnalysisResultContainer : AnalysisResultContainer
{
    protected TrackUrlAnalysisResultContainer() { }

    public TrackUrlAnalysisResultContainer(string keyField, string url, string fromUrl, string userKeyField, string discriminator, DateTime timestamp, string? jsonValue, bool? hasError,
        string? errorMessage, bool? isFromCache = false, string? deviceAlertKeyField = null)
        : base(keyField, userKeyField, discriminator, timestamp, jsonValue, hasError, errorMessage, isFromCache, deviceAlertKeyField)
    {
        this.Url = url;
        this.FromUrl = fromUrl;
        this.Domain = KnownPhishingWebsite.GetDomainFromUrl(url);
    }
    public string? Domain { get; set; } = string.Empty;
    public string? Url { get; set; } = string.Empty;
    public string? FromUrl { get; set; } = string.Empty;

    [NotMapped]
    public override string TypeName => "TrackUrlAnalysisResult";
}

public class RemoteAccessAnalysisResultContainer : AnalysisResultContainer
{
    protected RemoteAccessAnalysisResultContainer() { }

    public RemoteAccessAnalysisResultContainer(RemoteAccessApp remoteAccessApp, SessionStatus? sessionStatus, string keyField, string userKeyField, string discriminator, DateTime timestamp, string? jsonValue, bool? hasError,
        string? errorMessage, bool? isFromCache = false, string? deviceAlertKeyField = null)
        : base(keyField, userKeyField, discriminator, timestamp, jsonValue, hasError, errorMessage, isFromCache, deviceAlertKeyField)
    {
        this.RemoteAccessApp = (int)remoteAccessApp;
        this.SessionStatus = (int?)sessionStatus ?? 0;
    }
    public int RemoteAccessApp { get; set; } = 0;
    public int SessionStatus { get; set; } = 0;

    [NotMapped]
    public override string TypeName => "TrackUrlAnalysisResult";
}


public class AlertFlag
{
    public int Key { get; set; }
    public int UserKey { get; set; }
    public int SensorType { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public AlertFlagType AlertFlagType { get; set; }
    public AlertFlagStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? Deleted { get; set; }
    public DateTime Modified { get; set; } = DateTime.UtcNow;
}
