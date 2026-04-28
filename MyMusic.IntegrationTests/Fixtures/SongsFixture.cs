using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class SongsFixture
{
    public static SampleSong[] DefaultSongs { get; } =
    [
        new("Yours Eternally (feat. Ed Sheeran & Taras Topolia)", "Days Of Ash EP", ["U2"], [], 2026),
        new("The Alibi", "The Alibi", ["Dylan"], [], 2024),
        new("Wicker Woman", "Wicker Woman", ["Freya Ridings"], [], 2025),
        new("The Fate of Ophelia", "The Life of a Showgirl", ["Taylor Swift"], [], 2025),
        new("Girl Of Your Dreams", "Girl Of Your Dreams", ["Dylan"], [], 2022),
        new("Sand", "Sand", ["Dove Cameron"], [], 2023),
        new("New Religion", "New Religion", ["Faithless", "Bebe Rexha"], [], 2026),
        new("Kyiv", "Hymns Of Resilience", ["Apashe", "Alina Pash"], [], 2026),
        new("Guerra Nuclear", "Guerra Nuclear", ["Marisa Liz"], [], 2022),
        new("Je remercie mon ex", "Je remercie mon ex", ["Camille Lellouche"], [], 2020),
        new("Marcha", "Marcha", ["Bárbara Bandeira"], [], 2026),
        new("Girl across the street", "Girl across the street", ["DAMONA"], [], 2023),
        new("God give me a car", "God give me a car", ["DAMONA"], [], 2023),
        new("Woman", "Woman", ["DAMONA"], [], 2024),
        new("Good Girl", "Good Girl", ["Paris Paloma"], [], 2026),
        new("To Love Somebody", "To Love Somebody", ["Holly Humberstone"], [], 2026),
        new("Opalite", "The Life of a Showgirl", ["Taylor Swift"], [], 2025),
        new("Carolina (From The Motion Picture Where The Crawdads Sing)", "Carolina (From The Motion Picture Where The Crawdads Sing)", ["Taylor Swift"], [], 2022),
        new("Kiss The Mountain", "II - Those We Don't Speak Of", ["Johanna Kurkela", "Auri"], [], 2021),
        new("Call Me (Remastered 2002)", "Greatest Hits", ["Blondie"], [], 2002),
        new("Maneater", "Loose", ["Nelly Furtado"], [], 2006),
        new("St. Purple & Green", "Visions Of A Life", ["Wolf Alice"], [], 2017),
        new("Hell with You", "Hell with You", ["Hanne Mjøen"], [], 2021),
        new("Killing Time", "I Still Hide", ["Alba August"], [], 2022),
        new("I Can't Go To The Party", "Hymn For Tomorrow", ["Rozzi"], [], 2021),
        new("Not To Anybody", "Not To Anybody", ["The Livelines"], [], 2025),
        new("In And Out Of Love", "In And Out of Love", ["Armin van Buuren", "Sharon den Adel"], [], 2008),
        new("Pretty", "Pretty", ["Astrid S", "Dagny"], [], 2021),
        new("Invisible (from the Netflix Film Klaus)", "Invisible (from the Netflix Film Klaus)", ["Zara Larsson"], [], 2019),
        new("Revolution", "All Or Nothing (Deluxe Edition)", ["Pennywise"], [], 2012),
        new("I Feel Embarrassed", "I Feel Embarrassed", ["The Livelines"], [], 2025),
        new("Song 2", "Blur", ["Blur"], [], 1997),
        new("Under Pressure (feat. David Bowie)", "Hot Space (Deluxe Edition 2011 Remaster)", ["Queen"], [], 1982),
        new("No Need to Try Harder", "No Need to Try Harder", ["Laura Cox"], [], 2025),
        new("The Expanse Theme", "The Expanse - The Collector's Edition", ["Clinton Shorter"], [], 2019),
        new("Shooting Star", "Shooting Star", ["Bad Company", "Halestorm"], [], 2025),
        new("Que O Amor Te Salve Nesta Noite Escura (Ao Vivo)", "Que O Amor Te Salve Nesta Noite Escura (Ao Vivo)", ["Sara Correia", "Pedro Abrunhosa"], [], 2022),
        new("Undress Me", "Undress Me", ["Hana Lili"], [], 2025),
        new("Here I Go Again (1987 Version) (1987 Version; 2017 Remaster)", "Whitesnake (30th Anniversary Edition)", ["Whitesnake"], [], 1987),
        new("Try", "Folklore", ["Nelly Furtado"], [], 2003),
        new("Legends Never Die", "Legends Never Die", ["League of Legends", "Against The Current"], [], 2017),
        new("Lost It To Trying (feat. Lily & Madeleine)", "Lanterns", ["Son Lux"], [], 2013),
        new("Hunter", "Time Ain't Accidental", ["Jess Williamson"], [], 2023),
        new("Time Ain't Accidental", "Time Ain't Accidental", ["Jess Williamson"], [], 2023),
        new("Endlessly", "MAXIMALISM (Deluxe Edition)", ["Amaranthe"], [], 2016),
        new("Helix", "HELIX", ["Amaranthe"], [], 2018),
        new("Burn With Me", "The Nexus", ["Amaranthe"], [], 2012),
        new("The Nexus", "The Nexus", ["Amaranthe"], [], 2012),
        new("Someday We Won't Live Here Anymore", "Someday We Won't Live Here Anymore", ["The Accidentals"], [], 2025),
        new("Better Than Me", "Better Than Me", ["The Accidentals"], [], 2025),
        new("Kamikaze", "All is Love and Pain in the Mouse Parade", ["Of Monsters and Men"], [], 2025),
        new("The Towering Skyscraper at the End of the Road", "Dream Team", ["Of Monsters and Men"], [], 2025),
        new("Tuna in a Can", "All is Love and Pain in the Mouse Parade", ["Of Monsters and Men"], [], 2025),
        new("Fruit Bat", "Fruit Bat", ["Of Monsters and Men"], [], 2025),
        new("Earth", "Wanderer (Expedition Happiness Soundtrack)", ["Mogli"], [], 2017),
        new("Ignorance is Bliss", "Ignorance is Bliss", ["Alice Merton"], [], 2025),
        new("Fleeting", "Fleeting", ["Sarah Kinsley"], [], 2025),
        new("Talk to You", "Talk to You", ["Elton John", "Sam Fender"], [], 2025),
        new("Little Bit Closer", "Little Bit Closer", ["Sam Fender"], [], 2025),
        new("Wild Long Lie", "Wild Long Lie", ["Sam Fender"], [], 2024),
        new("Arm's Length", "Arm's Length", ["Sam Fender"], [], 2025),
        new("Bittersweet", "Bittersweet", ["Culture Wars"], [], 2025),
        new("It Hurts", "It Hurts", ["Culture Wars"], [], 2024),
        new("Cette vie", "Mon sang", ["Clara Luciani"], [], 2024),
        new("Black Jeans", "E.G.O.", ["Lucie Silvas"], [], 2018),
        new("No One Knows Us", "Returning To Myself", ["Brandi Carlile"], [], 2025),
        new("Hush Baby, Hurry Slowly", "There's Always More That I Could Say", ["Sigrid"], [], 2025),
        new("If The World Burns Down", "Nobody Wants This Season 2: The Soundtrack", ["Kacey Musgraves"], [], 2025),
        new("Hawk For The Dove", "Take It Like A Man", ["Amanda Shires"], [], 2022),
        new("Here He Comes", "Take It Like A Man", ["Amanda Shires"], [], 2022),
        new("Piece Of Mind", "Piece Of Mind", ["Amanda Shires"], [], 2025),
        new("A Way It Goes", "A Way It Goes", ["Amanda Shires"], [], 2025),
        new("Little Bit", "Little Bit", ["Larkin Poe"], [], 2024),
        new("You Are The River", "Bloom", ["Larkin Poe"], [], 2025),
        new("Divorce In A Small Town", "Divorce In A Small Town", ["Caroline Jones"], [], 2025),
        new("Bad", "Bad", ["Dylan"], [], 2025),
        new("Rebel Child", "Rebel Child", ["Dylan"], [], 2023),
        new("Spring", "On Giacometti", ["Hania Rani"], [], 2023),
        new("Storm", "On Giacometti", ["Hania Rani"], [], 2023),
        new("Mountains", "On Giacometti", ["Hania Rani"], [], 2023),
        new("Dreamy", "On Giacometti", ["Hania Rani"], [], 2023),
        new("Time", "On Giacometti", ["Hania Rani"], [], 2023),
        new("Alberto", "On Giacometti", ["Hania Rani"], [], 2023),
        new("Annette", "On Giacometti", ["Hania Rani"], [], 2023),
        new("For I Am Death", "For I Am Death", ["The Pretty Reckless"], [], 2025),
        new("Heaven Knows", "Going to Hell", ["The Pretty Reckless"], ["Rock"], 2013),
        new("Nothing Lasts Forever", "Nothing Lasts Forever", ["Goo Goo Dolls"], [], 2025),
        new("Be Ok", "Be Ok", ["April Ivy"], [], 2016),
        new("Thérèse", "MOSS", ["Maya Hawke"], [], 2022),
        new("Meu Amor de Longe", "Meu Amor de Longe", ["Raquel Tavares"], [], 2016),
        new("Quero É Viver", "Quero É Viver", ["Sara Correia"], [], 2022),
        new("Desfado (Live)", "Moura (Deluxe Version)", ["Ana Moura"], [], 2016),
        new("Dia De Folga", "Moura", ["Ana Moura"], [], 2015),
        new("The Kill", "A Beautiful Lie", ["Thirty Seconds To Mars"], [], 2005),
        new("Runaway", "XXI Century Blood", ["The Warning"], [], 2017),
        new("AMOUR", "ERROR", ["The Warning"], [], 2022),
        new("Weatherman", "Dead Sara", ["Dead Sara"], [], 2012),
        new("Hot Goblin", "Hot Goblin", ["Em Beihold"], [], 2025),
        new("In the Shadows", "Dead Letters", ["The Rasmus"], [], 2003),
        new("The Pretender", "Echoes, Silence, Patience & Grace", ["Foo Fighters"], [], 2007),
        new("Asking For A Friend", "Asking For A Friend", ["Foo Fighters"], [], 2025),
        new("Master Pretender", "Stay Gold", ["First Aid Kit"], [], 2014),
        new("Oats In The Water", "The Burgh Island EP", ["Ben Howard"], [], 2012),
        new("Seja Agora", "Mundo Pequenino", ["Deolinda"], [], 2013),
        new("Everybody Scream", "Everybody Scream", ["Florence + The Machine"], [], 2025),
        new("Sympathy Magic", "Sympathy Magic", ["Florence + The Machine"], [], 2025),
        new("Just Two Girls", "The Clearing", ["Wolf Alice"], [], 2025),
        new("Glum", "Ego Death At A Bachelorette Party", ["Hayley Williams"], [], 2025),
        new("last night's mascara", "Vertigo (Tour Edition)", ["Griff"], [], 2024),
        new("The Weaver's Song", "The Weaver's Song", ["EchoesOfVelandria"], [], 2025),
        new("Is There Anybody Out There", "The Cosmic Selector Vol. 1", ["Lord Huron"], [], 2025),
        new("Wearing Nothing", "Wearing Nothing", ["Dagny"], [], 2017),
        new("A Different Kind of Love", "Tomorrows III", ["Son Lux"], [], 2021),
        new("Human", "Hørizøns", ["Beyond The Black"], [], 2020),
        new("You're Not Alone", "Hørizøns", ["Beyond The Black"], [], 2020),
        new("Beautiful Lies (feat. Rick Altzi)", "Lost In Forever (Tour Edition)", ["Beyond The Black"], [], 2016),
        new("Forget My Name (Re-Recorded)", "Heart of the Hurricane (Black Edition)", ["Beyond The Black"], [], 2018),
        new("In the Shadows", "Heart of the Hurricane (Black Edition)", ["Beyond The Black"], [], 2018),
        new("Into The Light", "Beyond The Black", ["Beyond The Black"], [], 2023),
        new("Break The Silence", "Break The Silence", ["Beyond The Black"], [], 2025),
        new("Don't Waste Your Love On Me", "Don't Waste Your Love On Me", ["Ellysse Mason"], [], 2025),
        new("My Sanity", "Age Of Unreason", ["Bad Religion"], [], 2019),
        new("Believer", "Time Of My Life", ["3 Doors Down"], [], 2011),
        new("Around the World (La La La La La)", "Planet Pop", ["A Touch of Class"], [], 2000),
        new("Manos Al Aire", "Mi Plan", ["Nelly Furtado"], [], 2009),
        new("Bad Romance", "The Fame Monster", ["Lady Gaga"], ["Pop"], 2010),
        new("Ignorance", "Riot!", ["Paramore"], ["Other"], 2009),
        new("Ignorance", "Ignorance", ["Paramore"], [], 2009),
        new("One Minute", "Play Hard EP", ["Krewella"], ["Dubstep"], 2012),
        new("You Give Me Something", "Undiscovered (International Version)", ["James Morrison"], ["Other"], 2006),
        new("Saturday Sunday", "My Garden", ["Kat Dahlia"], ["Pop"], 2015),
        new("End Of All Hope", "Century Child", ["Nightwish"], ["Power metal", "Symphonic metal"], 2002),
        new("Straight Into The Fire", "True Colors", ["Zedd"], ["Dance"], 2015),
        new("Memories", "The Silent Force", ["Within Temptation"], ["World"], 2005),
        new("I Miss You", "The Henningsens EP", ["The Henningsens"], [], 2013),
        new("Forward", "Forward", ["Amy Macdonald"], [], 2025),
        new("Make A Move", "Scripted", ["Icon For Hire"], [], 2011),
        new("Fight", "Scripted", ["Icon For Hire"], ["Hard rock", "Alternative metal"], 2011),
        new("Theatre", "Scripted", ["Icon for Hire"], ["Hard rock", "Punk rock"], 2011),
        new("Bleeding Love", "Spirit", ["Leona Lewis"], [], null),
        new("Bleeding Love", "Bleeding Love", ["Leona Lewis"], [], 2007),
        new("When You're Young", "When You're Young", ["Clara Mae"], [], 2023),
        new("this is how i learn to say no", "this is how i learn to say no", ["EMELINE"], [], 2021),
        new("what it means to be a girl", "what it means to be a girl / this is how i learn to say no", ["EMELINE"], [], 2021),
        new("Waiting for the End", "A Thousand Suns", ["Linkin Park"], ["Metal"], 2010),
        new("Lost in the Echo", "Living Things", ["Linkin Park"], ["Metal"], 2012),
        new("Two Faced", "Two Faced", ["Linkin Park"], [], 2024),
    ];

    public async Task<List<SongData>> SeedAsync(IAPIRequestContext api, long userId, SampleSong[]? songs = null)
    {
        var sampleSongs = songs ?? DefaultSongs;
        var data = new List<SongData>();

        foreach (var song in sampleSongs)
        {
            var mp3Content = TestFiles.CreateTestMusicFile(song.Title, song.Album, song.Artists, song.Genres, song.Year);
            var safeFileName = string.Join("_", song.Title.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeFileName}.mp3";
            var modifiedAt = DateTime.UtcNow.ToString("o");
            var createdAt = DateTime.UtcNow.ToString("o");

            var filePayload = new FilePayload
            {
                Name = fileName,
                MimeType = "audio/mpeg",
                Buffer = mp3Content,
            };

            var multipart = api.CreateFormData();
            multipart.Set("file", filePayload);
            multipart.Set("path", $"/music/{safeFileName}.mp3");
            multipart.Set("modifiedAt", modifiedAt);
            multipart.Set("createdAt", createdAt);

            var response = await api.PostAsync("/api/songs/upload", new()
            {
                Multipart = multipart,
            });

            response.Ok.ShouldBeTrue($"Failed to upload song '{song.Title}': {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var success = json?.GetProperty("success").GetBoolean() ?? false;
            if (!success)
            {
                var error = json?.GetProperty("error").GetString() ?? "Unknown error";
                throw new InvalidOperationException($"Upload failed: {error}");
            }

            var songId = json?.GetProperty("songId").GetInt64()
                ?? throw new InvalidOperationException("Failed to get song ID from response");

            var devicePaths = new Dictionary<long, string>();

            if (song.DeviceIds != null && song.DeviceIds.Length > 0)
            {
                var updates = song.DeviceIds.Select(d => new { DeviceId = d, Include = true }).ToArray();
                var associateResponse = await api.PutAsync("/api/songs/devices", new()
                {
                    DataObject = new
                    {
                        SongIds = new[] { songId },
                        Updates = updates
                    }
                });

                if (!associateResponse.Ok)
                {
                    var responseText = await associateResponse.TextAsync();
                    throw new InvalidOperationException($"Failed to associate song '{song.Title}' (ID: {songId}) with devices [{string.Join(", ", song.DeviceIds)}]: Status={associateResponse.Status}, Response={responseText}");
                }

                foreach (var deviceId in song.DeviceIds)
                {
                    var deviceSongsResponse = await api.GetAsync($"/api/devices/{deviceId}/sync/songs");
                    if (deviceSongsResponse.Ok)
                    {
                        var deviceSongsData = await deviceSongsResponse.JsonAsync();
                        var deviceSongs = deviceSongsData?.GetProperty("songs").EnumerateArray();
                        var deviceSong = deviceSongs?.FirstOrDefault(s => s.GetProperty("songId").GetInt64() == songId);
                        if (deviceSong.HasValue)
                        {
                            var devicePath = deviceSong.Value.GetProperty("path").GetString();
                            devicePaths[deviceId] = devicePath;
                        }
                    }
                }
            }

            data.Add(new SongData(songId, song.Title, song.Year, devicePaths.Count > 0 ? devicePaths : null));
        }

        return data;
    }
}
