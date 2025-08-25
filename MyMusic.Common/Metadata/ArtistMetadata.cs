using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyMusic.Common.Metadata;

public class ArtistMetadata
{
    public string? Id { get; set; }

    public string Name { get; set; }

    public List<AlbumMetadata>? Albums { get; set; }

    public ArtistMetadata(string? id, string name, List<AlbumMetadata>? albums = null)
    {
        Id = id;
        Name = name;
        Albums = albums;
    }

    public ArtistMetadata Clone(bool albums = false)
    {
        var albumsCloned = albums 
            ? Albums?.Select(album => album.Clone())?.ToList() 
            : Albums;

        return new ArtistMetadata(Id, Name, albumsCloned);
    }
}