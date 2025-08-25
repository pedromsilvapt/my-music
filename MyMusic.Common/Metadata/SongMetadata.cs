using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

namespace MyMusic.Common.Metadata;

public class SongMetadata
{
    public string? Id { get; set; }

    public string Title { get; set; }

    public AlbumMetadata? Album { get; set; }

    public List<ArtistMetadata>? Artists { get; set; }

    public List<string>? Genres { get; set; }

    public int? Track { get; set; } = null;

    public decimal? Rating { get; set; } = null;

    public int? Year { get; set; } = null;

    public TimeSpan Duration { get; set; } = TimeSpan.Zero;

    public bool Explicit { get; set; } = false;

    public string? Lyrics { get; set; } = null;

    public SongMetadata(string? id, string title, AlbumMetadata? album = null, List<ArtistMetadata>? artists = null)
    {
        Id = id;
        Title = title;
        Album = album;
        Artists = artists;
    }

    public string SimpleLabel
    {
        get
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(Title);

            if (Explicit)
            {
                sb.Append(" (Explicit)");
            }

            var artistsLabel = ArtistsLabel;

            if (artistsLabel is not null)
            {
                sb.Append($" - {artistsLabel}");
            }

            return sb.ToString();
        }
    }

    public string FullLabel
    {
        get
        {
            if (Album?.Name is not null)
            {
                return SimpleLabel + $" - {Album.Name}";
            }

            return SimpleLabel;
        }
    }

    public string? ArtistsLabel
    {
        get
        {
            if (Album?.Artist is not null || Artists?.Count > 0)
            {
                return String.Join(", ", GetArtistsNormalized().Select(a => a.Name));
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Returns the list of artists in a normalized fashion, that follow these rules:
    ///   - The Album artist is always the first one, if it exists
    ///   - The other artists are returned in alphabetical order
    ///   - The returned artists are always distinct
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ArtistMetadata> GetArtistsNormalized() {
        var artistNames = new HashSet<string>();

        if (Album?.Artist is not null)
        {
            yield return Album.Artist;
            artistNames.Add(Album.Artist.Name);
        }

        if (Artists is not null)
        {
            foreach (var artist in Artists.OrderBy(a => a.Name))
            {
                if (!artistNames.Contains(artist.Name))
                {
                    yield return artist;
                    artistNames.Add(artist.Name);
                }
            }
        }
    }

    public SongMetadata Clone(bool album = false, bool artists = false, bool genres = false)
    {
        // TODO Decide if we remove the != null or if we mark this property as nullable
        AlbumMetadata? albumMetadata = (album && this.Album != null) 
            ? this.Album.Clone() 
            : this.Album;

        // TODO Decide if we remove the != null or if we mark this property as nullable
        List<ArtistMetadata>? artistsMetadata = (artists && this.Artists != null) 
            ? this.Artists.Select(a => a.Clone()).ToList() 
            : this.Artists;

        var genresMetadata = genres && this.Genres != null ? this.Genres.Select(a => a).ToList() : this.Genres;

        return new SongMetadata(Id, Title, albumMetadata, artistsMetadata)
        {
            Genres = genresMetadata,
            Year = Year,
            Explicit = Explicit,
            Lyrics = Lyrics
        };
    }
}