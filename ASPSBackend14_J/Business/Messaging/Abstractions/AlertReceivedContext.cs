namespace Business.Messaging.Abstractions;

/// <summary>
/// Transport-agnostic context passed from alert ingress to business logic.
/// </summary>
public record AlertReceivedContext(
    string RawJson,
    string? ClientIdentity,
    DateTimeOffset ReceivedAt
);
