using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class QueuesFixture
{
    /// <summary>
    /// Creates a queue named <paramref name="queue"/> with the given songs, its first song current.
    /// The created queue becomes the user's playing queue, so the last one seeded is the one playing.
    /// </summary>
    public async Task<PlaylistData> SeedAsync(IAPIRequestContext api, long userId, string queue,
        params SongData[] songs)
    {
        var response = await api.PostWithTraceAsync("/api/playlists/queues", new()
        {
            DataObject = new
            {
                name = queue,
                songIds = songs.Select(s => s.Id).ToArray(),
            },
        });

        response.Ok.ShouldBeTrue($"Failed to create queue: {response.Status} {response.StatusText}");

        var json = await response.JsonAsync();
        var id = json?.GetProperty("queue").GetProperty("id").GetInt64()
            ?? throw new InvalidOperationException("Failed to get queue ID from response");

        return new PlaylistData(id, queue);
    }
}
