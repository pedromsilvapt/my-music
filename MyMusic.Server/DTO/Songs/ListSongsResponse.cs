using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record ListSongsResponse
{
    public required IEnumerable<Song> Songs { get; set; }

    public record Song
    {
        public required long Id { get; set; }
        public required long? Cover { get; set; }
        public required string Title { get; set; }
        public required IEnumerable<Artist> Artists { get; set; }
        public required Album Album { get; set; }
        public required IEnumerable<Genre> Genres { get; set; }
        public required int? Year { get; set; }
        public required string Duration { get; set; }
        public required bool IsFavorite { get; set; }
        public required bool IsExplicit { get; set; }

        public static Song FromEntity(Entities.Song song)
        {
            var artists = song.Artists.Select(a => Artist.FromEntity(a.Artist)).ToList();
            var genres = song.Genres.Select(g => Genre.FromEntity(g.Genre)).ToList();
            var album = Album.FromEntity(song.Album);

            return new Song
            {
                Id = song.Id,
                Cover = song.CoverId,
                Title = song.Title,
                Artists = artists,
                Album = album,
                Genres = genres,
                Year = song.Year,
                Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
                IsFavorite = false,
                IsExplicit = song.Explicit
            };
        }
    }

    public record Artist
    {
        public required long Id { get; set; }
        public required string Name { get; set; }

        public static Artist FromEntity(Entities.Artist artist)
        {
            return new Artist
            {
                Id = artist.Id,
                Name = artist.Name,
            };
        }
    }

    public record Album
    {
        public required long Id { get; set; }
        public required string Name { get; set; }

        public static Album FromEntity(Entities.Album album)
        {
            return new Album
            {
                Id = album.Id,
                Name = album.Name,
            };
        }
    }

    public record Genre
    {
        public required long Id { get; set; }
        public required string Name { get; set; }

        public static Genre FromEntity(Entities.Genre genre)
        {
            return new Genre
            {
                Id = genre.Id,
                Name = genre.Name,
            };
        }
    }
}