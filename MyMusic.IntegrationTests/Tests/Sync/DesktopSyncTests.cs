using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;
using MyMusic.OpenTelemetry.XUnit;
using Microsoft.Extensions.Configuration;

namespace MyMusic.IntegrationTests.Tests.Sync;

public class DesktopSyncTests(ITestOutputHelper output) : SyncTestsBase(output)
{
    protected override ISyncApplication CreateApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry)
        => new DesktopCliApplication(configuration, telemetry);

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
