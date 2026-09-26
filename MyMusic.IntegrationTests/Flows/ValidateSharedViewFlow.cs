using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Navigates once to the shared songs view for the given sharer and asserts which songs are shown and which are hidden.
/// </summary>
public class ValidateSharedViewFlow(long sharerId, string[] shown, string[]? hidden = null) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var songsPage = await new HomePage(page).Navbar.GoToSharedSongsAsync(sharerId);

        foreach (var title in shown)
        {
            (await songsPage.Collection.FindRowByTitleAsync(title))
                .ShouldBeGreaterThanOrEqualTo(0, $"Song '{title}' should exist in the shared view");
        }

        foreach (var title in hidden ?? [])
        {
            (await songsPage.Collection.FindRowByTitleAsync(title))
                .ShouldBe(-1, $"Song '{title}' should not exist in the shared view");
        }
    }
}
