using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Business.Messaging;

public sealed class CqrsChannelSecurityOptions
{
    public static readonly string[] GatewayCommandTypes =
    {
        "CreateUserAdminCommand", "CreateUserDeviceCommand", "DeleteUserCommand",
        "ReInitializeASViewCommand", "CreateSimulationCommand", "UpdateSimulationCommand",
        "DeleteSimulationCommand", "RunSimulationCommand", "CreateWebsiteCategoryCommand",
        "UpdateWebsiteCategoryCommand", "AddTrackedDomainCommand", "UpdateTrackedDomainCommand",
        "DeleteTrackedDomainCommand", "CreateRoadmapCommand", "SaveRoadmapCommand",
        "UpdateRoadmapMetadataCommand", "ArchiveRoadmapCommand", "CreateUserCommand",
        "UpdateUserCommand", "UpdateUserDeviceCommand", "DeleteUserDeviceCommand"
    };

    public string ClientId { get; init; } = "asps-webapi";
    public string SharedSecret { get; init; } = string.Empty;
    public IReadOnlySet<string> AllowedClientIds { get; init; } =
        new HashSet<string>(StringComparer.Ordinal) { "asps-webapi" };
    public IReadOnlySet<string> AllowedCommands { get; init; } =
        new HashSet<string>(GatewayCommandTypes, StringComparer.Ordinal);
    public TimeSpan MaximumClockSkew { get; init; } = TimeSpan.FromMinutes(2);
}

public sealed class CqrsRequestEnvelope
{
    public int Version { get; init; } = 1;
    public string ClientId { get; init; } = string.Empty;
    public long TimestampUnixSeconds { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
}

public sealed class CqrsChannelSecurity
{
    private readonly CqrsChannelSecurityOptions _options;
    private readonly byte[] _key;
    private readonly ConcurrentDictionary<string, long> _acceptedNonces = new(StringComparer.Ordinal);

    public CqrsChannelSecurity(CqrsChannelSecurityOptions options)
    {
        _options = options;
        if (string.IsNullOrWhiteSpace(options.SharedSecret))
            throw new InvalidOperationException("CQRS:SharedSecret must be configured.");

        _key = Encoding.UTF8.GetBytes(options.SharedSecret);
        if (_key.Length < 32)
            throw new InvalidOperationException("CQRS:SharedSecret must contain at least 32 UTF-8 bytes.");
    }

    public string Protect(string payload)
    {
        var envelope = new CqrsRequestEnvelope
        {
            ClientId = _options.ClientId,
            TimestampUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            Payload = payload
        };
        envelope = new CqrsRequestEnvelope
        {
            Version = envelope.Version,
            ClientId = envelope.ClientId,
            TimestampUnixSeconds = envelope.TimestampUnixSeconds,
            Nonce = envelope.Nonce,
            Payload = envelope.Payload,
            Signature = Sign(envelope)
        };
        return JsonConvert.SerializeObject(envelope);
    }

    public bool TryUnprotect(string json, out string payload, out string clientId, out string error)
    {
        payload = string.Empty;
        clientId = string.Empty;
        error = "Invalid authenticated CQRS envelope.";
        CqrsRequestEnvelope? envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<CqrsRequestEnvelope>(
                json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null || envelope.Version != 1 ||
            string.IsNullOrWhiteSpace(envelope.ClientId) ||
            string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.Payload) ||
            string.IsNullOrWhiteSpace(envelope.Signature))
            return false;

        if (!_options.AllowedClientIds.Contains(envelope.ClientId))
        {
            error = "CQRS client is not authorized.";
            return false;
        }

        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - envelope.TimestampUnixSeconds);
        if (age > _options.MaximumClockSkew.TotalSeconds)
        {
            error = "CQRS request timestamp is outside the allowed window.";
            return false;
        }

        byte[] supplied;
        try { supplied = Convert.FromHexString(envelope.Signature); }
        catch (FormatException) { return false; }
        var expected = Convert.FromHexString(Sign(envelope));
        if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
            return false;

        if (!_acceptedNonces.TryAdd($"{envelope.ClientId}:{envelope.Nonce}", envelope.TimestampUnixSeconds))
        {
            error = "CQRS request nonce has already been used.";
            return false;
        }
        foreach (var accepted in _acceptedNonces)
        {
            if (accepted.Value < DateTimeOffset.UtcNow.Subtract(_options.MaximumClockSkew).ToUnixTimeSeconds())
                _acceptedNonces.TryRemove(accepted.Key, out _);
        }

        payload = envelope.Payload;
        clientId = envelope.ClientId;
        error = string.Empty;
        return true;
    }

    public bool IsCommandAuthorized(string commandType) =>
        _options.AllowedCommands.Contains(commandType);

    private string Sign(CqrsRequestEnvelope envelope)
    {
        var canonical = string.Join('\n', envelope.Version, envelope.ClientId,
            envelope.TimestampUnixSeconds, envelope.Nonce, envelope.Payload);
        return Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonical)));
    }
}
