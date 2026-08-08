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

    public async Task<long> GetIdAsync()
    {
        var url = Page.Url.TrimEnd('/').Split('/').Last();
        return long.Parse(url);
    }

    public async Task<AlbumDetailsPage> ClickAlbumLinkAsync()
    {
        await Root.Locator("a[href^='/albums/']").First.ClickAsync();
        var albumPage = new AlbumDetailsPage(Page);
        await albumPage.WaitForLoadedAsync();
        return albumPage;
    }

    public async Task<ArtistDetailsPage> ClickArtistLinkAsync(int index = 0)
    {
        await Root.Locator("a[href^='/artists/']").Nth(index).ClickAsync();
        var artistPage = new ArtistDetailsPage(Page);
        await artistPage.WaitForLoadedAsync();
        return artistPage;
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

    /// <summary>
    /// The album's id parsed from the album anchor's href, or null when the song has no album link.
    /// </summary>
    public async Task<long?> GetAlbumIdAsync()
    {
        var albumLink = Root.Locator("a[href^='/albums/']");
        var count = await albumLink.CountAsync();
        if (count == 0)
            return null;
        var href = await albumLink.First.GetAttributeAsync("href");
        return href is null ? null : long.Parse(href.TrimEnd('/').Split('/').Last());
    }

    /// <summary>
    /// The first artist's id parsed from its anchor href.
    /// </summary>
    public async Task<long?> GetArtistIdAsync()
    {
        var artistLink = Root.Locator("a[href^='/artists/']");
        var count = await artistLink.CountAsync();
        if (count == 0)
            return null;
        var href = await artistLink.First.GetAttributeAsync("href");
        return href is null ? null : long.Parse(href.TrimEnd('/').Split('/').Last());
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

    public async Task<string?> GetRepositoryPathAsync()
    {
        var element = Root.Locator("[data-testid='song-repository-path']");
        var count = await element.CountAsync();
        if (count == 0) return null;
        return await element.InnerTextAsync();
    }

    public async Task<DeviceBadgeComponent?> GetDeviceBadgeAsync(string deviceName)
    {
        var badgeLocator = Root.Locator("[data-testid='device-badge']").Filter(new()
        {
            HasText = deviceName
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
        var badges = Root.Locator("[data-testid='device-badge']");

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

    public async Task<ManageSharingDialogComponent> OpenManageSharingDialogAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Manage Sharing" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();
        return new ManageSharingDialogComponent(dialog);
    }

    public async Task DownloadAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Download" }).ClickAsync();
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
