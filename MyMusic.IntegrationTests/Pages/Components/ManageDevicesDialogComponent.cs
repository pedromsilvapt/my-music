using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageDevicesDialogComponent(ILocator locator) : BaseComponent(locator)
{
    private ILocator DeviceRow(string deviceName) =>
        Root.Locator("[data-testid^='device-row-']").Filter(new() { HasText = deviceName }).First;

    /// <summary>
    /// The item of a managed song under a device. The device must be expanded for it to be visible.
    /// </summary>
    public ManageSongItemComponent GetSongItem(string deviceName, string songTitle)
    {
        var title = Root.Page.Locator("[data-testid='song-title']",
            new() { HasTextRegex = new Regex($"^{Regex.Escape(songTitle)}$") });

        return new ManageSongItemComponent(
            DeviceRow(deviceName).Locator("[data-testid^='manage-song-item-']").Filter(new() { Has = title }));
    }

    public async Task SelectDeviceAsync(string deviceName, string action)
    {
        var actionLabel = DeviceRow(deviceName).Locator($"label:has-text('{action}')");
        await actionLabel.ClickAsync();
    }

    public async Task ApplyAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task CancelAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task ExpandDeviceAsync(string deviceName)
    {
        await DeviceRow(deviceName).GetByTestId("device-expand-badge").ClickAsync();
    }

    /// <summary>
    /// Types the path of a song on a device. The device is expanded to show its songs.
    /// </summary>
    public async Task SetSongPathAsync(string deviceName, string songTitle, string path)
    {
        await ExpandDeviceAsync(deviceName);
        await GetSongItem(deviceName, songTitle).SetPathAsync(path);
    }

    /// <summary>
    /// Validates the managed songs under a device, which must be expanded.
    /// </summary>
    public async Task ValidateSongsAsync(string deviceName, IEnumerable<SongDeviceValidation> validations)
    {
        foreach (var validation in validations)
        {
            var songItem = GetSongItem(deviceName, validation.SongTitle);
            var isIncluded = await songItem.IsIncludedAsync();

            if (!validation.ShouldExist)
            {
                isIncluded.ShouldBeFalse($"Song '{validation.SongTitle}' should NOT be on device");
                continue;
            }

            isIncluded.ShouldBeTrue($"Song '{validation.SongTitle}' should be included on device");

            if (validation.ExpectedPath is not null)
            {
                var path = await songItem.GetPathAsync();
                path.ShouldBe(validation.ExpectedPath,
                    $"Song '{validation.SongTitle}' should have path '{validation.ExpectedPath}' but was '{path}'");
            }

            if (validation.ExpectedSyncAction is not null)
            {
                var syncAction = await songItem.GetSyncActionAsync();
                syncAction.ShouldBe(validation.ExpectedSyncAction,
                    $"Song '{validation.SongTitle}' should have sync action '{validation.ExpectedSyncAction}' but was '{syncAction}'");
            }
        }
    }
}
