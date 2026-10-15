using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
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

    // Scenario: Files already on the device get the configured date when the dates are repaired
    //   Given a song on the server, created at a known past date, is assigned to the device
    //   And the sync runs, downloading the file with the time of the sync
    //   When the device is set to give downloaded files the song's created date
    //   And the dates are repaired as a dry run
    //   Then the file keeps its date
    //   When the dates are repaired
    //   Then the file has the song's created date
    //   When the sync runs
    //   Then nothing is uploaded or downloaded
    [Fact]
    public async Task RepairDates_ShouldGiveSyncedFileTheConfiguredDate()
    {
        // Seed a song on the server with past dates, associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId], CreatedAt = SongCreatedAt, FileModifiedAt = SongFileModifiedAt }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1, error: 0);
        var path = App.GetAllFiles().ShouldHaveSingleItem();
        var syncedDate = App.GetFileModifiedAt(path);

        // Changing the setting alone does nothing to the file, and neither should a dry run of the repair
        await App.SetFileModifiedDateAsync("ServerCreatedAt");
        var dryRun = await App.RepairDatesAsync(dryRun: true);
        dryRun.ExitCode.ShouldBe(0, dryRun.DescribeCliOutput());
        dryRun.StandardOutput.ShouldContain("1 re-dated", customMessage: dryRun.DescribeCliOutput());
        App.GetFileModifiedAt(path).ShouldBe(syncedDate);

        // Repair the dates - the file should get the song's created date
        var repair = await App.RepairDatesAsync();
        repair.ExitCode.ShouldBe(0, repair.DescribeCliOutput());
        repair.StandardOutput.ShouldContain("1 re-dated", customMessage: repair.DescribeCliOutput());
        App.GetFileModifiedAt(path).ShouldBe(SongCreatedAt, TimeSpan.FromSeconds(1));

        // Run sync again - the older date should count neither as a local change nor as a pending download
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 1, createLocal: 0, updateLocal: 0, updateRemote: 0, error: 0);
        App.GetFileModifiedAt(path).ShouldBe(SongCreatedAt, TimeSpan.FromSeconds(1));
    }

    // Scenario: A file edited on the device keeps its date when the dates are repaired
    //   Given a song on the server, created at a known past date, is assigned to the device
    //   And the sync runs, downloading the file
    //   And the file is edited on the device
    //   When the device is set to give downloaded files the song's created date
    //   And the dates are repaired
    //   Then the file keeps the date of the edit
    //   When the sync runs
    //   Then the edited file is uploaded
    [Fact]
    public async Task RepairDates_ShouldKeepTheDateOfFileEditedOnTheDevice()
    {
        // Seed a song on the server with past dates, associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId], CreatedAt = SongCreatedAt, FileModifiedAt = SongFileModifiedAt }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1, error: 0);
        var path = App.GetAllFiles().ShouldHaveSingleItem();

        // Edit the file on the device, after the sync
        await App.UpdateLocalFileMetadataAsync(path, new EditSongOptions(Title: "Edited On The Device"));
        var editedDate = App.GetFileModifiedAt(path);

        // Repair the dates - re-dating the file would hide the edit from the sync, so it should be left alone
        await App.SetFileModifiedDateAsync("ServerCreatedAt");
        var repair = await App.RepairDatesAsync();
        repair.ExitCode.ShouldBe(0, repair.DescribeCliOutput());
        repair.StandardOutput.ShouldContain("0 re-dated, 1 skipped (1 edited locally)", customMessage: repair.DescribeCliOutput());
        App.GetFileModifiedAt(path).ShouldBe(editedDate);

        // Run sync again - the edit should still be uploaded
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateRemote: 1, error: 0);
    }
}
