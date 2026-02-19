using MyMusic.Common.Metadata;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.NamingStrategies;

public class ArtistAlbumNamingStrategy : INamingStrategy
{
    public string Generate(SongMetadata metadata)
    {
        var artist = FilenameUtils.SanitizeFilename(metadata.Album?.Artist?.Name ??
                                                    metadata.Artists?.FirstOrDefault()?.Name ?? "(Unknown)");
        var album = FilenameUtils.SanitizeFilename(metadata.Album?.Name ?? "(No album)");

        return Path.Combine(artist, album, FilenameUtils.SanitizeFilename(metadata.SimpleLabel) + ".mp3");
    }
}