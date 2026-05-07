using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

public class OpenSongDetailsFlow : IFlow<SongDetailsPage>
{
    private readonly string? _songTitle;
    private readonly int? _rowIndex;

    public OpenSongDetailsFlow(string songTitle)
    {
        _songTitle = songTitle;
    }

    public OpenSongDetailsFlow(int rowIndex)
    {
        _rowIndex = rowIndex;
    }

    public async Task<SongDetailsPage> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();

        if (_rowIndex.HasValue)
        {
            return await songsPage.Collection.GoToSongDetailsAsync(_rowIndex.Value);
        }
        else
        {
            return await songsPage.Collection.GoToSongDetailsByTitleAsync(_songTitle!);
        }
    }
}
