using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

public class SongsPageTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();

    // Last of the default songs, far enough down the list to be off screen initially
    private const string FarDownSongTitle = "Two Faced";

    [Fact]
    public async Task SongsPage_ShouldDisplayCollection()
    {
        // Seed songs data for the current user
        var songs = new SongsFixture();
        await songs.SeedAsync(RequestContext, UserId);

        // Navigate to the songs page via the navbar
        var home = new HomePage(Page);
        var songsPage = await home.Navbar.GoToSongsAsync();

        // Verify the songs collection displays at least one row
        var rowCount = await songsPage.Collection.GetRowCountAsync();
        rowCount.ShouldBeGreaterThan(0, "Songs collection should have at least one row");
    }

    [Fact]
    public async Task GoTo_ShouldScrollToSelectedSong()
    {
        // Seed enough songs for the collection to overflow the viewport
        await _songs.SeedAsync(RequestContext, UserId);

        // Jump to a song far down the list through the "Go to item" modal
        var collection = await new GoToSongFlow(FarDownSongTitle).ExecuteAsync(Page);

        // The collection should have scrolled the selected song into view
        await Assertions.Expect(collection.GetRowByTitle(FarDownSongTitle)).ToBeInViewportAsync();
    }

    [Fact]
    public async Task GoTo_ShouldHighlightTargetThenClear()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Jump to a song far down the list through the "Go to item" modal
        var collection = await new GoToSongFlow(FarDownSongTitle).ExecuteAsync(Page);

        // Once scrolled into view, the song should flash, and the flash should end shortly after
        var row = collection.GetRowByTitle(FarDownSongTitle);
        await Assertions.Expect(row).ToBeInViewportAsync();
        await Assertions.Expect(row).ToHaveAttributeAsync("data-highlighted", "true");
        await Assertions.Expect(row).Not.ToHaveAttributeAsync("data-highlighted", "true");
    }

    [Fact]
    public async Task GoTo_ChangingSortShouldNotScrollBack()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Jump to a song far down the list and wait for its flash to end
        var collection = await new GoToSongFlow(FarDownSongTitle).ExecuteAsync(Page);
        await collection.WaitForScrollRequestFinishedAsync();

        // Sorting by title descending moves the song near the top of the list, out of view
        await collection.SortByAsync("Title", descending: true);

        // The finished jump should not follow the song: the list shouldn't scroll to it or flash it again
        await collection.ShouldNotScrollToAsync(collection.GetRowByTitle(FarDownSongTitle));
        await Assertions.Expect(collection.HighlightedRows).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GoTo_ShouldShowEmptyStateAndResetOnClose()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Open the "Go to item" modal on the songs page
        var home = new HomePage(Page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var goTo = await songsPage.Collection.OpenGoToAsync();

        // Searching for something that doesn't exist should show the empty state
        await goTo.SearchAsync("no song has this title");
        await Assertions.Expect(goTo.EmptyMessage).ToBeVisibleAsync();

        // Closing and reopening should start again with a focused, empty search and the options hidden
        await goTo.CloseAsync();
        goTo = await songsPage.Collection.OpenGoToAsync();
        await Assertions.Expect(goTo.SearchInput).ToBeFocusedAsync();
        await Assertions.Expect(goTo.SearchInput).ToHaveValueAsync("");
        await Assertions.Expect(goTo.Options).ToHaveCountAsync(0);

        // Clicking the search input should reveal the options, floating below the input
        await goTo.SearchInput.ClickAsync();
        await Assertions.Expect(goTo.Options.First).ToBeVisibleAsync();
        (await goTo.IsDropdownBelowInputAsync()).ShouldBeTrue("The options dropdown should not cover the search input");
    }

    [Fact]
    public async Task GoTo_ShouldJumpWithEnterKey()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Type the song title and press Enter without touching the arrow keys
        var collection = await new GoToSongFlow(FarDownSongTitle, useKeyboard: true).ExecuteAsync(Page);

        // The first match should have been submitted, scrolling the song into view
        await Assertions.Expect(collection.GetRowByTitle(FarDownSongTitle)).ToBeInViewportAsync();
    }

    [Fact]
    public async Task GoTo_HoverShouldSelectOption()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Search for songs by an artist with several matches
        var home = new HomePage(Page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var goTo = await songsPage.Collection.OpenGoToAsync();
        await goTo.SearchAsync("Linkin Park");

        // The first match starts selected; hovering another option should move the selection to it
        await Assertions.Expect(goTo.Options.First).ToHaveAttributeAsync("aria-selected", "true");
        await goTo.HoverAsync(FarDownSongTitle);
        await Assertions.Expect(goTo.GetOption(FarDownSongTitle)).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(goTo.Options.And(Page.Locator("[aria-selected='true']"))).ToHaveCountAsync(1);

        // Clicking the hovered option should jump to it
        await goTo.SelectAsync(FarDownSongTitle);
        await Assertions.Expect(songsPage.Collection.GetRowByTitle(FarDownSongTitle)).ToBeInViewportAsync();
    }

    [Fact]
    public async Task GoTo_OptionsShouldNotContainLinks()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Open the modal; the option content comes from the schema renderers, which contain links
        var home = new HomePage(Page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var goTo = await songsPage.Collection.OpenGoToAsync();
        await goTo.SearchAsync(FarDownSongTitle);

        // Those links should be disabled inside the options
        await Assertions.Expect(goTo.GetOption(FarDownSongTitle)).ToBeVisibleAsync();
        await Assertions.Expect(goTo.InteractiveLinks).ToHaveCountAsync(0);

        // Clicking over where the title link sits should select the song instead of navigating away
        var songsUrl = Page.Url;
        await goTo.SelectAsync(FarDownSongTitle);
        Page.Url.ShouldBe(songsUrl);
        await Assertions.Expect(songsPage.Collection.GetRowByTitle(FarDownSongTitle)).ToBeInViewportAsync();
    }

    [Fact]
    public async Task GoTo_ShouldReachEveryOptionWhileRenderingOnlyVisibleOnes()
    {
        await _songs.SeedAsync(RequestContext, UserId);
        var songCount = SongsFixture.DefaultSongs.Length;

        // Reveal the unfiltered options: only the ones in view should be rendered
        var home = new HomePage(Page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var goTo = await songsPage.Collection.OpenGoToAsync();
        await goTo.OpenOptionsAsync();
        (await goTo.Options.CountAsync()).ShouldBeLessThan(songCount, "Options should be virtualized");

        // ArrowUp from the first option should wrap around to the very last song, scrolling it into view
        await goTo.PressAsync("ArrowUp");
        await Assertions.Expect(goTo.SelectedOption).ToHaveAttributeAsync("id", new Regex($"-option-{songCount - 1}$"));
        await Assertions.Expect(goTo.SelectedOption).ToBeInViewportAsync();

        // Pressing Enter should jump to that last song in the collection
        var lastTitle = await goTo.GetSelectedTitleAsync();
        await goTo.SubmitWithEnterAsync();
        await Assertions.Expect(songsPage.Collection.GetRowByTitle(lastTitle)).ToBeInViewportAsync();
    }
}
