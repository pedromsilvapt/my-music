using Microsoft.Playwright;
using MyMusic.IntegrationTests.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageDevicesDialogComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task SelectDeviceAsync(string deviceName, string action)
    {
        var deviceRow = Root.Locator("[data-testid^='device-row-']").Filter(new() { HasText = deviceName }).First;
        var actionLabel = deviceRow.Locator($"label:has-text('{action}')");
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
        var deviceRow = Root.Locator($"[data-testid^='device-row-']").Filter(new() { HasText = deviceName }).First;
        var expandBadge = deviceRow.Locator("[data-testid='device-expand-badge']");
        await expandBadge.ClickAsync();
    }

    public async Task<ManageSongItemComponent?> GetSongItemAsync(string songTitle)
    {
        var songItems = Root.Locator("[data-testid^='manage-song-item-']");
        var count = await songItems.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var item = songItems.Nth(i);
            var titleElement = item.Locator("span[fw='500']").First;
            var title = await titleElement.TextContentAsync();
            if (title == songTitle)
            {
                return new ManageSongItemComponent(item);
            }
        }

        return null;
    }

    public async Task ValidateSongsAsync(IEnumerable<SongDeviceValidation> validations)
    {
        foreach (var validation in validations)
        {
            var songItem = await GetSongItemAsync(validation.SongTitle);

            if (validation.ShouldExist)
            {
                songItem.ShouldNotBeNull($"Song '{validation.SongTitle}' should be on device");
                var isIncluded = await songItem!.IsIncludedAsync();
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
            else
            {
                songItem.ShouldBeNull($"Song '{validation.SongTitle}' should NOT be on device");
            }
        }
    }
}
