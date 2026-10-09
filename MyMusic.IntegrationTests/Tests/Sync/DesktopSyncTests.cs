using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;
using MyMusic.OpenTelemetry.XUnit;
using Microsoft.Extensions.Configuration;

namespace MyMusic.IntegrationTests.Tests.Sync;

public class DesktopSyncTests(ITestOutputHelper output) : SyncTestsBase(output)
{
    private static readonly DateTime RepairSongCreatedAt = new(2019, 7, 15, 8, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime RepairSongFileModifiedAt = new(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private DesktopCliApplication Cli => (DesktopCliApplication)App;

    protected override ISyncApplication CreateApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry)
        => new DesktopCliApplication(configuration, telemetry);

    // Scenario: Files already on the device get the configured date when the dates are repaired
    //   Given a song on the server, created at a known past date, is assigned to the device
    //   And the CLI sync runs, downloading the file with the time of the sync
    //   When the device is set to give downloaded files the song's created date
    //   And the dates are repaired as a dry run
    //   Then the file keeps its date
    //   When the dates are repaired
    //   Then the file has the song's created date
    //   When the CLI sync runs
    //   Then nothing is uploaded or downloaded
    [Fact]
    public async Task RepairDates_ShouldGiveSyncedFileTheConfiguredDate()
    {
        // Seed a song on the server with past dates, associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId], CreatedAt = RepairSongCreatedAt, FileModifiedAt = RepairSongFileModifiedAt }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1, error: 0);
        var path = App.GetAllFiles().ShouldHaveSingleItem();
        var syncedDate = App.GetFileModifiedAt(path);

        // Changing the setting alone does nothing to the file, and neither should a dry run of the repair
        await App.SetFileModifiedDateAsync("ServerCreatedAt");
        var dryRun = await Cli.RepairDatesAsync(dryRun: true);
        dryRun.ExitCode.ShouldBe(0, dryRun.DescribeCliOutput());
        dryRun.StandardOutput.ShouldContain("1 re-dated", customMessage: dryRun.DescribeCliOutput());
        App.GetFileModifiedAt(path).ShouldBe(syncedDate);

        // Repair the dates - the file should get the song's created date
        var repair = await Cli.RepairDatesAsync();
        repair.ExitCode.ShouldBe(0, repair.DescribeCliOutput());
        repair.StandardOutput.ShouldContain("1 re-dated", customMessage: repair.DescribeCliOutput());
        App.GetFileModifiedAt(path).ShouldBe(RepairSongCreatedAt, TimeSpan.FromSeconds(1));

        // Run sync again - the older date should count neither as a local change nor as a pending download
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(skipped: 1, createLocal: 0, updateLocal: 0, updateRemote: 0, error: 0);
        App.GetFileModifiedAt(path).ShouldBe(RepairSongCreatedAt, TimeSpan.FromSeconds(1));
    }

    // Scenario: A file edited on the device keeps its date when the dates are repaired
    //   Given a song on the server, created at a known past date, is assigned to the device
    //   And the CLI sync runs, downloading the file
    //   And the file is edited on the device
    //   When the device is set to give downloaded files the song's created date
    //   And the dates are repaired
    //   Then the file keeps the date of the edit
    //   When the CLI sync runs
    //   Then the edited file is uploaded
    [Fact]
    public async Task RepairDates_ShouldKeepTheDateOfFileEditedOnTheDevice()
    {
        // Seed a song on the server with past dates, associated with this device, and download it
        await ServerSongs.SeedAsync(RequestContext, UserId,
            [SongsFixture.DefaultSongs[1] with { DeviceIds = [App.DeviceId], CreatedAt = RepairSongCreatedAt, FileModifiedAt = RepairSongFileModifiedAt }]);
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBe(createLocal: 1, error: 0);
        var path = App.GetAllFiles().ShouldHaveSingleItem();

        // Edit the file on the device, after the sync
        await App.UpdateLocalFileMetadataAsync(path, new EditSongOptions(Title: "Edited On The Device"));
        var editedDate = App.GetFileModifiedAt(path);

        // Repair the dates - re-dating the file would hide the edit from the sync, so it should be left alone
        await App.SetFileModifiedDateAsync("ServerCreatedAt");
        var repair = await Cli.RepairDatesAsync();
        repair.ExitCode.ShouldBe(0, repair.DescribeCliOutput());
        repair.StandardOutput.ShouldContain("0 re-dated, 1 skipped (1 edited locally)", customMessage: repair.DescribeCliOutput());
        App.GetFileModifiedAt(path).ShouldBe(editedDate);

        // Run sync again - the edit should still be uploaded
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBe(updateRemote: 1, error: 0);
    }

    // Scenario: A sync fails when the server has no device with the name the CLI is configured with
    //   Given the CLI is configured with the name of a device of the server
    //   And a song exists on the device
    //   When the device is renamed in the web app
    //   And the CLI sync runs
    //   Then the sync fails, telling the user to create the device in the web app
    //   And nothing is uploaded to the server
    //   And the device was not created again with the old name
    [Fact]
    public async Task Sync_DeviceNameNotOnTheServer_ShouldFailTellingToCreateItOnTheWeb()
    {
        // Setup: a song on the device, which a sync would upload
        var song = SongsFixture.DefaultSongs[0];
        await App.CreateSongAsync(song);

        // Renaming the device on the web leaves the CLI configured with a name no device has
        var oldName = App.DeviceName;
        await new EditDeviceFlow(oldName, new(Name: "Renamed On The Web")).ExecuteAsync(Page);

        // The sync should fail, saying where devices are created: the CLI never creates them
        var result = await App.SyncAsync(new SyncOptions());

        result.Success.ShouldBeFalse(result.DescribeCliOutput());
        result.StandardOutput.ShouldContain($"Device '{oldName}' not found", customMessage: result.DescribeCliOutput());
        // The console wraps the message, so only a part of it that fits in a line is checked
        result.StandardOutput.ShouldContain("Devices > New device", customMessage: result.DescribeCliOutput());

        // Nothing should reach the server, and the old name should not come back as a new device
        await new ShouldSongExistFlow(song.Title!, shouldExist: false).ExecuteAsync(Page);
        await new ValidateDevicesNamedFlow(oldName, count: 0).ExecuteAsync(Page);
        await new ValidateDevicesNamedFlow("Renamed On The Web", count: 1).ExecuteAsync(Page);
    }
}
