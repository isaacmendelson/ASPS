using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;

namespace Common.Entities;

/// <summary>
/// Per (user × data source) consent record. One row per user × source —
/// enforced by the composite unique index on (UserKey, Source) in
/// <c>AppDbContext.OnModelCreating</c>.
///
/// Absence of a row for a (user, source) pair means "no explicit choice yet"
/// and the system uses the default level for that source from
/// docs/SCRUM-904-user-risk-score-design.md §3.5. Defaults are NOT inserted
/// on read — see <c>ConsentService.GetLevelAsync</c>.
///
/// Every change is mirrored into <see cref="UserConsentAuditLog"/>.
/// </summary>
[Table("UserConsentPreferences")]
public class UserConsentPreferences
{
    /// <summary>
    /// Parameterless constructor for EF Core. Do not use directly — use the
    /// public constructor so timestamps are stamped.
    /// </summary>
    private UserConsentPreferences() { }

    /// <summary>
    /// Creates a new consent record. <paramref name="grantedBy"/> may be the
    /// user themselves (their own UserKey) or a guardian (the guardian's
    /// UserKey). Null means "system default" — typically only used for the
    /// initial seed of explicit defaults if/when we choose to materialize
    /// them.
    /// </summary>
    public UserConsentPreferences(string userKey, DataSourceKind source, DataConsentLevel level, string? grantedBy)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be empty", nameof(userKey));

        UserKey = userKey.Trim();
        Source = source;
        Level = level;
        GrantedBy = string.IsNullOrWhiteSpace(grantedBy) ? null : grantedBy!.Trim();
        var now = DateTime.UtcNow;
        GrantedAt = now;
        LastModified = now;
    }

    /// <summary>Primary key — INT AUTO_INCREMENT. Stored as column "Key".</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    /// <summary>
    /// The user this preference belongs to — refers to
    /// <c>UserDevice.UserKey</c> / <c>User.KeyField</c>. Indexed.
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string UserKey { get; set; } = string.Empty;

    /// <summary>
    /// The data source being configured. Stored as a string in the database
    /// (see EF config) for forward compatibility — adding a new source is
    /// non-breaking.
    /// </summary>
    [Required]
    public DataSourceKind Source { get; set; }

    /// <summary>
    /// The current consent level for this (user × source) pair. Stored as a
    /// string in the database.
    /// </summary>
    [Required]
    public DataConsentLevel Level { get; set; }

    /// <summary>When this level became effective (UTC).</summary>
    [Required]
    public DateTime GrantedAt { get; set; }

    /// <summary>When this row was last modified (UTC).</summary>
    [Required]
    public DateTime LastModified { get; set; }

    /// <summary>
    /// UserKey of whoever set this level — the user themselves, or a
    /// guardian. Null for system defaults if materialized.
    /// </summary>
    [MaxLength(36)]
    public string? GrantedBy { get; set; }

    /// <summary>
    /// Update this row to a new level. Caller is responsible for writing the
    /// matching <see cref="UserConsentAuditLog"/> entry — see
    /// <c>ConsentService.SetLevelAsync</c>.
    /// </summary>
    public void UpdateLevel(DataConsentLevel newLevel, string? changedBy)
    {
        Level = newLevel;
        GrantedBy = string.IsNullOrWhiteSpace(changedBy) ? null : changedBy!.Trim();
        LastModified = DateTime.UtcNow;
    }
}
