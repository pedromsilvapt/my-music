using System.Reflection;
using MyMusic.Common.Metadata;
using MyMusic.Common.Targets;

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

    public static byte[] CreateTestMusicFile(string title, string album, string[] artists, string[] genres, int? year = null)
    {
        var baseBytes = GetBaseTestMusicFile();
        
        var tempPath = Path.Combine(Path.GetTempPath(), $"mymusic_test_{Guid.NewGuid()}.mp3");
        
        try
        {
            File.WriteAllBytes(tempPath, baseBytes);
            
            using var tfile = TagLib.File.Create(tempPath);
            
            var metadata = new SongMetadata(null, title)
            {
                Album = new AlbumMetadata(null, album, new CoverArtMetadata(), new ArtistMetadata(null, artists.First())),
                Artists = artists.Select(a => new ArtistMetadata(null, a)).ToList(),
                Genres = genres.ToList(),
                Year = year,
            };
            
            TagConverter.FromSong(metadata, tfile.Tag).GetAwaiter().GetResult();
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
