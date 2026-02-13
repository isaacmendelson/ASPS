using System.Collections.Concurrent;
using System.Security.Cryptography;
using Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Business.Services;

/// <summary>
/// In-memory store for device authentication tokens.
/// Thread-safe via ConcurrentDictionary, keyed by DeviceUid.
/// </summary>
public class TokenStore
{
    private readonly ConcurrentDictionary<string, DeviceToken> _tokens = new();
    private readonly ILogger<TokenStore> _logger;
    private readonly int _tokenExpirationMinutes;
    private readonly int _maxExpirationMinutes;

    public TokenStore(IConfiguration configuration, ILogger<TokenStore> logger)
    {
        _logger = logger;
        _tokenExpirationMinutes = configuration.GetValue<int>("TokenManagement:TokenExpirationPeriod", 1440);
        _maxExpirationMinutes = configuration.GetValue<int>("TokenManagement:MaxExpiration", 10080);

        _logger.LogInformation(
            "TokenStore initialized. ExpirationPeriod={ExpirationMinutes}min, MaxExpiration={MaxMinutes}min",
            _tokenExpirationMinutes, _maxExpirationMinutes);
    }

    /// <summary>
    /// Create a new token for a device, replacing any existing token.
    /// </summary>
    public DeviceToken CreateToken(string deviceUid, string userKeyField)
    {
        var token = new DeviceToken
        {
            DateCreated = DateTime.UtcNow,
            TokenValue = GenerateSecureToken(),
            DeviceUid = deviceUid,
            UserKeyField = userKeyField,
            Expiration = DateTime.UtcNow.AddMinutes(_tokenExpirationMinutes)
        };

        _tokens[deviceUid] = token;

        _logger.LogInformation(
            "Token created for device {DeviceUid}, expires {Expiration}. TokenStore now has {Count} tokens",
            deviceUid, token.Expiration, _tokens.Count);

        return token;
    }

    /// <summary>
    /// Validate a token for a given device.
    /// </summary>
    public TokenValidationResult ValidateToken(string deviceUid, string tokenValue)
    {
        _logger.LogDebug("ValidateToken called. DeviceUid={DeviceUid}, TokenStore has {Count} tokens",
            deviceUid, _tokens.Count);

        if (string.IsNullOrEmpty(tokenValue))
        {
            _logger.LogWarning("Empty token received for device {DeviceUid}", deviceUid);
            return TokenValidationResult.InvalidToken;
        }

        if (!_tokens.TryGetValue(deviceUid, out var storedToken))
        {
            _logger.LogWarning("No token found for device {DeviceUid}. Known devices: [{Devices}]",
                deviceUid, string.Join(", ", _tokens.Keys));
            return TokenValidationResult.InvalidToken;
        }

        if (storedToken.TokenValue != tokenValue)
        {
            _logger.LogWarning("Token mismatch for device {DeviceUid}", deviceUid);
            return TokenValidationResult.InvalidToken;
        }

        if (DateTime.UtcNow > storedToken.Expiration)
        {
            _logger.LogInformation("Token expired for device {DeviceUid}", deviceUid);
            return TokenValidationResult.TokenExpired;
        }

        return TokenValidationResult.Valid;
    }

    /// <summary>
    /// Refresh an expired token. The old token value must match the stored token,
    /// and the token must not have been expired longer than MaxExpiration.
    /// </summary>
    /// <returns>New DeviceToken if refresh succeeds, null otherwise.</returns>
    public DeviceToken? RefreshToken(string deviceUid, string oldTokenValue)
    {
        if (!_tokens.TryGetValue(deviceUid, out var storedToken))
        {
            _logger.LogWarning("RefreshToken: No token found for device {DeviceUid}", deviceUid);
            return null;
        }

        if (storedToken.TokenValue != oldTokenValue)
        {
            _logger.LogWarning("RefreshToken: Old token mismatch for device {DeviceUid}", deviceUid);
            return null;
        }

        // Check that the token hasn't been expired for longer than MaxExpiration
        var expiredDuration = DateTime.UtcNow - storedToken.Expiration;
        if (expiredDuration.TotalMinutes > _maxExpirationMinutes)
        {
            _logger.LogWarning(
                "RefreshToken: Token for device {DeviceUid} expired {Minutes} minutes ago, exceeds MaxExpiration of {MaxMinutes}",
                deviceUid, (int)expiredDuration.TotalMinutes, _maxExpirationMinutes);
            return null;
        }

        // Generate new token
        var newToken = new DeviceToken
        {
            DateCreated = DateTime.UtcNow,
            TokenValue = GenerateSecureToken(),
            DeviceUid = deviceUid,
            UserKeyField = storedToken.UserKeyField,
            Expiration = DateTime.UtcNow.AddMinutes(_tokenExpirationMinutes)
        };

        _tokens[deviceUid] = newToken;

        _logger.LogInformation(
            "Token refreshed for device {DeviceUid}, expires {Expiration}",
            deviceUid, newToken.Expiration);

        return newToken;
    }

    /// <summary>
    /// Get the current token for a device, or null if none exists.
    /// </summary>
    public DeviceToken? GetToken(string deviceUid)
    {
        _tokens.TryGetValue(deviceUid, out var token);
        return token;
    }

    /// <summary>
    /// Remove a token (e.g., on device deregistration).
    /// </summary>
    public bool RemoveToken(string deviceUid)
    {
        return _tokens.TryRemove(deviceUid, out _);
    }

    /// <summary>
    /// Generate a cryptographically secure 64-character hex string.
    /// </summary>
    private static string GenerateSecureToken()
    {
        var bytes = new byte[32]; // 32 bytes = 64 hex chars
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
