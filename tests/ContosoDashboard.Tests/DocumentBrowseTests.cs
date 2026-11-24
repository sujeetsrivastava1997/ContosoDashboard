using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public sealed class DocumentBrowseTests
{
    [Fact]
    public async Task List_search_sort_and_filter_exclude_unauthorized_documents()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var first = await fixture.Service.UploadAsync(4, fixture.Pdf("Alpha architecture", projectId: 1), CancellationToken.None);
        var second = await fixture.Service.UploadAsync(4, fixture.Pdf("Beta testing", projectId: 1) with
        {
            Category = DocumentCategory.Testing
        }, CancellationToken.None);

        var outsiderResults = await fixture.Service.GetAccessibleAsync(7, new DocumentFilter(), CancellationToken.None);
        var searchResults = await fixture.Service.GetAccessibleAsync(2, new DocumentFilter { Search = "architecture" }, CancellationToken.None);
        var categoryResults = await fixture.Service.GetAccessibleAsync(2, new DocumentFilter
        {
            ProjectId = 1,
            Category = DocumentCategory.Testing,
            SortBy = DocumentSortField.Title
        }, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Empty(outsiderResults);
        Assert.Single(searchResults);
        Assert.Equal("Alpha architecture", searchResults[0].Title);
        Assert.Single(categoryResults);
        Assert.Equal("Beta testing", categoryResults[0].Title);
    }
}
