using MyMusic.Common.Metadata;

namespace MyMusic.Common.NamingStrategies;

public class ArtistAlbumNamingStrategy : INamingStrategy
{
    public string Generate(SongMetadata metadata)
    {
        var artist = ((INamingStrategy)this).SanitizeFilename(metadata.Album?.Artist?.Name ?? metadata.Artists?.FirstOrDefault()?.Name ?? "(Unknown)");
        var album = ((INamingStrategy)this).SanitizeFilename(metadata.Album?.Name ?? "(No album)");

        // TODO Support multiple extensions
        return Path.Combine(artist, album, ((INamingStrategy)this).SanitizeFilename(metadata.SimpleLabel) + ".mp3");
    }
}