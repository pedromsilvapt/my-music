using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class NavbarComponent(ILocator root) : BaseComponent(root)
{
    public ILocator SongsLink => Root.GetByTestId("nav-songs");
    public ILocator AlbumsLink => Root.GetByTestId("nav-albums");
    public ILocator ArtistsLink => Root.GetByTestId("nav-artists");
    public ILocator PlaylistsLink => Root.GetByTestId("nav-playlists");
    public ILocator DevicesLink => Root.GetByTestId("nav-devices");
    public ILocator HistoryLink => Root.GetByTestId("nav-history");
    public ILocator AuditsLink => Root.GetByTestId("nav-audits");
    public ILocator PurchasesLink => Root.GetByTestId("nav-purchases");
    public ILocator SettingsLink => Root.GetByTestId("nav-settings");
    public ILocator HomeLink => Root.GetByTestId("nav-home");
    public ILocator PlayerLink => Root.GetByTestId("nav-player");
    public ILocator MineSongsLink => Root.GetByTestId("nav-songs-mine");
    public ILocator SharedSongsLink(long sharerId) => Root.GetByTestId($"nav-songs-shared-{sharerId}");

    /// <summary>
    /// Waits for the navbar's sharers query to resolve, so the Songs sub-menus reflect the current shares.
    /// </summary>
    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync();
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false");
    }

    /// <summary>
    /// Whether the Songs nav item has any sub-menus (sharer links + "Mine").
    /// The sub-menus render only when at least one user has shared with the current user.
    /// </summary>
    public async Task<bool> HasSongsSubMenusAsync()
    {
        await WaitForLoadedAsync();
        return await MineSongsLink.CountAsync() > 0;
    }

    /// <summary>
    /// Whether a sharer sub-menu link for the given sharer is present under the Songs nav item.
    /// </summary>
    public async Task<bool> HasSharedSongsLinkAsync(long sharerId)
    {
        await WaitForLoadedAsync();
        return await SharedSongsLink(sharerId).CountAsync() > 0;
    }

    public async Task<SongsPage> GoToSongsAsync()
    {
        // When the sharer sub-menu is present, nav-songs toggles expansion instead of navigating.
        // Use the explicit "Mine" sub-link when it exists; otherwise navigate directly.
        // Wait for the sharers to load first, so the sub-menu reflects the current shares.
        await WaitForLoadedAsync();
        if (await MineSongsLink.CountAsync() > 0)
            await ClickSongsSubNavAsync(MineSongsLink);
        else
            await SongsLink.ClickAsync();

        var page = new SongsPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<SongsPage> GoToSharedSongsAsync(long sharerId)
    {
        await WaitForLoadedAsync();
        await ClickSongsSubNavAsync(SharedSongsLink(sharerId));
        var page = new SongsPage(Root.Page, shared: true);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    private async Task ClickSongsSubNavAsync(ILocator subLink)
    {
        if (await subLink.IsVisibleAsync())
        {
            await subLink.ClickAsync();
        }
        else
        {
            // Sub-menu is collapsed — expand it first, then click the sub-link
            await SongsLink.ClickAsync();
            await subLink.ClickAsync();
        }
    }

    public async Task<AlbumsPage> GoToAlbumsAsync()
    {
        await AlbumsLink.ClickAsync();
        var page = new AlbumsPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<ArtistsPage> GoToArtistsAsync()
    {
        await ArtistsLink.ClickAsync();
        var page = new ArtistsPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<PlaylistsPage> GoToPlaylistsAsync()
    {
        await PlaylistsLink.ClickAsync();
        var page = new PlaylistsPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<DevicesPage> GoToDevicesAsync()
    {
        await DevicesLink.ClickAsync();
        var page = new DevicesPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<HistoryPage> GoToHistoryAsync()
    {
        await HistoryLink.ClickAsync();
        var page = new HistoryPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<AuditsPage> GoToAuditsAsync()
    {
        await AuditsLink.ClickAsync();
        var page = new AuditsPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<PurchasesPage> GoToPurchasesAsync()
    {
        await PurchasesLink.ClickAsync();
        var page = new PurchasesPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
    }

    public async Task<SettingsPage> GoToSettingsAsync()
    {
        await SettingsLink.ClickAsync();
        return new SettingsPage(Root.Page);
    }

    public async Task<HomePage> GoToHomeAsync()
    {
        await HomeLink.ClickAsync();
        return new HomePage(Root.Page);
    }

    public async Task<PlayerPage> GoToPlayerAsync()
    {
        await PlayerLink.ClickAsync();
        return new PlayerPage(Root.Page);
    }
}
