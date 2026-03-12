using System;

namespace Interface.Analysis;

/// <summary>
/// Data Transfer Object for Analysis Results.
/// Used for API responses and data serialization.
/// </summary>
public class AnalysisResultDto
{
    public string UserKeyField { get; set; } = string.Empty;
    public string Discriminator { get; set; } = string.Empty;
    public string? JsonValue { get; set; }
    public bool? HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFromCache { get; set; }
    public string? DeviceAlertKeyField { get; set; }

    /// <summary>
    /// Default constructor for serialization
    /// </summary>
    public AnalysisResultDto()
    {
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Constructor with all required fields
    /// </summary>
    public AnalysisResultDto(
        string userKeyField, 
        string discriminator, 
        DateTime timestamp, 
        string? jsonValue = null, 
        bool hasError = false, 
        string? errorMessage = null, 
        bool isFromCache = false, 
        string? deviceAlertKeyField = null)
    {
        UserKeyField = userKeyField ?? throw new ArgumentNullException(nameof(userKeyField));
        Discriminator = discriminator ?? throw new ArgumentNullException(nameof(discriminator));
        Timestamp = timestamp;
        JsonValue = jsonValue;
        HasError = hasError;
        ErrorMessage = errorMessage;
        IsFromCache = isFromCache;
        DeviceAlertKeyField = deviceAlertKeyField;
    }
}

/// <summary>
/// DTO for URL-specific analysis results
/// </summary>
public class UrlAnalysisResultDto : AnalysisResultDto
{
    public string Domain { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public UrlAnalysisResultDto() : base()
    {
    }

    public UrlAnalysisResultDto(
        string userKeyField, 
        string discriminator, 
        DateTime timestamp, 
        string domain, 
        string url, 
        string? jsonValue = null, 
        bool hasError = false, 
        string? errorMessage = null) 
        : base(userKeyField, discriminator, timestamp, jsonValue, hasError, errorMessage)
    {
        Domain = domain ?? throw new ArgumentNullException(nameof(domain));
        Url = url ?? throw new ArgumentNullException(nameof(url));
    }
}
