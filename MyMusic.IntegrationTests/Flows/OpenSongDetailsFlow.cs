using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

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

        int rowIndex;
        if (_rowIndex.HasValue)
        {
            rowIndex = _rowIndex.Value;
        }
        else
        {
            rowIndex = await songsPage.Collection.FindRowByTitleAsync(_songTitle!);
            rowIndex.ShouldBeGreaterThanOrEqualTo(0, $"Song '{_songTitle}' not found in collection");
        }

        return await songsPage.Collection.GoToSongDetailsAsync(rowIndex);
    }
}
