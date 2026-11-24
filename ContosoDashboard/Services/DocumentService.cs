using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

/// <summary>Result of a document operation.</summary>
public sealed record DocumentOperationResult
{
    public bool Succeeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Document? Document { get; init; }

    public static DocumentOperationResult Success(Document document) =>
        new() { Succeeded = true, Document = document };

    public static DocumentOperationResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Request used to upload a document.</summary>
public sealed record DocumentUploadRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required DocumentCategory Category { get; init; }
    public int? ProjectId { get; init; }
    public int? TaskId { get; init; }
    public required string OriginalFileName { get; init; }
    public required string MimeType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required Stream FileStream { get; init; }
}

public interface IDocumentService
{
    Task<DocumentOperationResult> UploadAsync(int requestingUserId, DocumentUploadRequest request, CancellationToken cancellationToken);
}

public sealed class DocumentService(
    ApplicationDbContext context,
    IFileStorageService fileStorage,
    IMalwareScanService malwareScanner) : IDocumentService
{
    private const long MaximumFileSizeBytes = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTypes =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" },
            ["application/msword"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".doc" },
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" },
            ["application/vnd.ms-excel"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".xls" },
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".xlsx" },
            ["application/vnd.ms-powerpoint"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ppt" },
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pptx" },
            ["text/plain"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" },
            ["image/jpeg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" },
            ["image/png"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png" }
        };

    public async Task<DocumentOperationResult> UploadAsync(int requestingUserId, DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return DocumentOperationResult.Failure("invalid_title", "A document title is required.");
        }

        if (request.Title.Length > 255)
        {
            return DocumentOperationResult.Failure("invalid_title", "The document title must be 255 characters or fewer.");
        }

        if (request.Description?.Length > 2000)
        {
            return DocumentOperationResult.Failure("invalid_description", "The document description must be 2000 characters or fewer.");
        }

        if (request.FileSizeBytes is <= 0 or > MaximumFileSizeBytes)
        {
            return DocumentOperationResult.Failure(
                "invalid_file_size",
                "The file must be greater than 0 bytes and no larger than 25 MB.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (!AllowedTypes.TryGetValue(request.MimeType, out var allowedExtensions) ||
            !allowedExtensions.Contains(extension))
        {
            return DocumentOperationResult.Failure(
                "unsupported_file_type",
                "The file type is not supported.");
        }

        var uploader = await context.Users.FindAsync([requestingUserId], cancellationToken);
        if (uploader is null)
        {
            return DocumentOperationResult.Failure("unauthorized", "The uploader is not recognized.");
        }

        int? resolvedProjectId = request.ProjectId;
        if (request.TaskId.HasValue)
        {
            var task = await context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TaskId == request.TaskId.Value, cancellationToken);

            if (task is null ||
                (task.AssignedUserId != requestingUserId &&
                 task.CreatedByUserId != requestingUserId &&
                 task.ProjectId is null))
            {
                return DocumentOperationResult.Failure("invalid_task", "The task does not exist or is not accessible.");
            }

            resolvedProjectId = task.ProjectId;
        }

        if (resolvedProjectId.HasValue)
        {
            var project = await context.Projects
                .AsNoTracking()
                .Include(item => item.ProjectMembers)
                .FirstOrDefaultAsync(item => item.ProjectId == resolvedProjectId.Value, cancellationToken);

            if (project is null)
            {
                return DocumentOperationResult.Failure("invalid_project", "The project does not exist.");
            }

            var projectAccessible = project.ProjectManagerId == requestingUserId ||
                project.ProjectMembers.Any(member => member.UserId == requestingUserId);

            if (!projectAccessible && uploader.Role != UserRole.Administrator)
            {
                return DocumentOperationResult.Failure("unauthorized", "The project is not accessible.");
            }
        }

        if (!await malwareScanner.IsSafeAsync(request.FileStream, request.OriginalFileName, request.MimeType, cancellationToken))
        {
            return DocumentOperationResult.Failure(
                "unsafe_file",
                "The file could not be accepted.");
        }

        string? storageKey = null;
        try
        {
            storageKey = await fileStorage.SaveAsync(request.FileStream, extension, cancellationToken);

            var document = new Document
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Category = request.Category,
                ProjectId = resolvedProjectId,
                TaskId = request.TaskId,
                UploaderUserId = requestingUserId,
                UploadedUtc = DateTime.UtcNow,
                FileSizeBytes = request.FileSizeBytes,
                MimeType = request.MimeType.ToLowerInvariant(),
                StorageKey = storageKey,
                OriginalFileName = Path.GetFileName(request.OriginalFileName),
                UpdatedUtc = DateTime.UtcNow
            };

            context.Documents.Add(document);
            await context.SaveChangesAsync(cancellationToken);

            context.DocumentActivities.Add(new DocumentActivity
            {
                DocumentId = document.DocumentId,
                DocumentTitleSnapshot = document.Title,
                ActorUserId = requestingUserId,
                ActivityType = DocumentActivityType.Upload,
                ActivityUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);

            return DocumentOperationResult.Success(document);
        }
        catch (OperationCanceledException)
        {
            if (storageKey is not null)
            {
                await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            }

            throw;
        }
        catch
        {
            if (storageKey is not null)
            {
                await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            }

            return DocumentOperationResult.Failure(
                "upload_failed",
                "The document could not be uploaded.");
        }
    }
}
