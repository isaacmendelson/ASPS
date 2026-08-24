using System.Text.RegularExpressions;
using System.Text.Json;

namespace Common.Generated.Messaging.V1;

public sealed record MessagingEnvelopeValidationResult(bool IsValid, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static readonly MessagingEnvelopeValidationResult Valid = new(true);
}

/// <summary>Strict runtime validation for a v1 envelope before domain handling.</summary>
public static partial class MessageEnvelopeValidator
{
    private static readonly string[] RequiredFields =
    {
        "schemaVersion", "messageId", "correlationId", "requestId", "messageType",
        "sentAt", "source", "context", "outcome", "payload"
    };
    private static readonly HashSet<string> MessageTypes = new(StringComparer.Ordinal)
    {
        "url_scan.request", "url_scan.accepted", "url_scan.result", "url_scan.error",
        "track_url.request", "track_url.accepted", "track_url.result", "track_url.error",
        "tab_closed.request", "tab_closed.accepted", "tab_closed.result", "tab_closed.error",
        "tab_changed.request", "tab_changed.accepted", "tab_changed.result", "tab_changed.error",
        "remote_access.request", "remote_access.accepted", "remote_access.result", "remote_access.error"
    };
    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal)
    {
        "extension", "desktop", "backend", "analyzer"
    };

    public static MessagingEnvelopeValidationResult Validate(MessageEnvelopeV1? envelope, bool requireDeviceId = false)
    {
        if (envelope is null) return Invalid("protocol.malformed_envelope", "Envelope is required.");
        if (!Version.TryParse(envelope.SchemaVersion, out var schemaVersion) ||
            schemaVersion.Major != 1 || schemaVersion.Minor < 0)
            return Invalid("protocol.unsupported_schema_version", "Only schema major 1 is supported.");
        if (envelope.MessageId == Guid.Empty || envelope.CorrelationId == Guid.Empty || envelope.RequestId == Guid.Empty)
            return Invalid("protocol.invalid_identifier", "messageId, correlationId and requestId must be UUIDs.");
        if (!MessageTypes.Contains(envelope.MessageType)) return Invalid("protocol.unknown_message_type", "messageType is not supported.");
        if (!Sources.Contains(envelope.Source)) return Invalid("protocol.invalid_source", "source is not supported.");
        if (envelope.SentAt == default || envelope.SentAt.Offset != TimeSpan.Zero)
            return Invalid("protocol.invalid_sent_at", "sentAt must be a non-default UTC timestamp.");
        if (envelope.Context is null) return Invalid("protocol.missing_context", "context is required.");
        if (requireDeviceId && string.IsNullOrWhiteSpace(envelope.Context.DeviceId))
            return Invalid("validation.missing_device_id", "deviceId is required at this boundary.");
        if (envelope.Context.TabId is { Length: > 0 } tabId && !DecimalString().IsMatch(tabId))
            return Invalid("validation.invalid_tab_id", "tabId must be an opaque decimal string.");
        if (envelope.Payload.ValueKind != System.Text.Json.JsonValueKind.Object)
            return Invalid("protocol.invalid_payload", "payload must be an object.");
        var canonical = CanonicalizeUrl(envelope.Context.Url);
        if (!canonical.IsValid) return canonical;
        if (!string.Equals(envelope.Context.Url, canonical.ErrorMessage, StringComparison.Ordinal))
            return Invalid("validation.canonical_url_mismatch", "context.url must already be canonical.");
        return ValidateOutcome(envelope);
    }

    public static MessagingEnvelopeValidationResult DeserializeAndValidate(
        string json, out MessageEnvelopeV1? envelope, bool requireDeviceId = false)
    {
        envelope = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                RequiredFields.Any(field => !document.RootElement.TryGetProperty(field, out _)))
                return Invalid("protocol.malformed_envelope", "A required envelope field is missing.");
            envelope = JsonSerializer.Deserialize<MessageEnvelopeV1>(json);
            return Validate(envelope, requireDeviceId);
        }
        catch (JsonException)
        {
            return Invalid("protocol.malformed_envelope", "Envelope JSON is malformed.");
        }
    }

    public static MessagingEnvelopeValidationResult ValidateEcho(MessageEnvelopeV1 request, MessageEnvelopeV1 response)
    {
        var validation = Validate(response, !string.IsNullOrWhiteSpace(request.Context.DeviceId));
        if (!validation.IsValid) return validation;
        return request.SchemaVersion == response.SchemaVersion && request.CorrelationId == response.CorrelationId &&
               request.RequestId == response.RequestId && request.Context.DeviceId == response.Context.DeviceId &&
               request.Context.TabId == response.Context.TabId && request.Context.Url == response.Context.Url
            ? MessagingEnvelopeValidationResult.Valid
            : Invalid("validation.immutable_context_mismatch", "Response does not echo immutable request context.");
    }

    public static MessagingEnvelopeValidationResult CanonicalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Invalid("validation.invalid_canonical_url", "URL must be an absolute HTTP or HTTPS URL.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) return Invalid("validation.invalid_canonical_url", "URL user-info is forbidden.");
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0) return Invalid("validation.invalid_canonical_url", "URL host is required.");
        var defaultPort = (uri.Scheme == Uri.UriSchemeHttp && uri.Port == 80) || (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443);
        var authority = host.Contains(':') ? $"[{host}]" : host;
        if (!defaultPort) authority += $":{uri.Port}";
        var path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        return new(true, null, $"{uri.Scheme.ToLowerInvariant()}://{authority}{path}{uri.Query}");
    }

    private static MessagingEnvelopeValidationResult ValidateOutcome(MessageEnvelopeV1 envelope)
    {
        // Generalized by messageType suffix (".result" / ".error") rather than a hardcoded
        // "url_scan" family name, so every alert family added to MessageTypes above gets the
        // same outcome-shape validation for free.
        var isResult = envelope.MessageType.EndsWith(".result", StringComparison.Ordinal);
        var isError = envelope.MessageType.EndsWith(".error", StringComparison.Ordinal);
        var requiresOutcome = isResult || isError;
        if (!requiresOutcome) return envelope.Outcome is null ? MessagingEnvelopeValidationResult.Valid : Invalid("protocol.invalid_outcome", "Request must have a null outcome.");
        if (envelope.Outcome is null) return Invalid("protocol.missing_outcome", "Result messages require an outcome.");
        var success = envelope.Outcome.Status == "success";
        var error = envelope.Outcome.Status == "error";
        if (isResult && !success || isError && !error)
            return Invalid("protocol.invalid_outcome", "outcome.status does not match messageType.");
        if (!success && !error) return Invalid("protocol.invalid_outcome", "outcome.status must be success or error.");
        if (success == (envelope.Outcome.Result is null) || error == (envelope.Outcome.Error is null))
            return Invalid("protocol.invalid_outcome", "outcome must contain exactly its matching member.");
        if (error && (string.IsNullOrWhiteSpace(envelope.Outcome.Error!.Code) || string.IsNullOrWhiteSpace(envelope.Outcome.Error.Message)))
            return Invalid("protocol.invalid_outcome", "error code and message are required.");
        return MessagingEnvelopeValidationResult.Valid;
    }

    private static MessagingEnvelopeValidationResult Invalid(string code, string message) => new(false, code, message);
    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)] private static partial Regex DecimalString();
}
