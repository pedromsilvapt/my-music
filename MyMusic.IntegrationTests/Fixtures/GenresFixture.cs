using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class GenresFixture
{
    public static string[] DefaultGenres { get; } =
    [
        "Alternative metal",
        "Dance",
        "Dubstep",
        "Hard rock",
        "Metal",
        "Other",
        "Pop",
        "Power metal",
        "Punk rock",
        "Rock",
        "Symphonic metal",
        "World",
    ];

    public async Task<List<GenreData>> SeedAsync(IAPIRequestContext api, long userId, string[]? genres = null)
    {
        var sampleGenres = genres ?? DefaultGenres;
        var data = new List<GenreData>();

        foreach (var genreName in sampleGenres)
        {
            var response = await api.PostWithTraceAsync("/api/genres", new()
            {
                DataObject = new
                {
                    name = genreName,
                },
            });

            response.Ok.ShouldBeTrue($"Failed to create genre: {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var id = json?.GetProperty("genre").GetProperty("id").GetInt64()
                ?? throw new InvalidOperationException("Failed to get genre ID from response");
            var name = json?.GetProperty("genre").GetProperty("name").GetString()
                ?? genreName;

            data.Add(new GenreData(id, name));
        }

        return data;
    }

    public async Task<GenreData> SeedAsync(IAPIRequestContext api, long userId, string genre)
    {
        var genres = await SeedAsync(api, userId, [genre]);
        return genres[0];
    }
}
