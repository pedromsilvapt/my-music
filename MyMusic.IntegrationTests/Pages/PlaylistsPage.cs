using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class PlaylistsPage(IPage page) : BasePage(page, "playlists")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    public ILocator PlaylistTitle(string playlistName) =>
        Root.Locator($"[data-testid='playlist-title'][data-playlist-name='{playlistName}']").First;

    public PlaylistShareIndicatorComponent ShareIndicator(string playlistName) =>
        new(PlaylistTitle(playlistName).GetByTestId("playlist-share-indicator"));

    /// <summary>
    /// Whether a playlist with the given name is listed (owned or shared with the current user).
    /// </summary>
    public async Task<bool> HasPlaylistAsync(string playlistName) =>
        await PlaylistTitle(playlistName).CountAsync() > 0;

    /// <summary>
    /// Clicks the playlist's name link and waits for its detail page to load.
    /// </summary>
    public async Task<PlaylistDetailsPage> GoToPlaylistDetailsAsync(string playlistName)
    {
        await PlaylistTitle(playlistName).GetByRole(AriaRole.Link).ClickAsync();
        var details = new PlaylistDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }
}
