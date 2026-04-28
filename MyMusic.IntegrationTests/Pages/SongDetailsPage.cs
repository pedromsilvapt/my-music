using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class SongDetailsPage(IPage page) : BasePage(page)
{
    public ILocator Title => Page.Locator("text[size='xl'], h1, [data-size='xl']").First;

    public async Task WaitForLoadedAsync()
    {
        await Title.WaitForAsync(new() { Timeout = 10000 });
    }

    public async Task<string> GetTitleAsync()
    {
        return await Title.InnerTextAsync();
    }

    public async Task<DeviceBadgeComponent?> GetDeviceBadgeAsync(string deviceName)
    {
        var badgeLocator = Page.Locator("div, span").Filter(new()
        {
            HasText = deviceName
        }).Filter(new()
        {
            Has = Page.Locator("svg")
        });

        var count = await badgeLocator.CountAsync();
        if (count == 0)
            return null;

        return new DeviceBadgeComponent(badgeLocator.First);
    }

    public async Task<bool> HasDeviceAsync(string deviceName)
    {
        var badge = await GetDeviceBadgeAsync(deviceName);
        if (badge == null)
            return false;

        var syncAction = await badge.GetSyncActionAsync();
        return syncAction != "Remove";
    }

    public async Task<string?> GetDeviceSyncActionAsync(string deviceName)
    {
        var badge = await GetDeviceBadgeAsync(deviceName);
        return badge is not null
            ? await badge.GetSyncActionAsync()
            : null;
    }

    public async Task<List<DeviceBadgeComponent>> GetAllDeviceBadgesAsync()
    {
        var badges = Page.Locator("div").Filter(new()
        {
            Has = Page.Locator("svg[data-icon^='Icon']")
        });

        var count = await badges.CountAsync();
        var result = new List<DeviceBadgeComponent>();
        for (int i = 0; i < count; i++)
        {
            result.Add(new DeviceBadgeComponent(badges.Nth(i)));
        }
        return result;
    }

    public async Task<EditSongModalComponent> OpenEditModalAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync(new() { Timeout = 5000 });
        return new EditSongModalComponent(dialog);
    }
}
