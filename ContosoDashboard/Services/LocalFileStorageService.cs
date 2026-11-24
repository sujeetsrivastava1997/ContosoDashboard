using System.Security.Cryptography;

namespace ContosoDashboard.Services;

public sealed class LocalFileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    private readonly string _uploadRoot = Path.Combine(environment.ContentRootPath, "AppData", "uploads");

    public async Task<string> SaveAsync(Stream fileStream, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_uploadRoot);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_uploadRoot, storageKey);

        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
        return storageKey;
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.Combine(_uploadRoot, storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }
}
