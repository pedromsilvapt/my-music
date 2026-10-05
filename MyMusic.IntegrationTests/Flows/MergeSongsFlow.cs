using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// The merge songs dialog as it was right before the merge was confirmed.
/// </summary>
/// <param name="MatchScore">The match score shown for the songs, as a percentage, or null when unavailable.</param>
/// <param name="DefaultAction">The action the dialog chose for the songs that are not kept.</param>
public record MergeSongsResult(int? MatchScore, SoundalikeAction? DefaultAction);

/// <summary>
/// Selects <paramref name="keptSongTitle"/> and <paramref name="otherSongTitles"/> on the songs page and merges them
/// through the "Merge" bulk action, keeping the former. The other songs are left with the dialog's default action.
/// </summary>
public class MergeSongsFlow(string keptSongTitle, params string[] otherSongTitles) : IFlow<MergeSongsResult>
{
    public async Task<MergeSongsResult> ExecuteAsync(IPage page)
    {
        var menu = await new PerformSongsActionFlow([keptSongTitle, .. otherSongTitles]).ExecuteAsync(page);
        var modal = await menu.MergeAsync();

        await modal.Group.SelectPrimaryAsync(keptSongTitle);
        var result = new MergeSongsResult(
            await modal.Group.GetMatchScoreAsync(),
            await modal.Group.GetActionAsync(otherSongTitles[0]));

        await modal.ConfirmAsync();

        // The dialog closes once the merge succeeds, but the list refetch may still be in flight.
        // Wait for the merged rows to actually disappear.
        var songsPage = new SongsPage(page);
        foreach (var title in otherSongTitles)
            await Assertions.Expect(songsPage.Collection.GetRowByTitle(title)).ToHaveCountAsync(0);
        await songsPage.Collection.WaitForLoadedAsync();

        return result;
    }
}
