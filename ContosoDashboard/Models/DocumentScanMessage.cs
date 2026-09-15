namespace ContosoDashboard.Models;

public sealed class DocumentScanMessage
{
    public int DocumentId { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string UploadAttemptId { get; init; } = string.Empty;
    public int SchemaVersion { get; init; } = 1;
}
