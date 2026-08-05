using System.Text.Json;
using Common.Generated.Messaging.V1;
using FluentAssertions;

namespace ASPS.Tests.Common.Messaging;

public class MessageEnvelopeValidatorTests
{
    [Fact]
    public void Validate_ValidBrowserRequest_ReturnsValid()
    {
        var result = MessageEnvelopeValidator.Validate(CreateRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnsupportedMajorVersion_ReturnsExplicitProtocolError()
    {
        var result = MessageEnvelopeValidator.Validate(CreateRequest() with { SchemaVersion = "2.0" });
        result.ErrorCode.Should().Be("protocol.unsupported_schema_version");
    }

    [Fact]
    public void Validate_AdditiveMinorVersion_IsAccepted()
    {
        MessageEnvelopeValidator.Validate(CreateRequest() with { SchemaVersion = "1.7" })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DefaultSentAt_ReturnsExplicitProtocolError()
    {
        MessageEnvelopeValidator.Validate(CreateRequest() with { SentAt = default })
            .ErrorCode.Should().Be("protocol.invalid_sent_at");
    }

    [Fact]
    public void Serialize_SentAt_UsesUtcZSuffixWithMillisecondPrecision()
    {
        var envelope = CreateRequest() with { SentAt = new DateTimeOffset(2026, 8, 5, 20, 2, 59, 603, TimeSpan.Zero) };

        var json = JsonSerializer.Serialize(envelope);
        using var document = JsonDocument.Parse(json);
        var sentAt = document.RootElement.GetProperty("sentAt").GetString();

        sentAt.Should().Be("2026-08-05T20:02:59.603Z");
    }

    [Fact]
    public void Deserialize_SentAt_AcceptsBothZAndOffsetNotation()
    {
        const string zJson = """
        {"schemaVersion":"1.0","messageId":"4b2a90c9-4b50-40e8-8969-f594b9fde602","correlationId":"6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f","requestId":"7afe6cba-7916-40e1-91dc-666f40f760db","messageType":"url_scan.request","sentAt":"2026-08-05T20:02:59.603Z","source":"desktop","context":{"deviceId":"d","tabId":"1","url":"https://example.com/"},"outcome":null,"payload":{}}
        """;
        const string offsetJson = """
        {"schemaVersion":"1.0","messageId":"4b2a90c9-4b50-40e8-8969-f594b9fde602","correlationId":"6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f","requestId":"7afe6cba-7916-40e1-91dc-666f40f760db","messageType":"url_scan.request","sentAt":"2026-08-05T20:02:59.6033682+00:00","source":"desktop","context":{"deviceId":"d","tabId":"1","url":"https://example.com/"},"outcome":null,"payload":{}}
        """;

        var zEnvelope = JsonSerializer.Deserialize<MessageEnvelopeV1>(zJson);
        var offsetEnvelope = JsonSerializer.Deserialize<MessageEnvelopeV1>(offsetJson);

        zEnvelope!.SentAt.Offset.Should().Be(TimeSpan.Zero);
        offsetEnvelope!.SentAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Validate_UndefinedPayload_ReturnsExplicitProtocolError()
    {
        MessageEnvelopeValidator.Validate(CreateRequest() with { Payload = default })
            .ErrorCode.Should().Be("protocol.invalid_payload");
    }

    [Fact]
    public void Deserialize_EveryMissingRequiredField_IsRejectedBeforeDomainHandling()
    {
        const string json = """
        {"schemaVersion":"1.0","messageId":"4b2a90c9-4b50-40e8-8969-f594b9fde602","correlationId":"6b7a9fa7-e3f0-4c5c-86cc-3914f42b262f","requestId":"7afe6cba-7916-40e1-91dc-666f40f760db","messageType":"url_scan.request","sentAt":"2026-07-28T10:15:30.123Z","source":"desktop","context":{"deviceId":"d","tabId":"1","url":"https://example.com/"},"outcome":null,"payload":{}}
        """;
        using var document = JsonDocument.Parse(json);
        foreach (var field in document.RootElement.EnumerateObject().Select(p => p.Name))
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            node.Remove(field);
            MessageEnvelopeValidator.DeserializeAndValidate(node.ToJsonString(), out _)
                .IsValid.Should().BeFalse(field);
        }
    }

    [Fact]
    public void Validate_NonCanonicalUrl_ReturnsExplicitValidationError()
    {
        var result = MessageEnvelopeValidator.Validate(CreateRequest("HTTPS://Example.COM:443"));
        result.ErrorCode.Should().Be("validation.canonical_url_mismatch");
    }

    [Fact]
    public void Validate_ErrorOutcomeWithResult_ReturnsProtocolError()
    {
        using var document = JsonDocument.Parse("{}");
        var envelope = CreateRequest() with
        {
            MessageType = "url_scan.error",
            Outcome = new MessageOutcomeV1 { Status = "error", Error = new MessageErrorV1 { Code = "analyzer.timeout", Message = "timeout" }, Result = document.RootElement.Clone() }
        };
        MessageEnvelopeValidator.Validate(envelope).ErrorCode.Should().Be("protocol.invalid_outcome");
    }

    [Fact]
    public void ValidateEcho_ChangedImmutableUrl_ReturnsExplicitValidationError()
    {
        var request = CreateRequest();
        var response = CreateSuccess(request) with { Context = new MessageContextV1 { DeviceId = "device-1", TabId = "12", Url = "https://example.com/other" } };
        MessageEnvelopeValidator.ValidateEcho(request, response).ErrorCode.Should().Be("validation.immutable_context_mismatch");
    }

    [Theory]
    [InlineData("https://Example.COM:443")]
    [InlineData("http://example.com:80")]
    public void CanonicalizeUrl_ReturnsCanonicalUrl(string source)
    {
        var result = MessageEnvelopeValidator.CanonicalizeUrl(source);
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().Be(source.StartsWith("https") ? "https://example.com/" : "http://example.com/");
    }

    private static MessageEnvelopeV1 CreateRequest(string url = "https://example.com/")
    {
        using var document = JsonDocument.Parse("{}");
        return new MessageEnvelopeV1
        {
            MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), RequestId = Guid.NewGuid(),
            MessageType = "url_scan.request", SentAt = DateTimeOffset.UtcNow, Source = "desktop",
            Context = new MessageContextV1 { DeviceId = "device-1", TabId = "12", Url = url }, Payload = document.RootElement.Clone()
        };
    }

    private static MessageEnvelopeV1 CreateSuccess(MessageEnvelopeV1 request)
    {
        using var document = JsonDocument.Parse("{}");
        return request with
        {
            MessageId = Guid.NewGuid(), MessageType = "url_scan.result", Source = "backend",
            Outcome = new MessageOutcomeV1 { Status = "success", Result = document.RootElement.Clone() }
        };
    }
}
