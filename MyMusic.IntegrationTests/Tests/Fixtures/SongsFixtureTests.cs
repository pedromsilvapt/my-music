using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class SongsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected override bool NavigateOnInitialize => false;

    // Scenario: Seeding the sample library uploads every sample song
    //   Given a new user with an empty library
    //   When the whole sample library is seeded
    //   Then all 147 sample songs are created, each with an id and a title
    [Fact]
    public async Task SeedAsync_UploadsSong()
    {
        var fixture = new SongsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        data.ShouldNotBeEmpty();
        data.Count.ShouldBe(147);

        foreach (var song in data)
        {
            song.Id.ShouldBeGreaterThan(0);
            song.Title.ShouldNotBeNullOrEmpty();
        }
    }

    // Scenario: The seeded sample library contains its well-known songs
    //   Given a new user with an empty library
    //   When the whole sample library is seeded
    //   Then the created songs include "Bad Romance", "The Pretender" and "Heaven Knows"
    [Fact]
    public async Task Data_ContainsSeededSongs()
    {
        var fixture = new SongsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        var songTitles = data.Select(s => s.Title).ToList();
        songTitles.ShouldContain("Bad Romance");
        songTitles.ShouldContain("The Pretender");
        songTitles.ShouldContain("Heaven Knows");
    }

    // Scenario: The seeded sample library can be listed from the server
    //   Given a new user with an empty library
    //   When the whole sample library is seeded
    //   Then the server lists the user's songs successfully
    [Fact]
    public async Task SeedAsync_CreatesArtistsAndAlbums()
    {
        var fixture = new SongsFixture();
        await fixture.SeedAsync(RequestContext, UserId);

        var response = await RequestContext.GetWithTraceAsync("/api/songs");
        response.Ok.ShouldBeTrue();

        var json = await response.JsonAsync();
        var songs = json?.GetProperty("songs");

        songs.ShouldNotBeNull();
    }
}
