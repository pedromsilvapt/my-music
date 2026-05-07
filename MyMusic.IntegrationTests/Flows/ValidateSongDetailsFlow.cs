using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

public class ValidateSongDetailsFlow : IFlow
{
    private readonly string? _songTitle;
    private readonly int? _rowIndex;
    private readonly ValidateSongOptions _expected;

    public ValidateSongDetailsFlow(string songTitle, ValidateSongOptions expected)
    {
        _songTitle = songTitle;
        _expected = expected;
    }

    public ValidateSongDetailsFlow(int rowIndex, ValidateSongOptions expected)
    {
        _rowIndex = rowIndex;
        _expected = expected;
    }

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = _rowIndex.HasValue
            ? await new OpenSongDetailsFlow(_rowIndex.Value).ExecuteAsync(page)
            : await new OpenSongDetailsFlow(_songTitle!).ExecuteAsync(page);

        if (_expected.Title is not null)
        {
            var title = await songDetails.GetTitleAsync();
            title.ShouldBe(_expected.Title);
        }

        if (_expected.Artists is not null)
        {
            var artists = await songDetails.GetArtistsAsync();
            artists.ShouldBeEquivalentTo(_expected.Artists);
        }

        if (_expected.Album is not null)
        {
            var album = await songDetails.GetAlbumAsync();
            album.ShouldBe(_expected.Album);
        }

        if (_expected.Year is not null)
        {
            var year = await songDetails.GetYearAsync();
            year.ShouldBe(_expected.Year);
        }

        if (_expected.Explicit is not null)
        {
            var isExplicit = await songDetails.GetExplicitAsync();
            isExplicit.ShouldBe(_expected.Explicit.Value);
        }

        if (_expected.Genres is not null)
        {
            var genres = await songDetails.GetGenresAsync();
            genres.ShouldBe(_expected.Genres);
        }
    }
}
