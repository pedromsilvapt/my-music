using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// Wraps the player shown in the app footer while a song is loaded: the current song info,
/// its actions menu and the playback controls.
/// </summary>
public class FooterPlayerComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// Waits until the footer player shows the song with the given title.
    /// </summary>
    public async Task WaitForSongAsync(string title)
    {
        await Assertions.Expect(Root).ToContainTextAsync(title, new() { Timeout = 10000 });
    }

    /// <summary>
    /// Pauses the current song if it is playing. The headless browser may not start playback at all,
    /// in which case the song is already paused.
    /// </summary>
    public async Task EnsurePausedAsync()
    {
        var pauseButton = Root.GetByRole(AriaRole.Button, new() { Name = "Pause Current Track" });
        var playButton = Root.GetByRole(AriaRole.Button, new() { Name = "Play Current Track" });

        await pauseButton.Or(playButton).WaitForAsync();
        if (await pauseButton.IsVisibleAsync())
        {
            await pauseButton.ClickAsync();
        }

        await playButton.WaitForAsync();
    }

    /// <summary>
    /// Opens the current song's actions menu.
    /// </summary>
    public async Task<SongsActionsMenuComponent> OpenActionsMenuAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Actions" }).ClickAsync();

        // The Mantine Menu dropdown is rendered in a portal
        var menuDropdown = Root.Page.Locator(".mantine-Menu-dropdown").Last;
        await menuDropdown.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return new SongsActionsMenuComponent(menuDropdown);
    }

    /// <summary>
    /// Toggles the "Stop After This Song" flag of the current song through its actions menu.
    /// </summary>
    public async Task ToggleStopAfterThisSongAsync()
    {
        var menu = await OpenActionsMenuAsync();
        await menu.ClickItemAsync(SongsActionsMenuComponent.StopAfterThisSong);
    }

    /// <summary>
    /// Opens the current song's details page through its actions menu.
    /// </summary>
    public async Task<SongDetailsPage> GoToDetailsAsync()
    {
        var menu = await OpenActionsMenuAsync();
        await menu.ClickItemAsync(SongsActionsMenuComponent.GoToDetails);

        var songDetails = new SongDetailsPage(Root.Page);
        await songDetails.WaitForLoadedAsync();
        return songDetails;
    }

    /// <summary>
    /// Clicks the current song info to open the now playing page.
    /// </summary>
    public async Task<PlayerPage> OpenPlayerPageAsync()
    {
        await Root.Locator("a[href='/player']").ClickAsync();

        var playerPage = new PlayerPage(Root.Page);
        await playerPage.Collection.WaitForLoadedAsync();
        return playerPage;
    }
}
