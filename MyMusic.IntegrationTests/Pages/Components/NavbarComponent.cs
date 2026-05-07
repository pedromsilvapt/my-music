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

    public async Task<SongsPage> GoToSongsAsync()
    {
        await SongsLink.ClickAsync();
        var page = new SongsPage(Root.Page);
        await page.Collection.WaitForLoadedAsync();
        return page;
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
