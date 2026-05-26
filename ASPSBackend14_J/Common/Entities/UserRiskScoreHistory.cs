using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Persisted snapshot of <see cref="Common.Models.UserRiskScore"/> per user
/// per recompute. Each row is the full computed assessment at a point in
/// time. See docs/SCRUM-904-user-risk-score-design.md §8 ("Persistence").
///
/// Drives:
///   - trend graphs ("your risk has been rising"),
///   - guardian alerting on sustained rises,
///   - the auto-correction calibration loop (§7).
///
/// The denormalized scalar columns (Score, Level, axis scores, …) make
/// trend queries cheap; <see cref="SerializedSnapshot"/> holds the full
/// JSON of <see cref="Common.Models.UserRiskScore"/> for explainability +
/// replay (contributing-signal list, recommended actions, etc.).
/// </summary>
[Table("UserRiskScoreHistories")]
public class UserRiskScoreHistory
{
    /// <summary>Parameterless constructor for EF Core.</summary>
    public UserRiskScoreHistory() { }

    /// <summary>Primary key — INT AUTO_INCREMENT.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    /// <summary>
    /// User this snapshot belongs to — refers to <c>User.KeyField</c>.
    /// Indexed; also part of the composite (UserKey, ComputedAt DESC) index
    /// for the "latest URS per user" query.
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string UserKey { get; set; } = string.Empty;

    /// <summary>Headline 0–100 scalar at the time of computation.</summary>
    [Required]
    public int Score { get; set; }

    /// <summary>"Low" | "Elevated" | "High" | "Critical" — band at the time of computation.</summary>
    [Required]
    [MaxLength(20)]
    public string Level { get; set; } = "Low";

    /// <summary>Consent-aware confidence (0.0–1.0) at the time of computation.</summary>
    [Required]
    public double Confidence { get; set; }

    /// <summary>
    /// UTC timestamp when this snapshot was computed. Indexed; also part of
    /// the composite (UserKey, ComputedAt DESC) index used to fetch the
    /// latest URS per user.
    /// </summary>
    [Required]
    public DateTime ComputedAt { get; set; }

    /// <summary>Vulnerability axis 0–100.</summary>
    [Required]
    public double VulnerabilityScore { get; set; }

    /// <summary>Exposure axis 0–100.</summary>
    [Required]
    public double ExposureScore { get; set; }

    /// <summary>Live axis 0–100.</summary>
    [Required]
    public double LiveScore { get; set; }

    /// <summary>Correlation axis 0–100.</summary>
    [Required]
    public double CorrelationScore { get; set; }

    /// <summary>
    /// Full JSON serialization of the <see cref="Common.Models.UserRiskScore"/>
    /// object — contributing signals, dimension subscores, recommended
    /// actions, data-sources-active, explanation. Stored as LONGTEXT to
    /// accommodate large explainability payloads.
    /// </summary>
    [Required]
    public string SerializedSnapshot { get; set; } = string.Empty;
}
