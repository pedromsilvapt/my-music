using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    private static readonly DateTime SongCreatedAt = new(2019, 7, 15, 8, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime SongFileModifiedAt = new(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    // Scenario: A downloaded file gets the date the device is set to give it
    //   Given a song on the server, created and last changed at known past dates, is assigned to the device
    //   And the device is set to give downloaded files the song's modified (or created) date
    //   When the sync runs
    //   Then the song is downloaded and the file has that date
    //   When the sync runs again
    //   Then nothing is uploaded or downloaded
    [Theory]
    [InlineData("ServerModifiedAt")]
    [InlineData("ServerCreatedAt")]
    public async Task Sync_ShouldGiveDownloadedFileTheConfiguredDate(string source)
    {
        // Seed a song on the server with past dates, associated with this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId], CreatedAt = SongCreatedAt, FileModifiedAt = SongFileModifiedAt }]);
        await App.SetFileModifiedDateAsync(source);

        // Run sync - the song should be downloaded
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1, error: 0);

        // The file should have the song's date instead of the time of the sync
        var path = App.GetAllFiles().ShouldHaveSingleItem();
        var expectedDate = source == "ServerCreatedAt" ? SongCreatedAt : SongFileModifiedAt;
        App.GetFileModifiedAt(path).ShouldBe(expectedDate, TimeSpan.FromSeconds(1));

        // Run sync again - the older date should count neither as a local change nor as a pending download
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 1, createLocal: 0, updateLocal: 0, updateRemote: 0, error: 0);
        App.GetFileModifiedAt(path).ShouldBe(expectedDate, TimeSpan.FromSeconds(1));
    }

    // Scenario: A downloaded file keeps the time of the sync by default
    //   Given a song on the server, created and last changed at known past dates, is assigned to the device
    //   When the sync runs
    //   Then the song is downloaded and the file's date is the time of the sync
    [Fact]
    public async Task Sync_ShouldGiveDownloadedFileTheSyncDateByDefault()
    {
        // Seed a song on the server with past dates, associated with this device
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId], CreatedAt = SongCreatedAt, FileModifiedAt = SongFileModifiedAt }]);
        var beforeSync = DateTime.UtcNow.AddSeconds(-2);

        // Run sync - the song should be downloaded
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBe(createLocal: 1, error: 0);

        // The file should be dated when it was downloaded, not with any of the song's dates
        var path = App.GetAllFiles().ShouldHaveSingleItem();
        App.GetFileModifiedAt(path).ShouldBeGreaterThan(beforeSync);
    }
}
