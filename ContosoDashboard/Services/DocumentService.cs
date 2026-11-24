using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

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

public sealed record DocumentUploadRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DocumentCategory? Category { get; init; }
    public int? ProjectId { get; init; }
    public int? TaskId { get; init; }
    public IReadOnlyCollection<string>? Tags { get; init; }
    public required string OriginalFileName { get; init; }
    public required string MimeType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required Stream FileStream { get; init; }
}

public interface IDocumentService
{
    Task<DocumentOperationResult> UploadAsync(int requestingUserId, DocumentUploadRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> GetAccessibleAsync(int requestingUserId, DocumentFilter? filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> GetSharedWithMeAsync(int requestingUserId, CancellationToken cancellationToken);
    Task<bool> CanViewAsync(int documentId, int requestingUserId, CancellationToken cancellationToken);
    Task<DocumentOperationResult> UpdateMetadataAsync(int documentId, int requestingUserId, DocumentMetadataRequest request, CancellationToken cancellationToken);
    Task<DocumentOperationResult> ReplaceAsync(int documentId, int requestingUserId, DocumentReplacementRequest request, CancellationToken cancellationToken);
    Task<DocumentOperationResult> DeleteAsync(int documentId, int requestingUserId, CancellationToken cancellationToken);
    Task<DocumentOperationResult> ShareWithUserAsync(int documentId, int ownerId, int recipientUserId, CancellationToken cancellationToken);
    Task<DocumentOperationResult> ShareWithTeamAsync(int documentId, int ownerId, string teamIdentifier, CancellationToken cancellationToken);
    Task<DocumentOperationResult> RevokeUserShareAsync(int documentId, int ownerId, int recipientUserId, CancellationToken cancellationToken);
    Task<DocumentOperationResult> RevokeTeamShareAsync(int documentId, int ownerId, string teamIdentifier, CancellationToken cancellationToken);
    Task<DocumentFileResult> OpenPreviewAsync(int documentId, int requestingUserId, CancellationToken cancellationToken);
    Task<DocumentFileResult> OpenDownloadAsync(int documentId, int requestingUserId, CancellationToken cancellationToken);
}

public sealed class DocumentService(
    ApplicationDbContext context,
    IFileStorageService fileStorage,
    IMalwareScanService malwareScanner,
    INotificationService? notificationService = null) : IDocumentService
{
    public const long MaximumFileSizeBytes = 25 * 1024 * 1024;
    private const int MaximumTagCount = 20;

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

    public async Task<DocumentOperationResult> UploadAsync(
        int requestingUserId,
        DocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMetadata(request.Title, request.Description, request.Category);
        if (validation is not null)
        {
            return validation;
        }

        validation = ValidateFile(request.OriginalFileName, request.MimeType, request.FileSizeBytes, request.FileStream);
        if (validation is not null)
        {
            return validation;
        }

        var normalizedTags = NormalizeTags(request.Tags);
        if (normalizedTags.Count > MaximumTagCount || request.Tags?.Any(tag => tag?.Trim().Length > 50) == true)
            return DocumentOperationResult.Failure("invalid_tags", $"A document may have at most {MaximumTagCount} tags of 50 characters or fewer.");

        var uploader = await context.Users.FindAsync([requestingUserId], cancellationToken);
        if (uploader is null)
        {
            return DocumentOperationResult.Failure("unauthorized", "The uploader is not recognized.");
        }

        var resolvedProjectId = request.ProjectId;
        if (request.TaskId.HasValue)
        {
            var task = await context.Tasks.AsNoTracking()
                .FirstOrDefaultAsync(item => item.TaskId == request.TaskId.Value, cancellationToken);
            if (task is null || !await CanParticipateInTaskAsync(task, requestingUserId, cancellationToken))
            {
                return DocumentOperationResult.Failure("invalid_task", "The task does not exist or is not accessible.");
            }

            if (request.ProjectId.HasValue && request.ProjectId != task.ProjectId)
            {
                return DocumentOperationResult.Failure("invalid_project", "The project must match the selected task.");
            }

            resolvedProjectId = task.ProjectId;
        }

        if (resolvedProjectId.HasValue &&
            !await CanAccessProjectAsync(resolvedProjectId.Value, requestingUserId, uploader.Role, cancellationToken))
        {
            return DocumentOperationResult.Failure("invalid_project", "The project does not exist or is not accessible.");
        }

        bool safe;
        try
        {
            safe = await malwareScanner.IsSafeAsync(request.FileStream, request.OriginalFileName, request.MimeType, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            safe = false;
        }

        if (!safe)
        {
            return DocumentOperationResult.Failure("unsafe_file", "The file could not be accepted.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        string? storageKey = null;
        Document? document = null;
        DocumentActivity? uploadActivity = null;
        try
        {
            request.FileStream.Position = 0;
            storageKey = await fileStorage.SaveAsync(request.FileStream, extension, cancellationToken);

            document = new Document
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Category = request.Category!.Value,
                ProjectId = resolvedProjectId,
                TaskId = request.TaskId,
                UploaderUserId = requestingUserId,
                UploadedUtc = DateTime.UtcNow,
                FileSizeBytes = request.FileSizeBytes,
                MimeType = request.MimeType.ToLowerInvariant(),
                StorageKey = storageKey,
                OriginalFileName = Path.GetFileName(request.OriginalFileName),
                UpdatedUtc = DateTime.UtcNow,
                Tags = normalizedTags.Select(tag => new DocumentTag { Tag = tag }).ToList()
            };

            context.Documents.Add(document);
            uploadActivity = CreateActivity(document, requestingUserId, DocumentActivityType.Upload);
            context.DocumentActivities.Add(uploadActivity);
            await context.SaveChangesAsync(cancellationToken);

            if (document.ProjectId.HasValue)
            {
                await NotifyProjectMembersAsync(document, requestingUserId, cancellationToken);
            }

            return DocumentOperationResult.Success(document);
        }
        catch (OperationCanceledException)
        {
            if (uploadActivity is not null)
                context.Entry(uploadActivity).State = EntityState.Detached;
            if (document is not null)
                context.Entry(document).State = EntityState.Detached;
            if (storageKey is not null)
            {
                await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            }

            throw;
        }
        catch
        {
            if (uploadActivity is not null)
                context.Entry(uploadActivity).State = EntityState.Detached;
            if (document is not null)
                context.Entry(document).State = EntityState.Detached;
            if (storageKey is not null)
            {
                await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            }

            return DocumentOperationResult.Failure("upload_failed", "The document could not be uploaded.");
        }
    }

    public async Task<IReadOnlyList<Document>> GetAccessibleAsync(
        int requestingUserId,
        DocumentFilter? filter,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == requestingUserId, cancellationToken);
        if (user is null)
        {
            return [];
        }

        IQueryable<Document> query = context.Documents.AsNoTracking()
            .Include(document => document.Project)
            .Include(document => document.UploaderUser)
            .Include(document => document.Tags)
            .Where(document => user.Role == UserRole.Administrator
                || document.UploaderUserId == requestingUserId
                || document.ProjectId.HasValue && document.Project!.ProjectManagerId == requestingUserId
                || document.ProjectId.HasValue && document.Project!.ProjectMembers.Any(member => member.UserId == requestingUserId)
                || document.TaskId.HasValue && (document.TaskItem!.AssignedUserId == requestingUserId || document.TaskItem.CreatedByUserId == requestingUserId)
                || document.Shares.Any(share => share.Recipients.Any(recipient =>
                    recipient.RecipientUserId == requestingUserId && recipient.RevokedUtc == null))
                || user.Role == UserRole.TeamLead && user.Department != null
                    && document.UploaderUser.Department == user.Department);

        if (filter is not null)
        {
            if (filter.Category.HasValue)
                query = query.Where(document => document.Category == filter.Category.Value);
            if (filter.ProjectId.HasValue)
                query = query.Where(document => document.ProjectId == filter.ProjectId.Value);
            if (filter.UploadedFromUtc.HasValue)
                query = query.Where(document => document.UploadedUtc >= filter.UploadedFromUtc.Value);
            if (filter.UploadedThroughUtc.HasValue)
                query = query.Where(document => document.UploadedUtc < filter.UploadedThroughUtc.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(document =>
                    document.Title.Contains(term) ||
                    (document.Description != null && document.Description.Contains(term)) ||
                    document.Tags.Any(tag => tag.Tag.Contains(term)) ||
                    document.UploaderUser.DisplayName.Contains(term) ||
                    (document.Project != null && document.Project.Name.Contains(term)));
            }

            query = filter.SortBy switch
            {
                DocumentSortField.Title => filter.Descending ? query.OrderByDescending(document => document.Title) : query.OrderBy(document => document.Title),
                DocumentSortField.Category => filter.Descending ? query.OrderByDescending(document => document.Category) : query.OrderBy(document => document.Category),
                DocumentSortField.FileSizeBytes => filter.Descending ? query.OrderByDescending(document => document.FileSizeBytes) : query.OrderBy(document => document.FileSizeBytes),
                _ => filter.Descending ? query.OrderByDescending(document => document.UploadedUtc) : query.OrderBy(document => document.UploadedUtc)
            };
        }
        else
        {
            query = query.OrderByDescending(document => document.UploadedUtc);
        }

        return await query.Take(500).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetSharedWithMeAsync(int requestingUserId, CancellationToken cancellationToken) =>
        await context.Documents.AsNoTracking()
            .Include(document => document.Project)
            .Include(document => document.UploaderUser)
            .Include(document => document.Tags)
            .Where(document => document.Shares.Any(share =>
                share.Recipients.Any(recipient => recipient.RecipientUserId == requestingUserId && recipient.RevokedUtc == null)))
            .OrderByDescending(document => document.UploadedUtc)
            .ToListAsync(cancellationToken);

    public async Task<bool> CanViewAsync(int documentId, int requestingUserId, CancellationToken cancellationToken) =>
        await GetAccessibleDocumentsQuery(requestingUserId)
            .AnyAsync(document => document.DocumentId == documentId, cancellationToken);

    public async Task<DocumentOperationResult> UpdateMetadataAsync(
        int documentId,
        int requestingUserId,
        DocumentMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMetadata(request.Title, request.Description, request.Category);
        if (validation is not null)
            return validation;

        var tags = NormalizeTags(request.Tags);
        if (tags.Count > MaximumTagCount)
            return DocumentOperationResult.Failure("invalid_tags", $"A document may have at most {MaximumTagCount} tags.");
        if (request.Tags?.Any(tag => tag?.Trim().Length > 50) == true)
            return DocumentOperationResult.Failure("invalid_tags", "Each tag must be 50 characters or fewer.");

        var document = await context.Documents.Include(item => item.Tags)
            .FirstOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
            return DocumentOperationResult.Failure("not_found", "The document was not found.");
        if (!await CanManageAsync(document, requestingUserId, cancellationToken))
            return DocumentOperationResult.Failure("forbidden", "You are not permitted to manage this document.");

        document.Title = request.Title.Trim();
        document.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        document.Category = request.Category;
        document.UpdatedUtc = DateTime.UtcNow;
        context.DocumentTags.RemoveRange(document.Tags);
        document.Tags = tags.Select(tag => new DocumentTag { DocumentId = documentId, Tag = tag }).ToList();
        context.DocumentActivities.Add(CreateActivity(document, requestingUserId, DocumentActivityType.MetadataUpdated));
        await context.SaveChangesAsync(cancellationToken);
        return DocumentOperationResult.Success(document);
    }

    public async Task<DocumentOperationResult> ReplaceAsync(
        int documentId,
        int requestingUserId,
        DocumentReplacementRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateFile(request.OriginalFileName, request.MimeType, request.FileSizeBytes, request.FileStream);
        if (validation is not null)
            return validation;

        var document = await context.Documents.FirstOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
            return DocumentOperationResult.Failure("not_found", "The document was not found.");
        if (!await CanManageAsync(document, requestingUserId, cancellationToken))
            return DocumentOperationResult.Failure("forbidden", "You are not permitted to manage this document.");

        if (!await malwareScanner.IsSafeAsync(request.FileStream, request.OriginalFileName, request.MimeType, cancellationToken))
            return DocumentOperationResult.Failure("unsafe_file", "The file could not be accepted.");

        var oldStorageKey = document.StorageKey;
        string? newStorageKey = null;
        DocumentActivity? replacementActivity = null;
        try
        {
            request.FileStream.Position = 0;
            newStorageKey = await fileStorage.SaveAsync(request.FileStream, Path.GetExtension(request.OriginalFileName), cancellationToken);
            document.StorageKey = newStorageKey;
            document.OriginalFileName = Path.GetFileName(request.OriginalFileName);
            document.FileSizeBytes = request.FileSizeBytes;
            document.MimeType = request.MimeType.ToLowerInvariant();
            document.UpdatedUtc = DateTime.UtcNow;
            replacementActivity = CreateActivity(document, requestingUserId, DocumentActivityType.Replaced);
            context.DocumentActivities.Add(replacementActivity);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (replacementActivity is not null)
                context.Entry(replacementActivity).State = EntityState.Detached;
            if (newStorageKey is not null)
                await fileStorage.DeleteAsync(newStorageKey, CancellationToken.None);
            try
            {
                await context.Entry(document).ReloadAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the generic failure result if the provider cannot reload an unsaved entity.
            }
            return DocumentOperationResult.Failure("replace_failed", "The document could not be replaced.");
        }

        await fileStorage.DeleteAsync(oldStorageKey, cancellationToken);
        return DocumentOperationResult.Success(document);
    }

    public async Task<DocumentOperationResult> DeleteAsync(int documentId, int requestingUserId, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(item => item.Tags)
            .Include(item => item.Shares).ThenInclude(share => share.Recipients)
            .FirstOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
            return DocumentOperationResult.Failure("not_found", "The document was not found.");
        if (!await CanDeleteAsync(document, requestingUserId, cancellationToken))
            return DocumentOperationResult.Failure("forbidden", "You are not permitted to delete this document.");

        var storageKey = document.StorageKey;
        var audit = CreateActivity(document, requestingUserId, DocumentActivityType.Deleted);
        audit.DocumentId = null;
        audit.Document = null;
        context.DocumentActivities.Add(audit);
        context.Documents.Remove(document);
        await context.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(storageKey, cancellationToken);
        return DocumentOperationResult.Success(document);
    }

    public Task<DocumentOperationResult> ShareWithUserAsync(
        int documentId, int ownerId, int recipientUserId, CancellationToken cancellationToken) =>
        CreateShareAsync(documentId, ownerId, recipientUserId, null, cancellationToken);

    public Task<DocumentOperationResult> ShareWithTeamAsync(
        int documentId, int ownerId, string teamIdentifier, CancellationToken cancellationToken) =>
        CreateShareAsync(documentId, ownerId, null, teamIdentifier, cancellationToken);

    public async Task<DocumentOperationResult> RevokeUserShareAsync(
        int documentId, int ownerId, int recipientUserId, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocumentAsync(documentId, ownerId, cancellationToken);
        if (document is null)
            return DocumentOperationResult.Failure("forbidden", "Only the document owner may revoke a share.");

        var recipients = await context.DocumentShareRecipients
            .Include(recipient => recipient.Share)
            .Where(recipient => recipient.Share.DocumentId == documentId &&
                recipient.Share.ShareType == DocumentShareType.User &&
                recipient.RecipientUserId == recipientUserId && recipient.RevokedUtc == null)
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
            return DocumentOperationResult.Failure("share_not_found", "The active share was not found.");

        RevokeRecipients(recipients, ownerId);
        context.DocumentActivities.Add(CreateActivity(document, ownerId, DocumentActivityType.Revoked));
        await context.SaveChangesAsync(cancellationToken);
        return DocumentOperationResult.Success(document);
    }

    public async Task<DocumentOperationResult> RevokeTeamShareAsync(
        int documentId, int ownerId, string teamIdentifier, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocumentAsync(documentId, ownerId, cancellationToken);
        if (document is null)
            return DocumentOperationResult.Failure("forbidden", "Only the document owner may revoke a share.");

        var recipients = await context.DocumentShareRecipients
            .Include(recipient => recipient.Share)
            .Where(recipient => recipient.Share.DocumentId == documentId &&
                recipient.Share.ShareType == DocumentShareType.Team &&
                recipient.Share.TeamIdentifier == teamIdentifier && recipient.RevokedUtc == null)
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
            return DocumentOperationResult.Failure("share_not_found", "The active team share was not found.");

        RevokeRecipients(recipients, ownerId);
        context.DocumentActivities.Add(CreateActivity(document, ownerId, DocumentActivityType.Revoked));
        await context.SaveChangesAsync(cancellationToken);
        return DocumentOperationResult.Success(document);
    }

    public Task<DocumentFileResult> OpenPreviewAsync(int documentId, int requestingUserId, CancellationToken cancellationToken) =>
        OpenFileAsync(documentId, requestingUserId, preview: true, cancellationToken);

    public Task<DocumentFileResult> OpenDownloadAsync(int documentId, int requestingUserId, CancellationToken cancellationToken) =>
        OpenFileAsync(documentId, requestingUserId, preview: false, cancellationToken);

    private async Task<DocumentFileResult> OpenFileAsync(int documentId, int requestingUserId, bool preview, CancellationToken cancellationToken)
    {
        var document = await context.Documents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
            return new(false, "not_found", "The document was not found.");
        if (!await CanViewAsync(documentId, requestingUserId, cancellationToken))
            return new(false, "forbidden", "You are not permitted to access this document.");
        if (preview && document.MimeType is not ("application/pdf" or "image/jpeg" or "image/png"))
            return new(false, "preview_unsupported", "Preview is not available for this file type.");

        var stream = await fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (stream is null)
            return new(false, "not_found", "The document was not found.");

        try
        {
            await RecordActivityAsync(document, requestingUserId,
                preview ? DocumentActivityType.Preview : DocumentActivityType.Download, cancellationToken);
        }
        catch
        {
            await stream.DisposeAsync();
            return new(false, "access_failed", "The document could not be opened.");
        }

        return new(true, Document: document, Stream: stream);
    }

    private async Task<DocumentOperationResult> CreateShareAsync(
        int documentId, int ownerId, int? recipientUserId, string? teamIdentifier, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocumentAsync(documentId, ownerId, cancellationToken);
        if (document is null)
            return DocumentOperationResult.Failure("forbidden", "Only the document owner may share this document.");

        var isTeam = teamIdentifier is not null;
        List<int> recipientIds;
        if (isTeam)
        {
            var normalizedTeam = teamIdentifier!.Trim();
            if (normalizedTeam.Length is 0 or > 100)
                return DocumentOperationResult.Failure("invalid_team", "A valid department is required.");
            recipientIds = await context.Users.AsNoTracking()
                .Where(user => user.Department == normalizedTeam && user.UserId != ownerId)
                .Select(user => user.UserId).ToListAsync(cancellationToken);
            if (recipientIds.Count == 0)
                return DocumentOperationResult.Failure("empty_team", "No current team members are available to share with.");
            teamIdentifier = normalizedTeam;
        }
        else
        {
            if (!recipientUserId.HasValue ||
                !await context.Users.AnyAsync(user => user.UserId == recipientUserId.Value, cancellationToken))
                return DocumentOperationResult.Failure("invalid_recipient", "The selected recipient was not found.");
            recipientIds = [recipientUserId.Value];
        }

        var existingRecipientIds = await context.DocumentShareRecipients
            .Include(recipient => recipient.Share)
            .Where(recipient => recipient.Share.DocumentId == documentId && recipient.RevokedUtc == null)
            .Select(recipient => recipient.RecipientUserId).ToListAsync(cancellationToken);
        recipientIds = recipientIds.Except(existingRecipientIds).ToList();
        if (recipientIds.Count == 0)
            return DocumentOperationResult.Failure("already_shared", "The document is already shared with those recipients.");

        var share = new DocumentShare
        {
            DocumentId = documentId,
            SharedByUserId = ownerId,
            ShareType = isTeam ? DocumentShareType.Team : DocumentShareType.User,
            TeamIdentifier = teamIdentifier,
            SharedUtc = DateTime.UtcNow,
            Recipients = recipientIds.Select(id => new DocumentShareRecipient { RecipientUserId = id }).ToList()
        };
        context.DocumentShares.Add(share);
        context.DocumentActivities.Add(CreateActivity(document, ownerId, DocumentActivityType.Shared));
        await context.SaveChangesAsync(cancellationToken);
        return DocumentOperationResult.Success(document);
    }

    private async Task<Document?> GetOwnedDocumentAsync(int documentId, int ownerId, CancellationToken cancellationToken) =>
        await context.Documents.FirstOrDefaultAsync(document => document.DocumentId == documentId && document.UploaderUserId == ownerId, cancellationToken);

    private async Task<bool> CanManageAsync(Document document, int requestingUserId, CancellationToken cancellationToken)
    {
        if (document.UploaderUserId == requestingUserId)
            return true;
        var manager = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == requestingUserId, cancellationToken);
        var uploader = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == document.UploaderUserId, cancellationToken);
        return (manager?.Role is UserRole.TeamLead or UserRole.Administrator) &&
            manager.Department is not null && manager.Department == uploader?.Department;
    }

    private async Task<bool> CanDeleteAsync(Document document, int requestingUserId, CancellationToken cancellationToken)
    {
        if (await CanManageAsync(document, requestingUserId, cancellationToken))
            return true;
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == requestingUserId, cancellationToken);
        if (user?.Role == UserRole.Administrator)
            return true;
        if (!document.ProjectId.HasValue)
            return false;
        return await context.Projects.AnyAsync(project =>
            project.ProjectId == document.ProjectId && project.ProjectManagerId == requestingUserId, cancellationToken);
    }

    private async Task<bool> CanParticipateInTaskAsync(TaskItem task, int userId, CancellationToken cancellationToken)
    {
        if (task.AssignedUserId == userId || task.CreatedByUserId == userId)
            return true;
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return task.ProjectId.HasValue && user is not null &&
            await CanAccessProjectAsync(task.ProjectId.Value, userId, user.Role, cancellationToken);
    }

    private async Task<bool> CanAccessProjectAsync(int projectId, int userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role == UserRole.Administrator)
            return await context.Projects.AnyAsync(project => project.ProjectId == projectId, cancellationToken);
        return await context.Projects.AnyAsync(project => project.ProjectId == projectId &&
            (project.ProjectManagerId == userId || project.ProjectMembers.Any(member => member.UserId == userId)), cancellationToken);
    }

    private IQueryable<Document> GetAccessibleDocumentsQuery(int requestingUserId)
    {
        var user = context.Users.AsNoTracking().FirstOrDefault(item => item.UserId == requestingUserId);
        if (user?.Role == UserRole.Administrator)
            return context.Documents;

        return context.Documents.Where(document =>
            document.UploaderUserId == requestingUserId
            || document.ProjectId.HasValue && (document.Project!.ProjectManagerId == requestingUserId ||
                document.Project.ProjectMembers.Any(member => member.UserId == requestingUserId))
            || document.TaskId.HasValue && (document.TaskItem!.AssignedUserId == requestingUserId ||
                document.TaskItem.CreatedByUserId == requestingUserId)
            || document.Shares.Any(share => share.Recipients.Any(recipient =>
                recipient.RecipientUserId == requestingUserId && recipient.RevokedUtc == null))
            || user != null && user.Role == UserRole.TeamLead && user.Department != null &&
                document.UploaderUser.Department == user.Department);
    }

    private static DocumentOperationResult? ValidateMetadata(string? title, string? description, DocumentCategory? category)
    {
        if (string.IsNullOrWhiteSpace(title))
            return DocumentOperationResult.Failure("invalid_title", "A document title is required.");
        if (title.Length > 255)
            return DocumentOperationResult.Failure("invalid_title", "The document title must be 255 characters or fewer.");
        if (description?.Length > 2000)
            return DocumentOperationResult.Failure("invalid_description", "The document description must be 2000 characters or fewer.");
        if (category is null || !Enum.IsDefined(category.Value))
            return DocumentOperationResult.Failure("invalid_category", "Select a valid document category.");
        return null;
    }

    private static DocumentOperationResult? ValidateFile(string fileName, string mimeType, long size, Stream stream)
    {
        if (size is <= 0 or > MaximumFileSizeBytes)
            return DocumentOperationResult.Failure("invalid_file_size", "The file must be greater than 0 bytes and no larger than 25 MB.");
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName ||
            !AllowedTypes.TryGetValue(mimeType, out var extensions) ||
            !extensions.Contains(Path.GetExtension(fileName)))
            return DocumentOperationResult.Failure("unsupported_file_type", "The file type is not supported.");
        if (!stream.CanSeek || stream.Length != size)
            return DocumentOperationResult.Failure("invalid_file_size", "The file size could not be verified.");
        return null;
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();

    private DocumentActivity CreateActivity(Document document, int actorId, DocumentActivityType type) =>
        new()
        {
            DocumentId = document.DocumentId == 0 ? null : document.DocumentId,
            DocumentTitleSnapshot = document.Title,
            ActorUserId = actorId,
            ActivityType = type,
            ActivityUtc = DateTime.UtcNow,
            Document = context.Entry(document).State == EntityState.Detached ? null : document
        };

    private static void RevokeRecipients(IEnumerable<DocumentShareRecipient> recipients, int ownerId)
    {
        foreach (var recipient in recipients)
        {
            recipient.RevokedUtc = DateTime.UtcNow;
            recipient.RevokedByUserId = ownerId;
        }
    }

    private async Task RecordActivityAsync(Document document, int actorId, DocumentActivityType type, CancellationToken cancellationToken)
    {
        context.DocumentActivities.Add(CreateActivity(document, actorId, type));
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyProjectMembersAsync(Document document, int uploaderId, CancellationToken cancellationToken)
    {
        if (notificationService is null || !document.ProjectId.HasValue)
            return;

        var recipientIds = await context.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == document.ProjectId && member.UserId != uploaderId)
            .Select(member => member.UserId)
            .Union(context.Projects.Where(project => project.ProjectId == document.ProjectId && project.ProjectManagerId != uploaderId)
                .Select(project => project.ProjectManagerId))
            .Distinct().ToListAsync(cancellationToken);
        foreach (var recipientId in recipientIds)
        {
            try
            {
                await notificationService.CreateDocumentNotificationAsync(
                    recipientId, document.DocumentId, "Project document uploaded",
                    $"A new document, \"{document.Title}\", was added to a project you belong to.",
                    NotificationType.DocumentUploaded);
            }
            catch
            {
                // Notification delivery is best-effort and must not undo a successful upload.
            }
        }
    }
}
