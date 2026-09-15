namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService()
    {
        var appRoot = Path.Combine(AppContext.BaseDirectory, "AppData", "uploads");
        _basePath = Path.GetFullPath(appRoot);

        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required", nameof(fileName));

        var safeFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeFileName);
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_basePath, uniqueName);

        await using var output = File.Create(fullPath);
        fileStream.Position = 0;
        await fileStream.CopyToAsync(output);

        return fullPath;
    }

    public Task DeleteAsync(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("The requested document was not found.", filePath);
        }

        var stream = File.OpenRead(filePath);
        return Task.FromResult<Stream>(stream);
    }
}
