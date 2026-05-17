using Common.Messaging;

namespace Business.Commands;

/// <summary>
/// Command to add a tracked domain and synchronize it to all of the
/// user's devices. ASPS-371. Persists a TrackedDomain row and raises a
/// SetTrackedDomains domain event (handled by NotificationPublisherActor,
/// which fans it out to every device topic → agent → extension).
/// </summary>
public class AddTrackedDomainCommand : Command
{
    public AddTrackedDomainCommand()
    {
        CommandType = nameof(AddTrackedDomainCommand);
    }

    public string CommandType { get; set; }

    /// <summary>Domain or root domain to track (e.g. "evil-bank.example").</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Free-text category (Banking / Crypto / Phishing / …).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// User whose devices receive this domain. Empty = no device fan-out
    /// (row is still persisted; the event is published to the user topic only).
    /// </summary>
    public string UserKeyField { get; set; } = string.Empty;

    /// <summary>Optional scam-in-progress correlation key.</summary>
    public string ScamInProgressKey { get; set; } = string.Empty;

    /// <summary>
    /// Track mode as Common.Enums.TrackMode int (0=None, 1=Surf, 2=Click).
    /// Defaults to Surf when omitted/zero.
    /// </summary>
    public int TrackMode { get; set; } = 1;

    public string? Reason { get; set; }
}

public class AddTrackedDomainCommandResult : CommandResult
{
    public int? TrackedDomainId { get; set; }
}
