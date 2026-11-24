using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public enum DocumentCategory
{
    General,
    Requirements,
    Design,
    Implementation,
    Testing,
    Operations
}

public enum DocumentActivityType
{
    Upload,
    Download,
    Preview,
    MetadataUpdated,
    Replaced,
    Deleted,
    Shared,
    Revoked
}

public sealed class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DocumentCategory Category { get; set; }

    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }

    [Required]
    public int UploaderUserId { get; set; }

    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
    public long FileSizeBytes { get; set; }

    [Required]
    [MaxLength(255)]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string StorageKey { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public User UploaderUser { get; set; } = null!;
    public Project? Project { get; set; }
    public TaskItem? TaskItem { get; set; }
}
