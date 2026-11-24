using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public sealed class DocumentTag
{
    [Key]
    public int DocumentTagId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Tag { get; set; } = string.Empty;

    public Document Document { get; set; } = null!;
}
