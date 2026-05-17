using System.IO.Abstractions;
using System.Reflection;
using MyMusic.Common.Metadata;
using MyMusic.Common.Targets;

namespace MyMusic.Common.Tests.Utilities;

public static class MockMusicFile
{
    private static int _contentVariant = 0;

    public static byte[] GetTestMusicFile()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "MyMusic.Common.Tests.Resources.251progressie.mp3";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new Exception("Missing test audio file resource. The tests project is misconfigured.");
        }
        
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[] GetVariantMusicFile()
    {
        var content = GetTestMusicFile();
        var variant = ++_contentVariant;
        var result = new byte[content.Length + 4];
        Array.Copy(content, result, content.Length);
        result[content.Length] = (byte)(variant & 0xFF);
        result[content.Length + 1] = (byte)((variant >> 8) & 0xFF);
        result[content.Length + 2] = (byte)((variant >> 16) & 0xFF);
        result[content.Length + 3] = (byte)((variant >> 24) & 0xFF);
        return result;
    }
    
    public static void Create(IFileSystem fs, string filePath, string title, string album, string[] artists, string[] genres, int? year = null)
    {
        fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(filePath)!);
        
        Create(fs, filePath, new SongMetadata(null, title)
        {
            Album = new AlbumMetadata(null, album, new CoverArtMetadata(), new ArtistMetadata(null, artists.First())),
            Artists = artists.Select(a => new ArtistMetadata(null, a)).ToList(),
            Genres = genres.ToList(),
            Year = year,
        });
    }

    public static void CreateWithDifferentContent(IFileSystem fs, string filePath, string title, string album, string[] artists, string[] genres, int? year = null)
    {
        fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(filePath)!);

        Create(fs, filePath, new SongMetadata(null, title)
        {
            Album = new AlbumMetadata(null, album, new CoverArtMetadata(), new ArtistMetadata(null, artists.First())),
            Artists = artists.Select(a => new ArtistMetadata(null, a)).ToList(),
            Genres = genres.ToList(),
            Year = year,
        }, useVariantContent: true);
    }
    
    public static void Create(IFileSystem fs, string filePath, SongMetadata metadata, bool useVariantContent = false)
    {
        var content = useVariantContent ? GetVariantMusicFile() : GetTestMusicFile();
        fs.File.WriteAllBytes(filePath, content);
        
        var fileInfo = new FileSystemFileAbstraction(fs.FileInfo.New(filePath));
        
        using var tfile = TagLib.File.Create(fileInfo);

        TagConverter.FromSong(metadata, tfile.Tag).GetAwaiter().GetResult();
        FileTarget.RebuildTags(tfile);
        tfile.Save();
    }
}