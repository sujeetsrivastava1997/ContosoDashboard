using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public sealed class DocumentUploadTests
{
    [Fact]
    public async Task Upload_rejects_missing_metadata_unsupported_type_and_oversized_files()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();

        var noTitle = await fixture.Service.UploadAsync(4, fixture.Pdf() with { Title = " " }, CancellationToken.None);
        var noCategory = await fixture.Service.UploadAsync(4, fixture.Pdf() with { Category = null }, CancellationToken.None);
        var unsupported = await fixture.Service.UploadAsync(4, fixture.Pdf() with
        {
            OriginalFileName = "payload.exe",
            MimeType = "application/octet-stream"
        }, CancellationToken.None);
        var tooLarge = await fixture.Service.UploadAsync(4, fixture.Pdf() with
        {
            FileSizeBytes = 25L * 1024 * 1024 + 1
        }, CancellationToken.None);

        Assert.Equal("invalid_title", noTitle.ErrorCode);
        Assert.Equal("invalid_category", noCategory.ErrorCode);
        Assert.Equal("unsupported_file_type", unsupported.ErrorCode);
        Assert.Equal("invalid_file_size", tooLarge.ErrorCode);
        Assert.Empty(fixture.Storage.Keys);
        Assert.Empty(fixture.Context.Documents);
    }

    [Fact]
    public async Task Upload_compensates_by_removing_file_when_metadata_save_fails()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        fixture.SaveInterceptor.FailNext = true;

        var result = await fixture.Service.UploadAsync(4, fixture.Pdf(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("upload_failed", result.ErrorCode);
        Assert.Empty(fixture.Storage.Keys);
        Assert.Empty(fixture.Context.Documents);
        Assert.Empty(fixture.Context.DocumentActivities);
    }

    [Fact]
    public async Task Task_upload_inherits_project_and_invalid_project_associations_are_rejected()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();

        var inherited = await fixture.Service.UploadAsync(4, fixture.Pdf(taskId: 1), CancellationToken.None);
        var mismatched = await fixture.Service.UploadAsync(4, fixture.Pdf(projectId: 999, taskId: 1), CancellationToken.None);
        var inaccessible = await fixture.Service.UploadAsync(5, fixture.Pdf(projectId: 1), CancellationToken.None);

        Assert.True(inherited.Succeeded);
        Assert.Equal(1, inherited.Document!.ProjectId);
        Assert.Equal(1, inherited.Document.TaskId);
        Assert.Equal("invalid_project", mismatched.ErrorCode);
        Assert.Equal("invalid_project", inaccessible.ErrorCode);
    }
}
