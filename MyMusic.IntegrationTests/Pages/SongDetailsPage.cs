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

    public async Task<string[]> GetArtistsAsync()
    {
        var artistLinks = Page.Locator("a[href^='/artists/']");
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
        var albumLink = Page.Locator("a[href^='/albums/']");
        var count = await albumLink.CountAsync();
        if (count == 0)
            return null;
        return await albumLink.First.InnerTextAsync();
    }

    public async Task<int?> GetYearAsync()
    {
        var yearElement = Page.Locator("[data-testid=\"song-year\"]");
        var count = await yearElement.CountAsync();
        if (count == 0)
            return null;
        
        var text = await yearElement.InnerTextAsync();
        return int.TryParse(text, out var year) ? year : null;
    }

    public async Task<bool> GetExplicitAsync()
    {
        var explicitLabel = Page.GetByText("Explicit");
        return await explicitLabel.CountAsync() > 0;
    }

    public async Task<string[]> GetGenresAsync()
    {
        var genreItems = Page.Locator("[data-testid=\"song-genres\"] [data-testid=\"genre-item\"]");
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
        var badgeLocator = Page.Locator("[data-testid='device-badge']").Filter(new()
        {
            HasText = deviceName
        });

        var count = await badgeLocator.CountAsync();
        if (count == 0)
        {
            // Fallback to a broader selector if data-testid is not available yet
            badgeLocator = Page.Locator("div, span").Filter(new()
            {
                HasText = deviceName
            }).Filter(new()
            {
                Has = Page.Locator("svg")
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

    public async Task<ManagePlaylistsDialogComponent> OpenManagePlaylistsDialogAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Manage Playlists" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync(new() { Timeout = 5000 });
        return new ManagePlaylistsDialogComponent(dialog);
    }
}
