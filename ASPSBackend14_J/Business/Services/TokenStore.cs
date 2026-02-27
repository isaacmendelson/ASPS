using System.Collections.Concurrent;
using System.Security.Cryptography;
using Business.Data.EF;
using Common.Entities;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Business.Services;

/// <summary>
/// Write-through token store: in-memory ConcurrentDictionary for fast lookups,
/// persisted to DeviceTokens DB table for durability across restarts.
/// </summary>
public class TokenStore
{
    private readonly ConcurrentDictionary<string, DeviceToken> _tokens = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenStore> _logger;
    private readonly int _tokenExpirationMinutes;
    private readonly int _maxExpirationMinutes;

    public TokenStore(IConfiguration configuration, ILogger<TokenStore> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _tokenExpirationMinutes = configuration.GetValue<int>("TokenManagement:TokenExpirationPeriod", 1440);
        _maxExpirationMinutes = configuration.GetValue<int>("TokenManagement:MaxExpiration", 10080);

        _logger.LogInformation(
            "TokenStore initialized. ExpirationPeriod={ExpirationMinutes}min, MaxExpiration={MaxMinutes}min",
            _tokenExpirationMinutes, _maxExpirationMinutes);
    }

    /// <summary>
    /// Load tokens from database into memory on startup.
    /// </summary>
    public async Task LoadFromDatabaseAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entities = await db.DeviceTokens.ToListAsync();

            foreach (var entity in entities)
            {
                _tokens[entity.DeviceUid] = new DeviceToken
                {
                    DeviceUid = entity.DeviceUid,
                    TokenValue = entity.TokenValue,
                    UserKeyField = entity.UserKeyField,
                    DateCreated = entity.DateCreated,
                    Expiration = entity.Expiration
                };
            }

            _logger.LogInformation("TokenStore loaded {Count} tokens from database", entities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tokens from database - starting with empty store");
        }
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
        PersistTokenAsync(token);

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

        // Constant-time comparison to prevent timing attacks
        var storedBytes = Convert.FromHexString(storedToken.TokenValue);
        var inputBytes  = Convert.FromHexString(tokenValue);
        if (!CryptographicOperations.FixedTimeEquals(storedBytes, inputBytes))
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

        // Constant-time comparison to prevent timing attacks
        var storedBytes = Convert.FromHexString(storedToken.TokenValue);
        var inputBytes  = Convert.FromHexString(oldTokenValue);
        if (!CryptographicOperations.FixedTimeEquals(storedBytes, inputBytes))
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
        PersistTokenAsync(newToken);

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
        var removed = _tokens.TryRemove(deviceUid, out _);
        if (removed)
        {
            RemoveTokenFromDbAsync(deviceUid);
        }
        return removed;
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

    private void PersistTokenAsync(DeviceToken token)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var existing = await db.DeviceTokens.FindAsync(token.DeviceUid);
                if (existing != null)
                {
                    existing.TokenValue = token.TokenValue;
                    existing.UserKeyField = token.UserKeyField;
                    existing.DateCreated = token.DateCreated;
                    existing.Expiration = token.Expiration;
                }
                else
                {
                    db.DeviceTokens.Add(new DeviceTokenEntity
                    {
                        DeviceUid = token.DeviceUid,
                        TokenValue = token.TokenValue,
                        UserKeyField = token.UserKeyField,
                        DateCreated = token.DateCreated,
                        Expiration = token.Expiration
                    });
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist token for device {DeviceUid}", token.DeviceUid);
            }
        });
    }

    private void RemoveTokenFromDbAsync(string deviceUid)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var existing = await db.DeviceTokens.FindAsync(deviceUid);
                if (existing != null)
                {
                    db.DeviceTokens.Remove(existing);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove token from DB for device {DeviceUid}", deviceUid);
            }
        });
    }
}
