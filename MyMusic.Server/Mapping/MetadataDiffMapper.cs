using MyMusic.Common.Services;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.Mapping;

/// <summary>
/// Maps MetadataDiffModel (from Common project) to SongMetadataDiff (Server DTO).
/// </summary>
public static class MetadataDiffMapper
{
    public static SongMetadataDiff ToSongMetadataDiff(MetadataDiffModel model)
    {
        return new SongMetadataDiff
        {
            Title = MapField(model.Title),
            Year = MapField(model.Year),
            Lyrics = MapField(model.Lyrics),
            Rating = MapField(model.Rating),
            Explicit = MapField(model.Explicit),
            Cover = MapField(model.Cover),
            Album = MapAlbumField(model.Album),
            AlbumArtist = MapField(model.AlbumArtist),
            Artists = MapArtistsField(model.Artists),
            Genres = MapField(model.Genres),
        };
    }

    private static SongMetadataField<T>? MapField<T>(MetadataField<T>? field)
    {
        if (field == null) return null;

        return new SongMetadataField<T>
        {
            Old = field.Old,
            New = field.New,
        };
    }

    private static SongMetadataField<SongMetadataAlbum>? MapAlbumField(MetadataField<MetadataAlbumModel>? field)
    {
        if (field == null) return null;

        return new SongMetadataField<SongMetadataAlbum>
        {
            Old = new SongMetadataAlbum
            {
                Name = field.Old.Name,
                ArtistName = field.Old.ArtistName,
            },
            New = new SongMetadataAlbum
            {
                Name = field.New.Name,
                ArtistName = field.New.ArtistName,
            },
        };
    }

    private static SongMetadataField<List<SongMetadataArtist>>? MapArtistsField(MetadataField<List<MetadataArtistModel>>? field)
    {
        if (field == null) return null;

        return new SongMetadataField<List<SongMetadataArtist>>
        {
            Old = field.Old.Select(a => new SongMetadataArtist { Name = a.Name }).ToList(),
            New = field.New.Select(a => new SongMetadataArtist { Name = a.Name }).ToList(),
        };
    }
}
