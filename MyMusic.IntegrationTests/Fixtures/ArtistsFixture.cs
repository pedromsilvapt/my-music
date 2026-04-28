using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class ArtistsFixture
{
    public static string[] DefaultArtists { get; } =
    [
        "3 Doors Down",
            "A Touch of Class",
            "Against The Current",
            "Alba August",
            "Alice Merton",
            "Alina Pash",
            "Amanda Shires",
            "Amaranthe",
            "Amy Macdonald",
            "Ana Moura",
            "Apashe",
            "April Ivy",
            "Armin van Buuren",
            "Astrid S",
            "Auri",
            "Bad Company",
            "Bad Religion",
            "Bebe Rexha",
            "Ben Howard",
            "Beyond The Black",
            "Blondie",
            "Blur",
            "Brandi Carlile",
            "Bárbara Bandeira",
            "Camille Lellouche",
            "Caroline Jones",
            "Clara Luciani",
            "Clara Mae",
            "Clinton Shorter",
            "Culture Wars",
            "DAMONA",
            "Dagny",
            "Dead Sara",
            "Deolinda",
            "Dove Cameron",
            "Dylan",
            "EMELINE",
            "EchoesOfVelandria",
            "Ellysse Mason",
            "Elton John",
            "Em Beihold",
            "Faithless",
            "First Aid Kit",
            "Florence + The Machine",
            "Foo Fighters",
            "Freya Ridings",
            "Goo Goo Dolls",
            "Griff",
            "Halestorm",
            "Hana Lili",
            "Hania Rani",
            "Hanne Mjøen",
            "Hayley Williams",
            "Holly Humberstone",
            "Icon For Hire",
            "James Morrison",
            "Jess Williamson",
            "Johanna Kurkela",
            "Kacey Musgraves",
            "Kat Dahlia",
            "Krewella",
            "Lady Gaga",
            "Larkin Poe",
            "Laura Cox",
            "League of Legends",
            "Leona Lewis",
            "Linkin Park",
            "Lord Huron",
            "Lucie Silvas",
            "Marisa Liz",
            "Maya Hawke",
            "Mogli",
            "Nelly Furtado",
            "Nightwish",
            "Of Monsters and Men",
            "Paramore",
            "Paris Paloma",
            "Pedro Abrunhosa",
            "Pennywise",
            "Queen",
            "Raquel Tavares",
            "Rozzi",
            "Sam Fender",
            "Sara Correia",
            "Sarah Kinsley",
            "Sharon den Adel",
            "Sigrid",
            "Son Lux",
            "Taylor Swift",
            "The Accidentals",
            "The Henningsens",
            "The Livelines",
            "The Pretty Reckless",
            "The Rasmus",
            "The Warning",
            "Thirty Seconds To Mars",
            "U2",
            "Whitesnake",
            "Within Temptation",
            "Wolf Alice",
            "Zara Larsson",
            "Zedd",
        ];

    public async Task<List<ArtistData>> SeedAsync(IAPIRequestContext api, long userId, string[]? artists = null)
    {
        var sampleArtists = artists ?? DefaultArtists;
        var data = new List<ArtistData>();

        foreach (var artistName in sampleArtists)
        {
            var response = await api.PostAsync("/api/artists", new()
            {
                DataObject = new
                {
                    name = artistName,
                },
            });

            response.Ok.ShouldBeTrue($"Failed to create artist: {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var id = json?.GetProperty("artist").GetProperty("id").GetInt64()
                ?? throw new InvalidOperationException("Failed to get artist ID from response");
            var name = json?.GetProperty("artist").GetProperty("name").GetString()
                ?? artistName;

            data.Add(new ArtistData(id, name));
        }

        return data;
    }
}
