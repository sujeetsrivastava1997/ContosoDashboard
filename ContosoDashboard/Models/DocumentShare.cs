using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public enum DocumentShareType
{
    User,
    Team
}

public sealed class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    [Required]
    public int SharedByUserId { get; set; }

    [Required]
    public DocumentShareType ShareType { get; set; }

    [MaxLength(100)]
    public string? TeamIdentifier { get; set; }

    public DateTime SharedUtc { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public User SharedByUser { get; set; } = null!;
    public ICollection<DocumentShareRecipient> Recipients { get; set; } = new List<DocumentShareRecipient>();
}
