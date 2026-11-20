using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

public class SongsRatingTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();

    // Scenario: The songs list shows each song's rating and can be sorted by it
    //   Given songs rated with five, four and two and a half stars, and an unrated song
    //   When the user opens the songs page
    //   Then each song shows its rating, and the unrated song shows none
    //   When the user sorts the songs by rating, descending
    //   Then the best rated songs come first, and the unrated song comes last
    [Fact]
    public async Task SongsList_ShouldShowAndSortByRating()
    {
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.RatedSongs);

        // Open the songs page: every song should show its rating next to a star
        var songsPage = await new HomePage(Page).Navbar.GoToSongsAsync();
        var collection = songsPage.Collection;
        await collection.WaitForLoadedAsync();
        await Assertions.Expect(collection.GetRatingByTitle(SongsFixture.FiveStarsSong.Title!)).ToHaveTextAsync("5 ★");
        await Assertions.Expect(collection.GetRatingByTitle(SongsFixture.FourStarsSong.Title!)).ToHaveTextAsync("4 ★");
        await Assertions.Expect(collection.GetRatingByTitle(SongsFixture.TwoAndAHalfStarsSong.Title!)).ToHaveTextAsync("2.5 ★");
        await Assertions.Expect(collection.GetRatingByTitle(SongsFixture.UnratedSong.Title!)).Not.ToContainTextAsync("★");

        // Sorting by rating, descending, should list the songs from the best rated to the unrated one
        await collection.SortByAsync("Rating", descending: true);
        (await collection.GetTitlesAsync()).ShouldBe([
            SongsFixture.FiveStarsSong.Title!,
            SongsFixture.FourStarsSong.Title!,
            SongsFixture.TwoAndAHalfStarsSong.Title!,
            SongsFixture.UnratedSong.Title!,
        ]);
    }

    // Scenario: Songs can be filtered by their rating
    //   Given songs rated with five, four and two and a half stars, and an unrated song
    //   When the user filters the songs to those rated four stars or more
    //   Then only the five and four stars songs are listed
    [Fact]
    public async Task SongsList_ShouldFilterByRating()
    {
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.RatedSongs);

        // Filter the songs to those rated four stars or more
        var collection = await new FilterSongsFlow("rating >= 4").ExecuteAsync(Page);

        // Only the five and four stars songs should be left
        await Assertions.Expect(collection.Rows).ToHaveCountAsync(2);
        await Assertions.Expect(collection.GetRowByTitle(SongsFixture.FiveStarsSong.Title!)).ToBeVisibleAsync();
        await Assertions.Expect(collection.GetRowByTitle(SongsFixture.FourStarsSong.Title!)).ToBeVisibleAsync();
    }

    // Scenario: A song's rating can be changed and cleared straight from the songs list
    //   Given an unrated song
    //   When the user picks three and a half stars in the song's row
    //   Then the row shows the new rating
    //   And the song's edit dialog shows the same rating
    //   When the user goes back to the songs list and clears the rating
    //   Then the row shows no rating
    [Fact]
    public async Task SongsList_ShouldChangeRatingInPlace()
    {
        var title = SongsFixture.UnratedSong.Title!;
        await _songs.SeedAsync(RequestContext, UserId, [SongsFixture.UnratedSong]);

        // Rate the song with three and a half stars from its row
        var songsPage = await new HomePage(Page).Navbar.GoToSongsAsync();
        await songsPage.Collection.WaitForLoadedAsync();
        await songsPage.Collection.SetRatingAsync(title, 3.5m);
        await Assertions.Expect(songsPage.Collection.GetRatingByTitle(title)).ToHaveTextAsync("3.5 ★");

        // The rating should have been saved: the edit dialog shows it too
        var songDetails = await new OpenSongDetailsFlow(title).ExecuteAsync(Page);
        var editModal = await songDetails.OpenEditModalAsync();
        await editModal.Rating.ShouldHaveValueAsync(3.5m);
        await editModal.CancelAsync();

        // Clearing the rating from the row should leave the song unrated again
        songsPage = await new HomePage(Page).Navbar.GoToSongsAsync();
        await songsPage.Collection.WaitForLoadedAsync();
        await songsPage.Collection.SetRatingAsync(title, null);
        await Assertions.Expect(songsPage.Collection.GetRatingByTitle(title)).Not.ToContainTextAsync("★");
    }

    // Scenario: The playing song can be rated from the footer player
    //   Given a song rated with four stars
    //   When the user plays the song
    //   Then the footer player shows its four stars
    //   When the user picks four and a half stars in the footer player
    //   Then the footer player shows the new rating
    //   And the song's row in the songs list shows it too
    [Fact]
    public async Task FooterPlayer_ShouldChangeRatingOfCurrentSong()
    {
        var title = SongsFixture.FourStarsSong.Title!;
        await _songs.SeedAsync(RequestContext, UserId, [SongsFixture.FourStarsSong]);

        // Play the song: the footer player should show its current rating
        var footerPlayer = await new PlaySongFlow(title).ExecuteAsync(Page);
        await footerPlayer.Rating.ShouldHaveValueAsync(4m);

        // Rate it with four and a half stars from the footer player
        await footerPlayer.Rating.SetAsync(4.5m);
        await footerPlayer.Rating.ShouldHaveValueAsync(4.5m);

        // The songs list, still open behind the player, should show the new rating as well
        var collection = new SongsPage(Page).Collection;
        await Assertions.Expect(collection.GetRatingByTitle(title)).ToHaveTextAsync("4.5 ★");
    }
}
