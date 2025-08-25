using System.IO.Abstractions;
using System.Reflection;
using DotNext.Threading.Tasks;
using MyMusic.Common.Metadata;
using MyMusic.Common.Targets;

namespace MyMusic.Common.Tests.Utilities;

public static class MockMusicFile
{
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
    
    public static void Create(IFileSystem fs, string filePath, SongMetadata metadata)
    {
        fs.File.WriteAllBytes(filePath, GetTestMusicFile());
        
        var fileInfo = new FileSystemFileAbstraction(fs.FileInfo.New(filePath));
        
        using var tfile = TagLib.File.Create(fileInfo);

        TagConverter.FromSong(metadata, tfile.Tag).GetAwaiter().GetResult();

        tfile.Save();
    }
}