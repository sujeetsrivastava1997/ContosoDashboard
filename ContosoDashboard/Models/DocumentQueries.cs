namespace ContosoDashboard.Models;

public enum DocumentSortField
{
    Title,
    UploadedUtc,
    Category,
    FileSizeBytes
}

public sealed record DocumentFilter
{
    public string? Search { get; init; }
    public DocumentCategory? Category { get; init; }
    public int? ProjectId { get; init; }
    public DateTime? UploadedFromUtc { get; init; }
    public DateTime? UploadedThroughUtc { get; init; }
    public DocumentSortField SortBy { get; init; } = DocumentSortField.UploadedUtc;
    public bool Descending { get; init; } = true;
}

public sealed record DocumentMetadataRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required DocumentCategory Category { get; init; }
    public IReadOnlyCollection<string>? Tags { get; init; }
}

public sealed record DocumentReplacementRequest
{
    public required string OriginalFileName { get; init; }
    public required string MimeType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required Stream FileStream { get; init; }
}

public sealed record DocumentFileResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    Document? Document = null,
    Stream? Stream = null);
