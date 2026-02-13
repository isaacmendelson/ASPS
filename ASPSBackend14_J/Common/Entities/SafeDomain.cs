using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Entities;

[Table("SafeDomains")]
public class SafeDomain
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Key")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Domain { get; set; } = string.Empty;

    public DateTime? DateCreated { get; set; }

    public bool IsDeleted { get; set; } = false;
}
