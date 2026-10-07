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

    // Scenario: The songs page lists the user's songs
    //   Given the user has songs in their library
    //   When the user opens the songs page from the navigation bar
    //   Then the songs list shows at least one song
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

    // Scenario: Searching songs only looks into their lyrics while the "Lyrics" toggle is on
    //   Given a song that has some words only in its lyrics
    //   And another song that has the same words in its title
    //   When the user searches for those words with the "Lyrics" toggle off
    //   Then only the song with the words in its title is listed
    //   When the user turns the "Lyrics" toggle on
    //   Then both songs are listed
    //   When the user turns the "Lyrics" toggle back off
    //   Then only the song with the words in its title is listed again
    [Fact]
    public async Task Search_WithLyricsToggle_ShouldFindSongByLyrics()
    {
        // Seed two songs sharing the same words: one only has them in its lyrics, the other in its title
        var lyricsSong = SongsFixture.SongWithLyrics;
        var titleSong = SongsFixture.SongTitledLikeLyrics;
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.LyricsSearchSongs);

        // Search for those words with the "Lyrics" toggle off: only the title should match
        var collection = await new SearchSongsFlow("crimson harbour").ExecuteAsync(Page);
        await Assertions.Expect(collection.GetRowByTitle(titleSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(1);

        // Turning the toggle on should also find the other song through its lyrics
        await collection.SetLyricsSearchAsync(true);
        await Assertions.Expect(collection.GetRowByTitle(lyricsSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(2);

        // Turning it back off should leave only the title match again
        await collection.SetLyricsSearchAsync(false);
        await Assertions.Expect(collection.GetRowByTitle(titleSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(1);
    }

    // Scenario: Going to a song scrolls the songs list to it
    //   Given the user has more songs than fit on the screen
    //   When the user picks a song far down the list in the "Go to item" modal
    //   Then the list scrolls until that song is visible
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

    // Scenario: Going to a song briefly highlights it
    //   Given the user has more songs than fit on the screen
    //   When the user picks a song far down the list in the "Go to item" modal
    //   Then the list scrolls until that song is visible
    //   And the song is highlighted
    //   And the highlight goes away shortly after
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

    // Scenario: Changing the sort order after going to a song does not follow that song
    //   Given the user has more songs than fit on the screen
    //   And the user went to a song far down the list, and its highlight has ended
    //   When the user sorts the list by title, descending, moving the song out of view
    //   Then the list does not scroll to the song
    //   And no song is highlighted
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

    // Scenario: The "Go to item" modal reports when nothing matches, and starts clean when reopened
    //   Given the user has songs in their library
    //   When the user opens the "Go to item" modal and searches for a title no song has
    //   Then a message says that nothing was found
    //   When the user closes and reopens the modal
    //   Then the search is focused and empty, with no options showing
    //   When the user clicks the search
    //   Then the options show up below the search, without covering it
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

    // Scenario: Pressing Enter in the "Go to item" modal goes to the first match
    //   Given the user has more songs than fit on the screen
    //   When the user types the title of a song far down the list in the "Go to item" modal
    //   And presses Enter without using the arrow keys
    //   Then the list scrolls until that song is visible
    [Fact]
    public async Task GoTo_ShouldJumpWithEnterKey()
    {
        await _songs.SeedAsync(RequestContext, UserId);

        // Type the song title and press Enter without touching the arrow keys
        var collection = await new GoToSongFlow(FarDownSongTitle, useKeyboard: true).ExecuteAsync(Page);

        // The first match should have been submitted, scrolling the song into view
        await Assertions.Expect(collection.GetRowByTitle(FarDownSongTitle)).ToBeInViewportAsync();
    }

    // Scenario: Hovering an option in the "Go to item" modal selects it
    //   Given the user has several songs by the same artist
    //   When the user searches for that artist in the "Go to item" modal
    //   Then the first match is selected
    //   When the user hovers another match
    //   Then that match becomes the only selected one
    //   When the user clicks it
    //   Then the list scrolls until that song is visible
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

    // Scenario: Options in the "Go to item" modal select a song instead of acting as links
    //   Given the user has songs, whose details are shown as links in the songs list
    //   When the user searches for a song in the "Go to item" modal
    //   Then the song's option shows up without any link that can be followed
    //   When the user clicks the option
    //   Then the user stays on the songs page
    //   And the list scrolls until that song is visible
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

    // Scenario: Every song can be reached in the "Go to item" modal, which only renders the options in view
    //   Given the user has more songs than fit in the modal's options
    //   When the user opens the options without searching
    //   Then only part of the songs are rendered as options
    //   When the user presses the up arrow on the first option
    //   Then the selection wraps around to the very last song, which scrolls into view
    //   When the user presses Enter
    //   Then the songs list scrolls until that last song is visible
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
