using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

public class ValidateSongInPlaylistFlow : IFlow
{
    private readonly string _songTitle;
    private readonly string _playlistName;
    private readonly bool _shouldExist;

    public ValidateSongInPlaylistFlow(string songTitle, string playlistName, bool shouldExist = true)
    {
        _songTitle = songTitle;
        _playlistName = playlistName;
        _shouldExist = shouldExist;
    }

    public async Task ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var playlistsPage = await home.Navbar.GoToPlaylistsAsync();

        await playlistsPage.Collection.GoToDetailsByCellTextAsync("name", _playlistName);

        var songsInPlaylist = await GetSongsInPlaylistAsync(page);

        if (_shouldExist)
        {
            songsInPlaylist.ShouldContain(_songTitle,
                $"Song '{_songTitle}' should be in playlist '{_playlistName}' but was not found. " +
                $"Songs in playlist: {string.Join(", ", songsInPlaylist)}");
        }
        else
        {
            songsInPlaylist.ShouldNotContain(_songTitle,
                $"Song '{_songTitle}' should NOT be in playlist '{_playlistName}' but was found.");
        }
    }

    private async Task<List<string>> GetSongsInPlaylistAsync(IPage page)
    {
        // Wait for songs to load by checking for title cells with data-testid
        var titleCells = page.Locator("td[data-testid^='collection-cell-title-']");
        
        // Wait for at least one cell to be visible
        try
        {
            await titleCells.First.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        }
        catch
        {
            // If no songs found, return empty list
            return new List<string>();
        }
        
        var count = await titleCells.CountAsync();
        var songs = new List<string>();

        for (int i = 0; i < count; i++)
        {
            try
            {
                var cell = titleCells.Nth(i);
                var title = await cell.InnerTextAsync();
                songs.Add(title);
            }
            catch
            {
                // Skip if we can't read this cell
            }
        }

        return songs;
    }
}
