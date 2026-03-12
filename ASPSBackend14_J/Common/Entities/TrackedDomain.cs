using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Represents a tracked domain for identifying tracking/analytics services.
/// Uses INT AUTO_INCREMENT key for performance.
/// </summary>
[Table("TrackedDomains")]
public class TrackedDomain
{
    /// <summary>
    /// Parameterless constructor for EF Core
    /// </summary>
    protected TrackedDomain() { }

    /// <summary>
    /// Creates a new tracked domain entry
    /// </summary>
    /// <param name="domain">The domain to track (e.g., "google-analytics.com")</param>
    /// <param name="category">Category (e.g., "Analytics", "Advertising", "Social")</param>
    public TrackedDomain(string domain, string category)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain cannot be empty", nameof(domain));
        
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));

        Domain = domain.ToLowerInvariant().Trim();
        Category = category.Trim();
        IsActive = true;
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Primary key - INT AUTO_INCREMENT
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    /// <summary>
    /// The tracked domain (normalized to lowercase)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Category of tracking (Analytics, Advertising, Social, etc.)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Whether this tracking domain is actively monitored
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this entry was created
    /// </summary>
    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this entry was last modified
    /// </summary>
    [Required]
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this entry was soft-deleted (NULL if active)
    /// </summary>
    public DateTime? DateDeleted { get; set; }

    /// <summary>
    /// Soft delete this entry
    /// </summary>
    public void Delete()
    {
        DateDeleted = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Update category or active status
    /// </summary>
    public void Update(string? category = null, bool? isActive = null)
    {
        if (!string.IsNullOrWhiteSpace(category))
            Category = category.Trim();
        
        if (isActive.HasValue)
            IsActive = isActive.Value;
        
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if this entry is active (not deleted and enabled)
    /// </summary>
    public bool IsEnabled => DateDeleted == null && IsActive;
}
