using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.AspNetCore.Http;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    Task<Document> UploadAsync(DocumentUploadRequest request, int requestingUserId);
    Task<List<Document>> GetUserDocumentsAsync(int requestingUserId);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);
    Task<bool> UpdateMetadataAsync(int documentId, DocumentUpdateRequest request, int requestingUserId);
    Task<bool> DeleteAsync(int documentId, int requestingUserId);
    Task<bool> ShareAsync(int documentId, int recipientUserId, int requestingUserId);
    Task<List<Document>> SearchAsync(string query, int requestingUserId);
}

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IScanQueue _scanQueue;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".png", ".jpg", ".jpeg"
    };

    public DocumentService(ApplicationDbContext context, IFileStorageService fileStorageService, IScanQueue? scanQueue = null)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _scanQueue = scanQueue ?? new DiscardingScanQueue();
    }

    public async Task<Document> UploadAsync(DocumentUploadRequest request, int requestingUserId)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Document title is required.", nameof(request));

        if (request.File == null || request.File.Length == 0)
            throw new ArgumentException("A valid file is required.", nameof(request));

        var fileExtension = Path.GetExtension(request.File.FileName);
        if (string.IsNullOrWhiteSpace(fileExtension) || !AllowedExtensions.Contains(fileExtension))
            throw new InvalidOperationException("Unsupported document type.");

        var maxSizeBytes = 25 * 1024 * 1024;
        if (request.File.Length > maxSizeBytes)
            throw new InvalidOperationException("File exceeds the 25 MB limit.");

        if (request.ProjectId.HasValue)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId.Value);

            if (project == null)
                throw new InvalidOperationException("Project not found.");

            var isMember = project.ProjectMembers.Any(pm => pm.UserId == requestingUserId) || project.ProjectManagerId == requestingUserId;
            if (!isMember)
                throw new UnauthorizedAccessException("You are not allowed to upload documents to this project.");
        }

        var storedFileName = $"{Guid.NewGuid():N}{fileExtension}";
        var storagePath = await _fileStorageService.UploadAsync(request.File.OpenReadStream(), storedFileName, request.File.ContentType);
        var uploadAttemptId = Guid.NewGuid().ToString("N");

        var document = new Document
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Other" : request.Category.Trim(),
            Tags = NormalizeTags(request.Tags),
            FileName = request.File.FileName,
            StoredFileName = storedFileName,
            FilePath = storagePath,
            MimeType = string.IsNullOrWhiteSpace(request.File.ContentType) ? "application/octet-stream" : request.File.ContentType,
            FileSizeBytes = request.File.Length,
            UploadedByUserId = requestingUserId,
            ProjectId = request.ProjectId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        document.ScanAttemptId = uploadAttemptId;
        document.ScanStatus = DocumentScanStatus.PendingScan;

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            await _scanQueue.EnqueueAsync(new DocumentScanMessage
            {
                DocumentId = document.DocumentId,
                StoragePath = document.FilePath,
                MimeType = document.MimeType,
                FileSizeBytes = document.FileSizeBytes,
                UploadAttemptId = uploadAttemptId
            });
        }
        catch
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
            _context.Entry(document).State = EntityState.Detached;
            await _fileStorageService.DeleteAsync(storagePath);
            throw;
        }

        return document;
    }

    public async Task<List<Document>> GetUserDocumentsAsync(int requestingUserId)
    {
        return await _context.Documents
            .Where(d => d.UploadedByUserId == requestingUserId && !d.IsDeleted)
            .Include(d => d.Project)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectMembers)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId);

        if (project == null)
            return new List<Document>();

        var isAuthorized = project.ProjectManagerId == requestingUserId ||
            project.ProjectMembers.Any(pm => pm.UserId == requestingUserId);

        if (!isAuthorized)
            return new List<Document>();

        return await _context.Documents
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .Include(d => d.Project)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
            .ThenInclude(p => p!.ProjectMembers)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted && d.ScanStatus == DocumentScanStatus.Available);

        if (document == null)
            return null;

        var ownsDocument = document.UploadedByUserId == requestingUserId;
        var isProjectManager = document.Project != null && document.Project.ProjectManagerId == requestingUserId;
        var isProjectMember = document.Project != null && document.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId);
        var hasExplicitShare = await _context.DocumentShares.AnyAsync(ds => ds.DocumentId == documentId && ds.UserId == requestingUserId);
        var isAdministrator = await _context.Users.AnyAsync(u => u.UserId == requestingUserId && u.Role == UserRole.Administrator);

        if (!ownsDocument && !isProjectManager && !isProjectMember && !hasExplicitShare && !isAdministrator)
            return null;

        return document;
    }

    public async Task<bool> UpdateMetadataAsync(int documentId, DocumentUpdateRequest request, int requestingUserId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

        if (document == null)
            return false;

        if (document.UploadedByUserId != requestingUserId)
            return false;

        if (!string.IsNullOrWhiteSpace(request.Title))
            document.Title = request.Title.Trim();

        if (request.Description != null)
            document.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (!string.IsNullOrWhiteSpace(request.Category))
            document.Category = request.Category.Trim();

        if (request.Tags != null)
            document.Tags = NormalizeTags(request.Tags);

        if (request.ProjectId.HasValue)
            document.ProjectId = request.ProjectId.Value;

        document.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int documentId, int requestingUserId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

        if (document == null)
            return false;

        var project = document.ProjectId.HasValue
            ? await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == document.ProjectId.Value)
            : null;

        var isOwner = document.UploadedByUserId == requestingUserId;
        var isManager = project != null && project.ProjectManagerId == requestingUserId;

        if (!isOwner && !isManager)
            return false;

        document.IsDeleted = true;
        document.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(document.FilePath))
            await _fileStorageService.DeleteAsync(document.FilePath);

        return true;
    }

    public async Task<bool> ShareAsync(int documentId, int recipientUserId, int requestingUserId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted);

        if (document == null)
            return false;

        if (document.UploadedByUserId != requestingUserId)
            return false;

        var recipientExists = await _context.Users.AnyAsync(u => u.UserId == recipientUserId);
        if (!recipientExists)
            return false;

        var alreadyShared = await _context.DocumentShares
            .AnyAsync(ds => ds.DocumentId == documentId && ds.UserId == recipientUserId);

        if (alreadyShared)
            return false;

        _context.DocumentShares.Add(new DocumentShare
        {
            DocumentId = documentId,
            UserId = recipientUserId,
            SharedByUserId = requestingUserId,
            SharedDate = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Document>> SearchAsync(string query, int requestingUserId)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Document>();

        var normalizedQuery = query.Trim();

        var accessibleQuery = _context.Documents
            .Where(d => !d.IsDeleted)
            .Where(d => d.UploadedByUserId == requestingUserId ||
                        d.ProjectId.HasValue && (
                            d.Project!.ProjectManagerId == requestingUserId ||
                            d.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId)
                        ) ||
                        _context.DocumentShares.Any(ds => ds.DocumentId == d.DocumentId && ds.UserId == requestingUserId));

        return await accessibleQuery
            .Where(d => d.Title.Contains(normalizedQuery) ||
                        (d.Description != null && d.Description.Contains(normalizedQuery)) ||
                        d.Category.Contains(normalizedQuery) ||
                        (d.Tags != null && d.Tags.Contains(normalizedQuery)) ||
                        d.FileName.Contains(normalizedQuery))
            .OrderByDescending(d => d.CreatedDate)
            .Take(50)
            .ToListAsync();
    }

    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return null;

        var normalized = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(", ", normalized);
    }
}

public class DocumentUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Other";
    public string? Tags { get; set; }
    public int? ProjectId { get; set; }
    public IFormFile File { get; set; } = null!;
}

public class DocumentUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public int? ProjectId { get; set; }
}

internal sealed class DiscardingScanQueue : IScanQueue
{
    public ValueTask EnqueueAsync(ContosoDashboard.Models.DocumentScanMessage message, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
