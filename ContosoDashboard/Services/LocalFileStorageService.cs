using System.Text.RegularExpressions;

namespace ContosoDashboard.Services;

public sealed class LocalFileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    private readonly string _uploadRoot = Path.Combine(environment.ContentRootPath, "AppData", "uploads");

    public async Task<string> SaveAsync(Stream fileStream, string extension, CancellationToken cancellationToken)
    {
        if (extension.Length is < 2 or > 5 || extension[0] != '.' ||
            extension[1..].Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("A validated file extension is required.", nameof(extension));
        }

        Directory.CreateDirectory(_uploadRoot);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_uploadRoot, storageKey);

        try
        {
            await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await fileStream.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
            return storageKey;
        }
        catch
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidStorageKey(storageKey))
        {
            return Task.FromResult(false);
        }

        var fullPath = Path.Combine(_uploadRoot, storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidStorageKey(storageKey))
        {
            return Task.FromResult<Stream?>(null);
        }

        var fullPath = Path.Combine(_uploadRoot, storageKey);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous)
            : null;
        return Task.FromResult(stream);
    }

    private static bool IsValidStorageKey(string storageKey) =>
        !string.IsNullOrWhiteSpace(storageKey) &&
        Regex.IsMatch(storageKey, @"\A[0-9a-fA-F]{32}\.(pdf|docx?|xlsx?|pptx?|txt|jpe?g|png)\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
