using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ContosoDashboard.Tests;

public sealed class DocumentTestFixture : IAsyncDisposable
{
    private DocumentTestFixture(ApplicationDbContext context, FailNextSaveInterceptor saveInterceptor)
    {
        Context = context;
        SaveInterceptor = saveInterceptor;
        Storage = new MemoryFileStorage();
        Service = new DocumentService(context, Storage, new AcceptSafeFilesScanner());
    }

    public ApplicationDbContext Context { get; }
    public FailNextSaveInterceptor SaveInterceptor { get; }
    public MemoryFileStorage Storage { get; }
    public IDocumentService Service { get; }

    public static async Task<DocumentTestFixture> CreateAsync()
    {
        var interceptor = new FailNextSaveInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"documents-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Users.AddRange(
            new User { UserId = 5, Email = "alex@contoso.com", DisplayName = "Alex Employee", Department = "Engineering", Role = UserRole.Employee },
            new User { UserId = 7, Email = "outside@contoso.com", DisplayName = "Outside Employee", Department = "Sales", Role = UserRole.Employee });
        await context.SaveChangesAsync();
        return new DocumentTestFixture(context, interceptor);
    }

    public DocumentUploadRequest Pdf(string title = "Test document", int? projectId = null, int? taskId = null) =>
        new()
        {
            Title = title,
            Category = DocumentCategory.General,
            ProjectId = projectId,
            TaskId = taskId,
            OriginalFileName = "document.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = PdfBytes.Length,
            FileStream = new MemoryStream(PdfBytes)
        };

    public static byte[] PdfBytes => "%PDF-1.7\nfixture"u8.ToArray();

    public async ValueTask DisposeAsync() => await Context.DisposeAsync();
}

public sealed class MemoryFileStorage : IFileStorageService
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => _files.Keys.ToArray();

    public async Task<string> SaveAsync(Stream fileStream, string extension, CancellationToken cancellationToken)
    {
        var key = $"{Guid.NewGuid():N}{extension}";
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        _files.Add(key, buffer.ToArray());
        return key;
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult(_files.Remove(storageKey));

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(_files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes, writable: false) : null);
}

public sealed class AcceptSafeFilesScanner : IMalwareScanService
{
    public Task<bool> IsSafeAsync(Stream fileStream, string originalFileName, string mimeType, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}

public sealed class FailNextSaveInterceptor : SaveChangesInterceptor
{
    public bool FailNext { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new InvalidOperationException("Simulated metadata persistence failure.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
