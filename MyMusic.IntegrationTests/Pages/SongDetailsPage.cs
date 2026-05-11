using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class SongDetailsPage(IPage page) : BasePage(page, "song-detail")
{
    public ILocator Title => Root.Locator("text[size='xl'], h1, [data-size='xl']").First;

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    public async Task<string> GetTitleAsync()
    {
        return await Title.InnerTextAsync();
    }

    public async Task<string[]> GetArtistsAsync()
    {
        var artistLinks = Root.Locator("a[href^='/artists/']");
        var count = await artistLinks.CountAsync();
        var artists = new List<string>();
        for (int i = 0; i < count; i++)
        {
            artists.Add(await artistLinks.Nth(i).InnerTextAsync());
        }
        return artists.ToArray();
    }

    public async Task<string?> GetAlbumAsync()
    {
        var albumLink = Root.Locator("a[href^='/albums/']");
        var count = await albumLink.CountAsync();
        if (count == 0)
            return null;
        return await albumLink.First.InnerTextAsync();
    }

    public async Task<int?> GetYearAsync()
    {
        var yearElement = Root.Locator("[data-testid=\"song-year\"]");
        var count = await yearElement.CountAsync();
        if (count == 0)
            return null;

        var text = await yearElement.InnerTextAsync();
        return int.TryParse(text, out var year) ? year : null;
    }

    public async Task<bool> GetExplicitAsync()
    {
        var explicitLabel = Root.GetByText("Explicit");
        return await explicitLabel.CountAsync() > 0;
    }

    public async Task<string[]> GetGenresAsync()
    {
        var genreItems = Root.Locator("[data-testid=\"song-genres\"] [data-testid=\"genre-item\"]");
        var count = await genreItems.CountAsync();
        var genres = new List<string>();
        for (int i = 0; i < count; i++)
        {
            genres.Add(await genreItems.Nth(i).InnerTextAsync());
        }
        return genres.ToArray();
    }

    public async Task<DeviceBadgeComponent?> GetDeviceBadgeAsync(string deviceName)
    {
        var badgeLocator = Root.Locator("[data-testid='device-badge']").Filter(new()
        {
            HasText = deviceName
        });

        var count = await badgeLocator.CountAsync();
        if (count == 0)
        {
            // Fallback to a broader selector if data-testid is not available yet
            badgeLocator = Root.Locator("div, span").Filter(new()
            {
                HasText = deviceName
            }).Filter(new()
            {
                Has = Root.Locator("svg")
            });
            count = await badgeLocator.CountAsync();
            if (count == 0)
                return null;
        }

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
        var badges = Root.Locator("div").Filter(new()
        {
            Has = Root.Locator("svg[data-icon^='Icon']")
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
        await Root.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();
        return new EditSongModalComponent(dialog);
    }

    public async Task<ManagePlaylistsDialogComponent> OpenManagePlaylistsDialogAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Manage Playlists" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();
        return new ManagePlaylistsDialogComponent(dialog);
    }

    /// <summary>
    /// Clicks the Delete button on the song detail page and returns the confirmation dialog component.
    /// </summary>
    public async Task<ConfirmDialogComponent> ClickDeleteButtonAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();
        return new ConfirmDialogComponent(dialog);
    }
}
