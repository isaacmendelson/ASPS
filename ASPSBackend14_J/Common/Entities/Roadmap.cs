using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Stores a single roadmap as a JSON blob (items, categories, slides).
/// One row per roadmap project — supports multi-roadmap workflow.
/// </summary>
[Table("Roadmaps")]
public class Roadmap
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    /// <summary>Display name of the roadmap (e.g., "ASPS Master Roadmap").</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional short description shown in the index list.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Full roadmap state as JSON: { items, categories, slides, exportedAt }.
    /// Mirrors the localStorage format produced by the Export feature.
    /// </summary>
    [Required]
    [Column(TypeName = "longtext")]
    public string Data { get; set; } = "{\"items\":[],\"categories\":[],\"slides\":[]}";

    /// <summary>Increments on every save (used for optimistic concurrency).</summary>
    [Required]
    [ConcurrencyCheck]
    public int Version { get; set; } = 1;

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public DateTime? LastUpdatedAt { get; set; }

    [MaxLength(255)]
    public string? CreatedBy { get; set; }

    [MaxLength(255)]
    public string? LastUpdatedBy { get; set; }

    [Required]
    public bool IsArchived { get; set; } = false;
}
