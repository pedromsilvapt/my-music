using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a song's detail page from the shared songs view for a given sharer.
/// </summary>
public class OpenSharedSongDetailsFlow(long sharerId, string songTitle) : IFlow<SongDetailsPage>
{
    public async Task<SongDetailsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSharedSongsAsync(sharerId);
        return await songsPage.Collection.GoToSongDetailsByTitleAsync(songTitle);
    }
}