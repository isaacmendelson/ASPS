using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Common.Models;

namespace Common.Entities;

/// <summary>
/// Represents a device alert simulation configuration.
/// Contains a series of SimulationSteps stored as JSON in SimulationStepsJson field.
/// </summary>
public class Simulation : Entity
{
    private Tag? tag;

    public Simulation()
    {
        // Parameterless constructor for EF Core
    }

    public Simulation(string name, string description, string creatorKeyField)
    {
        Name = name;
        Description = description;
        CreatorKeyField = creatorKeyField;
        DateCreated = DateTime.UtcNow;
    }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Foreign Key: Link to the User who created this simulation
    /// </summary>
    [Required]
    public string CreatorKeyField { get; set; } = string.Empty;

    /// <summary>
    /// JSON representation of SimulationStep[] array
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? SimulationStepsJson { get; set; }

    // Navigation property to Creator User
    [ForeignKey(nameof(CreatorKeyField))]
    public User? Creator { get; set; }

    // Computed CreatorKey property for compatibility
    [NotMapped]
    public Key? CreatorKey
    {
        get => !string.IsNullOrEmpty(CreatorKeyField) ? new Key("User", CreatorKeyField) : null;
        set => CreatorKeyField = value?.Value ?? string.Empty;
    }

    [NotMapped]
    public override string TypeName => "Simulation";

    [NotMapped]
    public override Tag Tag
    {
        get
        {
            if (this.tag is not null)
                return this.tag;

            if (!string.IsNullOrEmpty(this.KeyField))
            {
                this.tag = this.CreateTag();
                return this.tag;
            }

            return this.CreateTag();
        }
    }

    protected virtual Tag CreateTag()
    {
        var name = string.IsNullOrWhiteSpace(Name) ? KeyField : Name;
        return new Tag(this.Key, name, this.TypeName);
    }
}
