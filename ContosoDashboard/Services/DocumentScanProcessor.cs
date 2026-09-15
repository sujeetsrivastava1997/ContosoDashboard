using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public interface IDocumentScanProcessor
{
    Task ProcessAsync(DocumentScanMessage message, CancellationToken cancellationToken = default);
}

public sealed class DocumentScanProcessor : IDocumentScanProcessor
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IMalwareScanner _scanner;

    public DocumentScanProcessor(ApplicationDbContext context, IFileStorageService fileStorage, IMalwareScanner scanner)
    {
        _context = context;
        _fileStorage = fileStorage;
        _scanner = scanner;
    }

    public async Task ProcessAsync(DocumentScanMessage message, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.DocumentId == message.DocumentId, cancellationToken);
        if (document == null || document.IsDeleted || document.ScanStatus != DocumentScanStatus.PendingScan || document.ScanAttemptId != message.UploadAttemptId)
            return;

        try
        {
            await using var content = await _fileStorage.DownloadAsync(message.StoragePath);
            var result = await _scanner.ScanAsync(content, document.FileName, message.MimeType, cancellationToken);

            document.ScanStatus = result.IsClean ? DocumentScanStatus.Available : DocumentScanStatus.Rejected;
            document.ScanResult = result.Detail;
            document.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            document.ScanStatus = DocumentScanStatus.ScanFailed;
            document.ScanResult = exception.Message[..Math.Min(exception.Message.Length, 500)];
            document.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
