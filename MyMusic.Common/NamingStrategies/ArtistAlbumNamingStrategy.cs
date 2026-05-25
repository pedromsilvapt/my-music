using MyMusic.Common.Metadata;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.NamingStrategies;

public class ArtistAlbumNamingStrategy : INamingStrategy
{
    public string Generate(SongMetadata metadata, NamingMetadata? naming = null)
    {
        var albumArtist = metadata.Album?.Artist?.Name;
        if (string.IsNullOrEmpty(albumArtist))
            albumArtist = metadata.Artists?.FirstOrDefault()?.Name;
        if (string.IsNullOrEmpty(albumArtist))
            albumArtist = "(Unknown)";

        var albumName = metadata.Album?.Name;
        if (string.IsNullOrEmpty(albumName))
            albumName = "(No album)";

        var label = metadata.SimpleLabel;
        if (string.IsNullOrWhiteSpace(label))
            label = naming?.OriginalName ?? "(Untitled)";

        var artist = FilenameUtils.SanitizeFilename(albumArtist);
        var album = FilenameUtils.SanitizeFilename(albumName);
        var extension = naming?.Extension ?? ".mp3";

        return Path.Combine(artist, album, FilenameUtils.SanitizeFilename(label) + extension);
    }
}