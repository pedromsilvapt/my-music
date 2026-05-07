using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class PlaylistsFixture
{
    public static string[] DefaultPlaylists { get; } =
    [
        "Test Playlist 1",
        "Test Playlist 2",
        "Test Playlist 3",
    ];

    public async Task<List<PlaylistData>> SeedAsync(IAPIRequestContext api, long userId, string[]? playlists = null)
    {
        var samplePlaylists = playlists ?? DefaultPlaylists;
        var data = new List<PlaylistData>();

        foreach (var playlistName in samplePlaylists)
        {
            var response = await api.PostWithTraceAsync("/api/playlists", new()
            {
                DataObject = new
                {
                    name = playlistName,
                },
            });

            response.Ok.ShouldBeTrue($"Failed to create playlist: {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var id = json?.GetProperty("playlist").GetProperty("id").GetInt64()
                ?? throw new InvalidOperationException("Failed to get playlist ID from response");
            var name = json?.GetProperty("playlist").GetProperty("name").GetString()
                ?? playlistName;

            data.Add(new PlaylistData(id, name));
        }

        return data;
    }

    public async Task<PlaylistData> SeedAsync(IAPIRequestContext api, long userId, string playlist)
    {
        var playlists = await SeedAsync(api, userId, [playlist]);
        return playlists[0];
    }
}
