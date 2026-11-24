using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public sealed class DocumentShareTests
{
    [Fact]
    public async Task Team_shares_snapshot_membership_and_revocation_immediately_removes_access()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var upload = await fixture.Service.UploadAsync(4, fixture.Pdf(), CancellationToken.None);
        var id = upload.Document!.DocumentId;

        var shared = await fixture.Service.ShareWithTeamAsync(id, 4, "Engineering", CancellationToken.None);
        var snapshotRecipientAccess = await fixture.Service.CanViewAsync(id, 5, CancellationToken.None);
        fixture.Context.Users.Add(new User
        {
            UserId = 6,
            Email = "new@contoso.com",
            DisplayName = "New Employee",
            Department = "Engineering",
            Role = UserRole.Employee
        });
        await fixture.Context.SaveChangesAsync();
        var laterMemberAccess = await fixture.Service.CanViewAsync(id, 6, CancellationToken.None);
        var outsiderAccess = await fixture.Service.CanViewAsync(id, 7, CancellationToken.None);
        var revoked = await fixture.Service.RevokeTeamShareAsync(id, 4, "Engineering", CancellationToken.None);
        var accessAfterRevoke = await fixture.Service.CanViewAsync(id, 5, CancellationToken.None);

        Assert.True(shared.Succeeded);
        Assert.True(snapshotRecipientAccess);
        Assert.False(laterMemberAccess);
        Assert.False(outsiderAccess);
        Assert.True(revoked.Succeeded);
        Assert.False(accessAfterRevoke);
    }

    [Fact]
    public async Task Only_owner_can_share_or_revoke_a_direct_recipient()
    {
        await using var fixture = await DocumentTestFixture.CreateAsync();
        var upload = await fixture.Service.UploadAsync(4, fixture.Pdf(), CancellationToken.None);
        var id = upload.Document!.DocumentId;

        var deniedShare = await fixture.Service.ShareWithUserAsync(id, 5, 6, CancellationToken.None);
        var share = await fixture.Service.ShareWithUserAsync(id, 4, 5, CancellationToken.None);
        var recipientAccess = await fixture.Service.CanViewAsync(id, 5, CancellationToken.None);
        var revoke = await fixture.Service.RevokeUserShareAsync(id, 4, 5, CancellationToken.None);
        var accessAfterRevoke = await fixture.Service.CanViewAsync(id, 5, CancellationToken.None);

        Assert.Equal("forbidden", deniedShare.ErrorCode);
        Assert.True(share.Succeeded);
        Assert.True(recipientAccess);
        Assert.True(revoke.Succeeded);
        Assert.False(accessAfterRevoke);
    }
}
