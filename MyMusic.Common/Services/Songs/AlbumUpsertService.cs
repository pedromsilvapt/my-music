using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

public class AlbumUpsertService : IAlbumUpsertService
{
    public async Task<Album> UpsertAsync(MusicDbContext db, long ownerId, string name, Artist artist,
        CancellationToken cancellationToken = default)
    {
        // Albums created earlier in this unit of work are not in the database yet
        var album = db.Albums.Local.FirstOrDefault(a =>
            a.OwnerId == ownerId && a.Name == name && ReferenceEquals(a.Artist, artist));

        // An artist that is not saved yet cannot have albums in the database
        if (album is null && artist.Id != 0)
        {
            album = await db.Albums.FirstOrDefaultAsync(
                a => a.OwnerId == ownerId && a.ArtistId == artist.Id && a.Name == name, cancellationToken);
        }

        if (album is not null)
        {
            album.Artist = artist;
            return album;
        }

        album = new Album
        {
            Name = name,
            Artist = artist,
            ArtistId = artist.Id,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };
        await db.AddAsync(album, cancellationToken);

        return album;
    }
}
