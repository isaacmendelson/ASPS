using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Entity for storing known bank websites.
/// JIRA: ASPS-297
/// </summary>
[Table("BankWebsites")]
public class BankWebsite
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Domain { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Country { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public DateTime? DateModified { get; set; }

    public DateTime? DateDeleted { get; set; }

    public bool IsDeleted { get; set; } = false;
}
