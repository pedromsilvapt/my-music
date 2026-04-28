using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

public class ShouldSongExistInDeviceFlow(
    string songTitle,
    string deviceName,
    bool shouldExist,
    string? syncAction = null,
    bool shouldHaveNoSyncAction = false) : IFlow
{
    private readonly OpenSongDetailsFlow _openSongDetailsFlow = new(songTitle);

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = await _openSongDetailsFlow.ExecuteAsync(page);

        var hasDevice = await songDetails.HasDeviceAsync(deviceName);

        if (shouldExist)
        {
            hasDevice.ShouldBeTrue($"Song '{songTitle}' should be on device '{deviceName}'");
        }
        else
        {
            hasDevice.ShouldBeFalse($"Song '{songTitle}' should NOT be on device '{deviceName}'");
        }

        if (syncAction != null)
        {
            var actualSyncAction = await songDetails.GetDeviceSyncActionAsync(deviceName);
            actualSyncAction.ShouldBe(syncAction,
                $"Device '{deviceName}' should have sync action '{syncAction}' but was '{actualSyncAction}'");
        }
        else if (shouldHaveNoSyncAction)
        {
            var actualSyncAction = await songDetails.GetDeviceSyncActionAsync(deviceName);
            actualSyncAction.ShouldBeNullOrEmpty(
                $"Device '{deviceName}' should have no sync action but was '{actualSyncAction}'");
        }
    }
}
