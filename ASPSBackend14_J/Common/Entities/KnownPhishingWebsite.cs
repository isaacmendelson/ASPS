using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Represents a known phishing website stored in the database for URL analysis.
/// Uses INT AUTO_INCREMENT key instead of GUID for performance.
/// </summary>
[Table("KnownPhishingWebsites")]
public class KnownPhishingWebsite
{
    private string? _domain = null;

    /// <summary>
    /// Parameterless constructor for EF Core
    /// </summary>
    protected KnownPhishingWebsite() { }

    /// <summary>
    /// Creates a new known phishing website entry
    /// </summary>
    /// <param name="url">The phishing URL</param>
    /// <param name="source">Optional source of the URL (e.g., "PhishTank", "OpenPhish")</param>
    public KnownPhishingWebsite(string url, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));

        Url = url;
        Source = source ?? string.Empty;
        DateCreated = DateTime.UtcNow;
        
        // Pre-compute domain on creation
        Domain = GetDomainFromUrl(url);
    }

    /// <summary>
    /// Primary key - INT AUTO_INCREMENT
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    /// <summary>
    /// The full phishing URL
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The domain extracted from the URL (stored for performance)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// When this entry was created
    /// </summary>
    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this entry was soft-deleted (NULL if active)
    /// </summary>
    public DateTime? DateDeleted { get; set; }

    /// <summary>
    /// Source of the phishing URL (e.g., "PhishTank", "OpenPhish", "Manual")
    /// </summary>
    [MaxLength(100)]
    public string? Source { get; set; }

    /// <summary>
    /// Extracts domain from URL
    /// </summary>
    public static string GetDomainFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        try
        {
            // Ensure scheme exists (Uri requires it)
            if (!url.Contains("://"))
                url = "http://" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return string.Empty;
            
            var h = uri.Host.ToLowerInvariant();
            
            // Remove www prefix
            if (h.StartsWith("www."))
                h = h.Substring(4);
            
            // Extract root domain (last 2 parts, or 3 for known TLDs like co.uk)
            var parts = h.Split('.');
            if (parts.Length <= 2)
                return h;
            
            // Check for known two-part TLDs
            var knownTwoPartTlds = new[] { "co.uk", "com.au", "co.nz", "co.jp", "com.br", "co.il", "org.uk", "net.au" };
            var lastTwo = $"{parts[^2]}.{parts[^1]}";
            
            if (knownTwoPartTlds.Contains(lastTwo))
            {
                // Return last 3 parts (e.g., example.co.uk)
                return parts.Length >= 3 ? $"{parts[^3]}.{lastTwo}" : h;
            }
            
            // Return last 2 parts (e.g., example.com)
            return lastTwo;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Soft delete this entry
    /// </summary>
    public void Delete()
    {
        DateDeleted = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if this entry is active (not deleted)
    /// </summary>
    public bool IsActive => DateDeleted == null;
}
