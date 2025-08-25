using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

namespace MyMusic.Common.Metadata;

public class AlbumMetadata
{
    public string? Id { get; set; }

    public CoverArtMetadata CoverArt { get; set; }

    public string Name { get; set; }

    public List<SongMetadata>? Songs { get; set; }

    public ArtistMetadata? Artist { get; set; }

    public AlbumMetadata(string? id, string name, CoverArtMetadata coverArt, ArtistMetadata? artist = null, List<SongMetadata>? songs = null)
    {
        Id = id;
        Name = name;
        CoverArt = coverArt;
        Artist = artist;
        Songs = songs;
    }

    public AlbumMetadata Clone(bool artist = false, bool songs = false)
    {
        var artistCloned = artist 
            ? Artist?.Clone() 
            : Artist;

        var songsCloned = songs 
            ? Songs?.Select(s => s.Clone(artists: true, genres: true))?.ToList() 
            : Songs;

        return new AlbumMetadata(Id, Name, CoverArt.Clone(), artistCloned, songsCloned);
    }
}