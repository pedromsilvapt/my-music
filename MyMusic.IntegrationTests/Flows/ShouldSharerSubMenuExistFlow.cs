using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Asserts whether the Songs nav item shows a sub-menu for the given sharer, or, when
/// <paramref name="sharerId"/> is <see langword="null"/>, for any sharer at all.
/// </summary>
public class ShouldSharerSubMenuExistFlow(bool shouldExist, long? sharerId = null) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var navbar = new HomePage(page).Navbar;

        var exists = sharerId is { } id
            ? await navbar.HasSharedSongsLinkAsync(id)
            : await navbar.HasSongsSubMenusAsync();

        var target = sharerId is { } s ? $"Sharer {s} sub-menu" : "Songs sub-menus";
        exists.ShouldBe(shouldExist, $"{target} should {(shouldExist ? "" : "not ")}appear");
    }
}
