using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public sealed class DocumentShareRecipient
{
    [Key]
    public int DocumentShareRecipientId { get; set; }

    [Required]
    public int DocumentShareId { get; set; }

    [Required]
    public int RecipientUserId { get; set; }

    public DateTime? RevokedUtc { get; set; }
    public int? RevokedByUserId { get; set; }

    public DocumentShare Share { get; set; } = null!;
    public User RecipientUser { get; set; } = null!;
    public User? RevokedByUser { get; set; }
}
