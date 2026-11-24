using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public sealed class DocumentManagementTests
{
    [Fact]
    public async Task Owner_and_same_department_team_lead_can_update_metadata_and_tags()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var upload = await fixture.Service.UploadAsync(4, fixture.Pdf(), CancellationToken.None);

        var result = await fixture.Service.UpdateMetadataAsync(upload.Document!.DocumentId, 3, new DocumentMetadataRequest
        {
            Title = "Updated title",
            Description = "Reviewed",
            Category = DocumentCategory.Design,
            Tags = [" Architecture ", "architecture", " UX "]
        }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Updated title", result.Document!.Title);
        Assert.Equal(DocumentCategory.Design, result.Document.Category);
        Assert.Equal(["architecture", "ux"], result.Document.Tags.Select(tag => tag.Tag).Order().ToArray());
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(2, true)]
    [InlineData(7, false)]
    public async Task Delete_permissions_cover_owner_team_lead_and_project_manager(int requesterId, bool allowed)
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var upload = await fixture.Service.UploadAsync(4, fixture.Pdf(projectId: 1), CancellationToken.None);

        var result = await fixture.Service.DeleteAsync(upload.Document!.DocumentId, requesterId, CancellationToken.None);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task Replacement_compensates_failure_and_removes_old_file_after_success()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var upload = await fixture.Service.UploadAsync(4, fixture.Pdf(), CancellationToken.None);
        var document = upload.Document!;
        var originalKey = document.StorageKey;

        fixture.SaveInterceptor.FailNext = true;
        var failed = await fixture.Service.ReplaceAsync(document.DocumentId, 4, new DocumentReplacementRequest
        {
            OriginalFileName = "replacement.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = DocumentTestFixture.PdfBytes.Length,
            FileStream = new MemoryStream(DocumentTestFixture.PdfBytes)
        }, CancellationToken.None);
        Assert.False(failed.Succeeded);
        Assert.Single(fixture.Storage.Keys);
        Assert.Contains(originalKey, fixture.Storage.Keys);

        var succeeded = await fixture.Service.ReplaceAsync(document.DocumentId, 4, new DocumentReplacementRequest
        {
            OriginalFileName = "replacement.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = DocumentTestFixture.PdfBytes.Length,
            FileStream = new MemoryStream(DocumentTestFixture.PdfBytes)
        }, CancellationToken.None);
        Assert.True(succeeded.Succeeded);
        Assert.Single(fixture.Storage.Keys);
        Assert.DoesNotContain(originalKey, fixture.Storage.Keys);
    }
}
