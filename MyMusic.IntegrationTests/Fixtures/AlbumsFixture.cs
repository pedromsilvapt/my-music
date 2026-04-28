using System.Text.Json;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class AlbumsFixture
{
    public static SampleAlbum[] DefaultAlbums { get; } =
    [
        new("A Beautiful Lie", 2005),
        new("A Thousand Suns", 2010),
        new("A Way It Goes", 2025),
        new("Age Of Unreason", 2019),
        new("All Or Nothing (Deluxe Edition)", 2012),
        new("All is Love and Pain in the Mouse Parade", 2025),
        new("Arm's Length", 2025),
        new("Asking For A Friend", 2025),
        new("Bad", 2025),
        new("Be Ok", 2016),
        new("Better Than Me", 2025),
        new("Beyond The Black", 2023),
        new("Bittersweet", 2025),
        new("Bleeding Love", 2007),
        new("Bloom", 2025),
        new("Blur", 1997),
        new("Break The Silence", 2025),
        new("Carolina (From The Motion Picture \"Where The Crawdads Sing\")", 2022),
        new("Century Child", 2002),
        new("Days Of Ash EP", 2026),
        new("Dead Letters", 2003),
        new("Dead Sara", 2012),
        new("Divorce In A Small Town", 2025),
        new("Don't Waste Your Love On Me", 2025),
        new("Dream Team", 2025),
        new("E.G.O.", 2018),
        new("ERROR", 2022),
        new("Echoes, Silence, Patience & Grace", 2007),
        new("Ego Death At A Bachelorette Party", 2025),
        new("Everybody Scream", 2025),
        new("Fleeting", 2025),
        new("Folklore", 2003),
        new("For I Am Death", 2025),
        new("Forward", 2025),
        new("Fruit Bat", 2025),
        new("Girl Of Your Dreams", 2022),
        new("Girl across the street", 2023),
        new("God give me a car", 2023),
        new("Going to Hell", 2013),
        new("Good Girl", 2026),
        new("Greatest Hits", 2002),
        new("Guerra Nuclear", 2022),
        new("HELIX", 2018),
        new("Heart of the Hurricane (Black Edition)", 2018),
        new("Hell with You", 2021),
        new("Hot Goblin", 2025),
        new("Hot Space (Deluxe Edition 2011 Remaster)", 1982),
        new("Hymn For Tomorrow", 2021),
        new("Hymns Of Resilience", 2026),
        new("Hørizøns", 2020),
        new("I Feel Embarrassed", 2025),
        new("I Still Hide", 2022),
        new("II - Those We Don't Speak Of", 2021),
        new("Ignorance", 2009),
        new("Ignorance is Bliss", 2025),
        new("In And Out of Love", 2008),
        new("Invisible (from the Netflix Film Klaus)", 2019),
        new("It Hurts", 2024),
        new("Je remercie mon ex", 2020),
        new("Lanterns", 2013),
        new("Legends Never Die", 2017),
        new("Little Bit", 2024),
        new("Little Bit Closer", 2025),
        new("Living Things", 2012),
        new("Loose", 2006),
        new("Lost In Forever (Tour Edition)", 2016),
        new("MAXIMALISM (Deluxe Edition)", 2016),
        new("MOSS", 2022),
        new("Marcha", 2026),
        new("Meu Amor de Longe", 2016),
        new("Mi Plan", 2009),
        new("Mon sang", 2024),
        new("Moura", 2015),
        new("Moura (Deluxe Version)", 2016),
        new("Mundo Pequenino", 2013),
        new("My Garden", 2015),
        new("New Religion", 2026),
        new("No Need to Try Harder", 2025),
        new("Nobody Wants This Season 2: The Soundtrack", 2025),
        new("Not To Anybody", 2025),
        new("Nothing Lasts Forever", 2025),
        new("On Giacometti", 2023),
        new("Piece Of Mind", 2025),
        new("Planet Pop", 2000),
        new("Play Hard EP", 2012),
        new("Pretty", 2021),
        new("Que O Amor Te Salve Nesta Noite Escura (Ao Vivo)", 2022),
        new("Quero É Viver", 2022),
        new("Rebel Child", 2023),
        new("Returning To Myself", 2025),
        new("Riot!", 2009),
        new("Sand", 2023),
        new("Scripted", 2011),
        new("Shooting Star", 2025),
        new("Someday We Won't Live Here Anymore", 2025),
        new("Spirit", null),
        new("Stay Gold", 2014),
        new("Sympathy Magic", 2025),
        new("Take It Like A Man", 2022),
        new("Talk to You", 2025),
        new("The Alibi", 2024),
        new("The Burgh Island EP", 2012),
        new("The Clearing", 2025),
        new("The Cosmic Selector Vol. 1", 2025),
        new("The Expanse - The Collector's Edition", 2019),
        new("The Fame Monster", 2010),
        new("The Henningsens EP", 2013),
        new("The Life of a Showgirl", 2025),
        new("The Nexus", 2012),
        new("The Silent Force", 2005),
        new("The Weaver's Song", 2025),
        new("There's Always More That I Could Say", 2025),
        new("Time Ain't Accidental", 2023),
        new("Time Of My Life", 2011),
        new("To Love Somebody", 2026),
        new("Tomorrows III", 2021),
        new("True Colors", 2015),
        new("Two Faced", 2024),
        new("Undiscovered (International Version)", 2006),
        new("Undress Me", 2025),
        new("Vertigo (Tour Edition)", 2024),
        new("Visions Of A Life", 2017),
        new("Wanderer (Expedition Happiness Soundtrack)", 2017),
        new("Wearing Nothing", 2017),
        new("When You're Young", 2023),
        new("Whitesnake (30th Anniversary Edition)", 1987),
        new("Wicker Woman", 2025),
        new("Wild Long Lie", 2024),
        new("Woman", 2024),
        new("XXI Century Blood", 2017),
        new("this is how i learn to say no", 2021),
        new("what it means to be a girl / this is how i learn to say no", 2021),
    ];

    public async Task<List<AlbumData>> SeedAsync(IAPIRequestContext api, long userId, List<ArtistData> artists, SampleAlbum[]? albums = null)
    {
        if (artists.Count == 0)
        {
            throw new InvalidOperationException("Artists fixture must be seeded first");
        }

        var sampleAlbums = albums ?? DefaultAlbums;
        var data = new List<AlbumData>();

        foreach (var (album, index) in sampleAlbums.Select((a, i) => (a, i)))
        {
            var artistId = artists[index % artists.Count].Id;

            var response = await api.PostAsync("/api/albums", new()
            {
                DataObject = new
                {
                    name = album.Name,
                    artistId = artistId,
                    year = album.Year,
                },
            });

            response.Ok.ShouldBeTrue($"Failed to create album: {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var id = json?.GetProperty("album").GetProperty("id").GetInt64()
                ?? throw new InvalidOperationException("Failed to get album ID from response");
            var name = json?.GetProperty("album").GetProperty("name").GetString()
                ?? album.Name;
            int? year = null;
            if (json?.GetProperty("album").TryGetProperty("year", out var yearProp) == true && yearProp.ValueKind != JsonValueKind.Null)
            {
                year = yearProp.GetInt32();
            }

            data.Add(new AlbumData(id, name, year));
        }

        return data;
    }
}
