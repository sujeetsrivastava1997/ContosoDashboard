namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream fileStream, string extension, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}
