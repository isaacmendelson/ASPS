using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

/// <summary>
/// Entity for storing blacklisted phone numbers.
/// JIRA: ASPS-282
/// </summary>
[Table("BlacklistedPhoneNumbers")]
public class BlacklistedPhoneNumber
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public DateTime? DateDeleted { get; set; }

    public bool IsDeleted { get; set; } = false;
}
