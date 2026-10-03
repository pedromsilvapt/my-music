using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Audits;

/// <summary>
/// Integration tests for the soundalike audit page: resolving a group of soundalike songs by keeping one of them and
/// choosing what happens to each of the others.
/// </summary>
public class SoundalikesTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;
    private SoundalikesFixture _soundalikes = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
        _soundalikes = new SoundalikesFixture();
    }

    [Fact]
    public async Task ResolveGroup_FromItsOwnButton_ShouldDeleteTheOtherSongs()
    {
        // Setup: seed two soundalikes (same audio under different tags) and have them detected as a group
        var kept = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var deleted = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);
        await _soundalikes.SeedAsync(RequestContext);

        // Action: resolve the group through its own button rather than the toolbar — the other song is deleted by
        // default, and the group should leave the page
        await new ResolveSoundalikesFlow(kept.Title, groupOnly: true).ExecuteAsync(Page);

        // Assert: only the kept song is left in the library
        await new ShouldSongExistFlow(kept.Title, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistFlow(deleted.Title, shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task ResolveGroup_WithIgnoredSong_ShouldKeepItAndNotReportItAgain()
    {
        // Setup: seed three soundalikes and have them detected as a single group
        var kept = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]);
        var deleted = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]);
        var ignored = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[3]);
        await _soundalikes.SeedAsync(RequestContext);

        // Action: resolve the group, ignoring one of the songs — it should be neither merged nor deleted, while the
        // remaining one is deleted by default
        await new ResolveSoundalikesFlow(kept.Title, new() { [ignored.Title] = SoundalikeAction.Ignore }, groupOnly: true)
            .ExecuteAsync(Page);

        // Assert: the ignored song is still in the library, next to the kept one
        await new ShouldSongExistFlow(kept.Title, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistFlow(ignored.Title, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistFlow(deleted.Title, shouldExist: false).ExecuteAsync(Page);

        // Assert: scanning again should not report the ignored song as a soundalike of the kept one anymore
        var groupsCount = await _soundalikes.ScanAsync(RequestContext);
        groupsCount.ShouldBe(0, "The ignored song should no longer be a soundalike of the kept song");
    }
}
