using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class SongsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
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
