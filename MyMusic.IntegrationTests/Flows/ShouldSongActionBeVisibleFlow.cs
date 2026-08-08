using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Which song action whose visibility should be asserted by <see cref="ShouldSongActionBeVisibleFlow"/>.
/// </summary>
public enum SongAction
{
    /// <summary>
    /// The "Manage Sharing" page-level button on the song detail page.
    /// </summary>
    ManageSharing,

    /// <summary>
    /// The "Share" floating menu item on a songs collection view, gated behind row selection.
    /// </summary>
    Share,
}

/// <summary>
/// Navigates to the relevant view for a song, locates a song action, and asserts whether it is visible.
/// Abstracts the "navigate + locate an action + assert visibility" pattern for song actions:
/// <list type="bullet">
/// <item><see cref="SongAction.ManageSharing"/>: page-level button on the song detail page.</item>
/// <item><see cref="SongAction.Share"/>: floating menu item on a songs collection view, gated behind row selection.</item>
/// </list>
/// </summary>
public class ShouldSongActionBeVisibleFlow : IFlow
{
    private readonly SongAction _action;
    private readonly long? _songId;
    private readonly long? _sharerId;
    private readonly string? _songTitle;
    private readonly bool _shouldExist;

    /// <summary>
    /// Asserts visibility of the <see cref="SongAction.ManageSharing"/> button on the song detail page.
    /// </summary>
    public ShouldSongActionBeVisibleFlow(SongAction action, long songId, bool shouldExist)
    {
        if (action != SongAction.ManageSharing)
            throw new ArgumentException($"Constructor expects {SongAction.ManageSharing}, got {action}.", nameof(action));
        _action = action;
        _songId = songId;
        _shouldExist = shouldExist;
    }

    /// <summary>
    /// Asserts visibility of the <see cref="SongAction.Share"/> floating menu item on a songs collection view.
    /// When <paramref name="sharerId"/> is null, navigates to the user's own Songs list; otherwise to the shared view.
    /// </summary>
    public ShouldSongActionBeVisibleFlow(SongAction action, long? sharerId, string songTitle, bool shouldExist)
    {
        if (action != SongAction.Share)
            throw new ArgumentException($"Constructor expects {SongAction.Share}, got {action}.", nameof(action));
        _action = action;
        _sharerId = sharerId;
        _songTitle = songTitle;
        _shouldExist = shouldExist;
    }

    public async Task ExecuteAsync(IPage page)
    {
        switch (_action)
        {
            case SongAction.ManageSharing:
                await AssertManageSharingButtonAsync(page);
                break;
            case SongAction.Share:
                await AssertShareActionAsync(page);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_action), _action, "Unknown song action");
        }
    }

    private async Task AssertManageSharingButtonAsync(IPage page)
    {
        var songId = _songId!.Value;
        await page.GotoAsync($"{IntegrationTestBase.BaseUrl}/songs/{songId}");
        var songDetails = new SongDetailsPage(page);
        await songDetails.WaitForLoadedAsync();

        var button = page.GetByRole(AriaRole.Button, new() { Name = "Manage Sharing" });
        var isVisible = await button.CountAsync() > 0;
        isVisible.ShouldBe(_shouldExist,
            $"Manage Sharing button should {(_shouldExist ? "" : "not ")}be visible on song {songId}");
    }

    private async Task AssertShareActionAsync(IPage page)
    {
        var songTitle = _songTitle!;
        var home = new HomePage(page);
        var songsPage = _sharerId is null
            ? await home.Navbar.GoToSongsAsync()
            : await home.Navbar.GoToSharedSongsAsync(_sharerId.Value);

        var rowIndex = await songsPage.Collection.FindRowByTitleAsync(songTitle);
        rowIndex.ShouldBeGreaterThanOrEqualTo(0, $"Song '{songTitle}' not found in the {( _sharerId is null ? "songs" : "shared")} view");
        await songsPage.Collection.SelectRowByIndexAsync(rowIndex);

        var menu = await songsPage.Collection.OpenFloatingActionsMenuAsync();
        var hasShareItem = await menu.HasItemAsync(SongsActionsMenuComponent.ShareSongs);
        hasShareItem.ShouldBe(_shouldExist,
            $"Share action should {(_shouldExist ? "" : "not ")}be present in the {(_sharerId is null ? "songs" : "shared")} view");

        await menu.CloseAsync();
    }
}