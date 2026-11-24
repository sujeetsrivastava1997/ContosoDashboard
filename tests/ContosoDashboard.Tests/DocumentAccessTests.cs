using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public sealed class DocumentAccessTests
{
    [Fact]
    public async Task Preview_and_download_require_access_and_preview_restricts_mime_types()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var pdf = await fixture.Service.UploadAsync(4, fixture.Pdf(), CancellationToken.None);
        var word = await fixture.Service.UploadAsync(4, fixture.Pdf("Word file") with
        {
            OriginalFileName = "document.docx",
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        }, CancellationToken.None);

        var denied = await fixture.Service.OpenDownloadAsync(pdf.Document!.DocumentId, 7, CancellationToken.None);
        var preview = await fixture.Service.OpenPreviewAsync(pdf.Document.DocumentId, 4, CancellationToken.None);
        var download = await fixture.Service.OpenDownloadAsync(pdf.Document.DocumentId, 4, CancellationToken.None);
        var unsupportedPreview = await fixture.Service.OpenPreviewAsync(word.Document!.DocumentId, 4, CancellationToken.None);

        Assert.Equal("forbidden", denied.ErrorCode);
        Assert.True(preview.Succeeded);
        Assert.True(download.Succeeded);
        Assert.Equal("preview_unsupported", unsupportedPreview.ErrorCode);
        await preview.Stream!.DisposeAsync();
        await download.Stream!.DisposeAsync();
    }
}
