using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Represents a sensitive site configuration for enhanced monitoring.
/// Sensitive sites include banking, crypto, government, and trading platforms
/// that require special attention in security analysis.
/// </summary>
[Table("SensitiveSites")]
public class SensitiveSite
{
    /// <summary>
    /// Parameterless constructor for EF Core
    /// </summary>
    protected SensitiveSite() { }

    /// <summary>
    /// Creates a new sensitive site entry
    /// </summary>
    /// <param name="domainPattern">Domain or URL pattern (e.g., "bankleumi.co.il", "*.paypal.com")</param>
    /// <param name="category">Category: Banking, Crypto, Government, Trading</param>
    /// <param name="riskMultiplier">Risk weight multiplier (default 2.0)</param>
    public SensitiveSite(string domainPattern, string category, decimal riskMultiplier = 2.0m)
    {
        if (string.IsNullOrWhiteSpace(domainPattern))
            throw new ArgumentException("Domain pattern cannot be empty", nameof(domainPattern));
        
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));

        if (riskMultiplier <= 0)
            throw new ArgumentException("Risk multiplier must be positive", nameof(riskMultiplier));

        DomainPattern = domainPattern.ToLowerInvariant().Trim();
        Category = category.Trim();
        RiskMultiplier = riskMultiplier;
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
    /// Domain or URL pattern to match (normalized to lowercase)
    /// Supports wildcards: *.paypal.com, bankleumi.co.il
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string DomainPattern { get; set; } = string.Empty;

    /// <summary>
    /// Category of sensitive site:
    /// - Banking: Bank Leumi, PayPal
    /// - Crypto: Binance, Coinbase
    /// - Government: gov.il, Tax Authority
    /// - Trading: eToro, Interactive Brokers
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Risk weight multiplier for this site category
    /// Higher values increase alert priority (typically 1.5-3.0)
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal RiskMultiplier { get; set; } = 2.0m;

    /// <summary>
    /// Whether this sensitive site is actively monitored
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
    /// Update sensitive site configuration
    /// </summary>
    public void Update(string? category = null, decimal? riskMultiplier = null, bool? isActive = null)
    {
        if (!string.IsNullOrWhiteSpace(category))
            Category = category.Trim();
        
        if (riskMultiplier.HasValue)
        {
            if (riskMultiplier.Value <= 0)
                throw new ArgumentException("Risk multiplier must be positive", nameof(riskMultiplier));
            RiskMultiplier = riskMultiplier.Value;
        }

        if (isActive.HasValue)
            IsActive = isActive.Value;
        
        DateModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if this entry is active (not deleted and enabled)
    /// </summary>
    public bool IsEnabled => DateDeleted == null && IsActive;

    /// <summary>
    /// Check if a URL matches this domain pattern
    /// Supports exact match and wildcard patterns (*.domain.com)
    /// </summary>
    public bool MatchesDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var urlLower = url.ToLowerInvariant();
        
        // Extract domain from URL
        var domain = ExtractDomain(urlLower);
        if (string.IsNullOrEmpty(domain))
            return false;

        // Wildcard pattern (*.example.com)
        if (DomainPattern.StartsWith("*."))
        {
            var baseDomain = DomainPattern[2..]; // Remove "*."
            return domain.EndsWith(baseDomain) || domain == baseDomain;
        }

        // Exact match
        return domain == DomainPattern || domain.EndsWith("." + DomainPattern);
    }

    /// <summary>
    /// Extract domain from URL string
    /// </summary>
    private static string ExtractDomain(string url)
    {
        try
        {
            // Handle URLs without protocol
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;

            var uri = new Uri(url);
            return uri.Host.ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
