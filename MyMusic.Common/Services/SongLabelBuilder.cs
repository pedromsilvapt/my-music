using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public static class SongLabelBuilder
{
    public static string Build(Song song)
    {
        var artists = string.Join(", ", song.Artists.Select(a => a.Artist.Name));
        var explicitSuffix = song.Explicit ? " (Explicit)" : "";
        return $"{song.Title}{explicitSuffix} - {artists}";
    }
}
