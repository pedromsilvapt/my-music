using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
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
    }
}
