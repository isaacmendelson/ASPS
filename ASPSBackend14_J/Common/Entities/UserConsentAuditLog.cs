using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;

namespace Common.Entities;

/// <summary>
/// Append-only audit log of every consent change — by user or by guardian.
/// Never updated after insert. See
/// docs/SCRUM-904-user-risk-score-design.md §3.5 ("Audit trail"). Needed for:
///   - legal defensibility,
///   - trust restoration ("I never enabled this" → here's the record),
///   - recognising patterns like consent-revocation-under-social-engineering.
/// </summary>
[Table("UserConsentAuditLog")]
public class UserConsentAuditLog
{
    /// <summary>Parameterless constructor for EF Core.</summary>
    private UserConsentAuditLog() { }

    /// <summary>
    /// Creates a new audit-log entry capturing an <paramref name="oldLevel"/>
    /// → <paramref name="newLevel"/> transition for one (user × source) pair.
    /// </summary>
    public UserConsentAuditLog(
        string userKey,
        DataSourceKind source,
        DataConsentLevel oldLevel,
        DataConsentLevel newLevel,
        string? changedBy,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));

        UserKey = userKey.Trim();
        Source = source;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? null : changedBy!.Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason!.Trim();
        ChangedAt = DateTime.UtcNow;
    }

    /// <summary>Primary key — INT AUTO_INCREMENT.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    /// <summary>UserKey of the user whose consent changed. Indexed.</summary>
    [Required]
    [MaxLength(36)]
    public string UserKey { get; set; } = string.Empty;

    /// <summary>The data source whose level changed (stored as string).</summary>
    [Required]
    public DataSourceKind Source { get; set; }

    /// <summary>Previous consent level (stored as string).</summary>
    [Required]
    public DataConsentLevel OldLevel { get; set; }

    /// <summary>New consent level (stored as string).</summary>
    [Required]
    public DataConsentLevel NewLevel { get; set; }

    /// <summary>UserKey of the actor who made the change — user or guardian. Null if a system default seed.</summary>
    [MaxLength(36)]
    public string? ChangedBy { get; set; }

    /// <summary>When the change happened (UTC).</summary>
    [Required]
    public DateTime ChangedAt { get; set; }

    /// <summary>Free-text reason (e.g. "user toggled in settings", "guardian approval granted").</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
