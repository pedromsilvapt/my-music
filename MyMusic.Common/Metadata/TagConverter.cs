using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using TagLib;

namespace MyMusic.Common.Metadata;

public static class TagConverter
{
    private static readonly Regex ExplicitRegex = new Regex(@"\s+\(Explicit\)\s*$");

    #region To Metadata

    public static SongMetadata ToSong(Tag tag, TagLib.Properties properties)
    {
        var album = ToAlbum(tag);

        var title = tag.Title ?? "";

        title = TrimExplicit(title, out var isExplicit);

        decimal? rating = null;

        if (tag is TagLib.Id3v2.Tag id3Tag)
        {
            var ratingFrame =
                TagLib.Id3v2.PopularimeterFrame.Get(id3Tag, "Windows Media Player 9 Series", create: true);

            rating = (int?)ratingFrame.Rating;

            if (rating == 0)
            {
                rating = null;
            }

            if (rating > 5)
            {
                rating = Math.Round(Math.Clamp(rating.Value * 5m / 255m, 0, 5), 1);
            }
        }

        return new SongMetadata(null, title)
        {
            Explicit = isExplicit,
            Artists = tag.Performers?.Select(name => new ArtistMetadata(null, name))?.ToList(),
            Genres = tag.Genres?.ToList(),
            Album = album,
            Year = tag.Year > 0 ? (int)tag.Year : null,
            Lyrics = tag.Lyrics,
            Duration = properties.Duration,
            Rating = rating,
            Track = tag.Track > 0 ? (int)tag.Track : null
        };
    }

    public static AlbumMetadata ToAlbum(Tag tag)
    {
        var coverArt = ToCoverArt(tag);

        var album = new AlbumMetadata(null, "", coverArt);

        if (!string.IsNullOrEmpty(tag.Album))
        {
            album.Name = tag.Album;
        }

        if (tag.FirstAlbumArtist != null)
        {
            album.Artist = new ArtistMetadata(null, tag.FirstAlbumArtist);
        }

        return album;
    }

    public static CoverArtMetadata ToCoverArt(Tag tag)
    {
        var coverArt = new CoverArtMetadata();

        if (tag.Pictures is not { Length: > 0 })
        {
            return coverArt;
        }

        // Take only the 3 biggest pictures
        // TODO This means we lose any other artwork currently saved on the file. We should avoid that if possible
        var frontCovers = tag.Pictures.Where(t => t.Type == PictureType.FrontCover)
            .OrderByDescending(d => d.Data.Data.Length)
            .Take(3)
            .ToArray();

        if (frontCovers.Length >= 1)
        {
            coverArt.Big = ImageBuffer.FromPicture(frontCovers[0]).ToBase64Url();
        }

        if (frontCovers.Length >= 2)
        {
            coverArt.Normal = ImageBuffer.FromPicture(frontCovers[1]).ToBase64Url();
        }

        if (frontCovers.Length >= 3)
        {
            coverArt.Small = ImageBuffer.FromPicture(frontCovers[2]).ToBase64Url();
        }

        return coverArt;
    }

    #endregion To Metadata

    #region From Metadata

    public static async Task FromSong(SongMetadata metadata, Tag tag, ImageFormat? imageFormat = null,
        CancellationToken cancellationToken = default)
    {
        tag.Title = TrimExplicit(metadata.Title);

        if (metadata.Explicit)
        {
            tag.Title += " (Explicit)";
        }

        tag.Track = (uint)(metadata.Track ?? 0);
        tag.Year = (uint)(metadata.Year ?? 0);
        tag.Lyrics = metadata.Lyrics;

        #region Picture

        var pictureUrl = metadata.Album?.CoverArt?.Biggest;

        IPicture[] pictures = [];

        if (metadata.Album is not null)
        {
            tag.Album = metadata.Album.Name;
        }

        if (pictureUrl is not null && metadata.Album is not null)
        {
            var originalPicturePath = await ImageBuffer.FromStringAsync(pictureUrl, cancellationToken);

            if (imageFormat != null)
            {
                originalPicturePath = originalPicturePath.ToFormat(imageFormat);
            }

            pictures = [originalPicturePath.ToPicture()];
        }

        tag.Pictures = pictures;

        #endregion Picture

        tag.Performers = metadata.Artists?.Select(artist => artist.Name)?.ToArray();

        var albumArtist = metadata.Album?.Artist?.Name ?? metadata.Artists?[0]?.Name;
        tag.AlbumArtists = albumArtist is not null ? [albumArtist] : [];

        tag.Genres = metadata.Genres?.ToArray();

        if (tag is TagLib.Id3v2.Tag id3Tag)
        {
            var ratingFrame =
                TagLib.Id3v2.PopularimeterFrame.Get(id3Tag, "Windows Media Player 9 Series", create: true);

            ratingFrame.Rating = (byte)Math.Clamp((metadata.Rating ?? 0) * 255m / 5m, 0, 255);
        }
    }

    #endregion From Metadata

    #region Utility Methods

    public static string TrimExplicit(string title, out bool isExplicit)
    {
        var newTitle = ExplicitRegex.Replace(title, "");

        isExplicit = newTitle != title;

        return newTitle;
    }

    public static string TrimExplicit(string title)
    {
        return TrimExplicit(title, out _);
    }

    #endregion Utility Methods
}