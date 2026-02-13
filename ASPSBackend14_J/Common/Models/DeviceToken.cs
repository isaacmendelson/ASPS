namespace Common.Models;

/// <summary>
/// Represents an authentication token issued to a device.
/// Stored in-memory by TokenStore, keyed by DeviceUid.
/// </summary>
public class DeviceToken
{
    public DateTime DateCreated { get; set; }
    public string TokenValue { get; set; } = string.Empty;
    public string DeviceUid { get; set; } = string.Empty;
    public string UserKeyField { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
}

/// <summary>
/// Result of token validation
/// </summary>
public enum TokenValidationResult
{
    Valid,
    InvalidToken,
    TokenExpired
}
