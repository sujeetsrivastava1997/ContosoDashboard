using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public sealed class DocumentActivity
{
    [Key]
    public int DocumentActivityId { get; set; }

    public int? DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string DocumentTitleSnapshot { get; set; } = string.Empty;

    [Required]
    public int ActorUserId { get; set; }

    [Required]
    public DocumentActivityType ActivityType { get; set; }

    public DateTime ActivityUtc { get; set; } = DateTime.UtcNow;

    public Document? Document { get; set; }
}
