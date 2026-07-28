using System.Text.Json;

namespace Common.Generated.Messaging.V1;

public sealed record MessageIdentityV1(
    Guid MessageId,
    Guid CorrelationId,
    Guid RequestId,
    string DeviceId,
    string? TabId,
    string Url);

public static class MessageEnvelopeFactoryV1
{
    public static MessageEnvelopeV1 CreateSuccess(MessageIdentityV1 identity, object result) =>
        new()
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = identity.CorrelationId,
            RequestId = identity.RequestId,
            MessageType = "url_scan.result",
            SentAt = DateTimeOffset.UtcNow,
            Source = "backend",
            Context = Context(identity),
            Outcome = new MessageOutcomeV1
            {
                Status = "success",
                Result = JsonSerializer.SerializeToElement(result)
            },
            Payload = JsonSerializer.SerializeToElement(new { })
        };

    public static MessageEnvelopeV1 CreateError(
        MessageIdentityV1 identity, string code, string message, bool retryable = false) =>
        new()
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = identity.CorrelationId,
            RequestId = identity.RequestId,
            MessageType = "url_scan.error",
            SentAt = DateTimeOffset.UtcNow,
            Source = "backend",
            Context = Context(identity),
            Outcome = new MessageOutcomeV1
            {
                Status = "error",
                Error = new MessageErrorV1 { Code = code, Message = message, Retryable = retryable }
            },
            Payload = JsonSerializer.SerializeToElement(new { })
        };

    private static MessageContextV1 Context(MessageIdentityV1 identity) => new()
    {
        DeviceId = identity.DeviceId,
        TabId = identity.TabId,
        Url = identity.Url
    };
}
