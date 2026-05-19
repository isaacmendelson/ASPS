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
    public string? ScamInProgressKey { get; set; }

    public string? AnalysisKey { get; set; }

    /// <summary>
    /// Track mode as Common.Enums.TrackMode int (0=None, 1=Surf, 2=Click).
    /// Defaults to Surf when omitted/zero.
    /// </summary>
    public int TrackMode { get; set; } = 1;

    public string? Reason { get; set; }

    /// <summary>
    /// When true, fan the SetTrackedDomains event out to EVERY user's devices
    /// (admin "Notify all user devices" action), ignoring UserKeyField for
    /// targeting. The row's UserKey is still persisted as-is.
    /// </summary>
    public bool NotifyAllUsers { get; set; }

    /// <summary>
    /// When true, the row is NOT (re-)persisted — only the SetTrackedDomains
    /// event is raised. Used by the admin "Notify" actions on existing rows.
    /// </summary>
    public bool NotifyOnly { get; set; }
}

public class AddTrackedDomainCommandResult : CommandResult
{
    public int? TrackedDomainId { get; set; }
}

/// <summary>Edit an existing TrackedDomain row (ASPS-371). Raises
/// TrackedDomainChanged and re-syncs the domain to the user's devices.</summary>
public class UpdateTrackedDomainCommand : Command
{
    public UpdateTrackedDomainCommand()
    {
        CommandType = nameof(UpdateTrackedDomainCommand);
    }

    public string CommandType { get; set; }

    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? UserKeyField { get; set; }
    public string? ScamInProgressKey { get; set; }
    public int TrackMode { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? Reason { get; set; }
}

public class UpdateTrackedDomainCommandResult : CommandResult
{
    public int? TrackedDomainId { get; set; }
}

/// <summary>Soft-delete a TrackedDomain row (ASPS-371). Raises
/// TrackedDomainDeleted.</summary>
public class DeleteTrackedDomainCommand : Command
{
    public DeleteTrackedDomainCommand()
    {
        CommandType = nameof(DeleteTrackedDomainCommand);
    }

    public string CommandType { get; set; }

    public int Id { get; set; }
    public string? Reason { get; set; }
}

public class DeleteTrackedDomainCommandResult : CommandResult
{
}
