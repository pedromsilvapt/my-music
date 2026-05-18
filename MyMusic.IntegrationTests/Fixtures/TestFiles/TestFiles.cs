using System.Reflection;
using MyMusic.Common.Metadata;
using MyMusic.Common.Targets;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Fixtures;

public static class TestFiles
{
    private static byte[] GetBaseTestMusicFile()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "MyMusic.IntegrationTests.Fixtures.TestFiles.Resources.251progressie.mp3";

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new Exception("Missing test audio file resource. The tests project is misconfigured.");
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static byte[] CreateTestMusicFile(SampleSong song, int? contentVariant = null)
    {
        var baseBytes = GetBaseTestMusicFile();

        var tempPath = Path.Combine(Path.GetTempPath(), $"mymusic_test_{Guid.NewGuid()}.mp3");

        try
        {
            File.WriteAllBytes(tempPath, baseBytes);

            using var tfile = TagLib.File.Create(tempPath);

            var hasFullMetadata = song.Title is not null
                && song.Album is not null
                && song.Artists is not null;

            if (hasFullMetadata)
            {
                var metadata = new SongMetadata(null, song.Title)
                {
                    Album = new AlbumMetadata(null, song.Album, new CoverArtMetadata(), song.AlbumArtist is not null ? new ArtistMetadata(null, song.AlbumArtist) : null),
                    Artists = song.Artists.Select(a => new ArtistMetadata(null, a)).ToList(),
                    Genres = song.Genres?.ToList() ?? [],
                    Year = song.Year,
                    Lyrics = song.Lyrics ?? (contentVariant.HasValue ? $"Variant {contentVariant.Value}" : null),
                };

                TagConverter.FromSong(metadata, tfile.Tag).GetAwaiter().GetResult();
            }
            else
            {
                if (song.Title is not null)
                {
                    tfile.Tag.Title = song.Title;
                }

                if (song.Album is not null)
                {
                    tfile.Tag.Album = song.Album;
                }

                if (song.AlbumArtist is not null)
                {
                    tfile.Tag.AlbumArtists = [song.AlbumArtist];
                }

                if (song.Artists is not null)
                {
                    tfile.Tag.Performers = song.Artists;
                }

                if (song.Genres is not null)
                {
                    tfile.Tag.Genres = song.Genres;
                }

                if (song.Year is not null)
                {
                    tfile.Tag.Year = (uint)song.Year.Value;
                }

                if (song.Lyrics is not null)
                {
                    tfile.Tag.Lyrics = song.Lyrics;
                }

                if (contentVariant.HasValue)
                {
                    if (song.Lyrics is null)
                    {
                        tfile.Tag.Lyrics = $"Variant {contentVariant.Value}";
                    }
                }
            }

            FileTarget.RebuildTags(tfile);
            tfile.Save();

            return File.ReadAllBytes(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
