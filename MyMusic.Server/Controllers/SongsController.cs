using System.Globalization;
using System.IO.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Filters;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("songs")]
public class SongsController(
    ILogger<SongsController> logger,
    ICurrentUser currentUser,
    IOptions<Config> config,
    ISongUpdateService songUpdateService,
    IMusicService musicService,
    IFileSystem fileSystem,
    ILogger<MusicImportJob> importJobLogger)
    : ControllerBase
{
    private readonly ILogger<SongsController> _logger = logger;
    private readonly ISongUpdateService _songUpdateService = songUpdateService;
    private readonly IMusicService _musicService = musicService;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<MusicImportJob> _importJobLogger = importJobLogger;

    [HttpGet(Name = "ListSongs")]
    public async Task<ListSongsResponse> List(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? filter = null,
        [FromQuery] string? search = null)
    {
        var query = context.Songs
            .Where(s => s.OwnerId == currentUser.Id)
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, s => s.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            DynamicFilterBuilder.ResolveEntityPaths(filterRequest, GetSongFieldMappings());
            var filterExpression = DynamicFilterBuilder.BuildFilter<Song>(filterRequest);
            query = query.Where(filterExpression);
        }

        var songs = await query
            .OrderBy(s => s.Title)
            .ToListAsync(cancellationToken);

        return new ListSongsResponse
        {
            Songs = songs.Select(ListSongsItem.FromEntity).ToList(),
        };
    }

    [HttpGet("{id:long}", Name = "GetSong")]
    public async Task<GetSongResponse> Get(long id, MusicDbContext context, CancellationToken cancellationToken)
    {
        var song = await context.Songs
            .Where(s => s.Id == id && s.OwnerId == currentUser.Id)
            .Include(s => s.Album)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .Include(s => s.Devices)
            .ThenInclude(sd => sd.Device)
            .FirstOrDefaultAsync(cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {id}");
        }

        return new GetSongResponse
        {
            Song = GetSongResponseSong.FromEntity(song),
        };
    }

    [HttpGet("{id}/download", Name = "DownloadSong")]
    public async Task<IActionResult> Download(MusicDbContext context, IFileSystem fileSystem, long id,
        CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .SingleAsync(s => s.Id == id && s.OwnerId == currentUser.Id, cancellationToken);

        var fileStream = fileSystem.File.OpenRead(songs.RepositoryPath);

        new FileExtensionContentTypeProvider().TryGetContentType(songs.RepositoryPath, out var contentType);

        return File(fileStream, contentType ?? "audio/mpeg", enableRangeProcessing: true,
            fileDownloadName: fileSystem.Path.GetFileName(songs.RepositoryPath));
    }

    [HttpPost("import", Name = "ImportSongs")]
    public async Task<object> Import(
        [FromForm] string sourceFolder,
        [FromServices] IMusicService musicService,
        [FromServices] MusicImportJob job,
        [FromServices] MusicDbContext db,
        CancellationToken cancellationToken)
    {
        await musicService.ImportRepositorySongs(db, job, currentUser.Id, sourceFolder,
            cancellationToken: cancellationToken);

        return new { success = true };
    }

    [HttpPost("upload", Name = "UploadSong")]
    public async Task<UploadSongResponse> Upload(
        IFormFile file,
        [FromForm] string path,
        [FromForm] string modifiedAt,
        [FromForm] string createdAt,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return new UploadSongResponse { Success = false, Error = "No file provided" };
        }

        var repositoryPath = config.Value.MusicRepositoryPath
                             ?? throw new Exception("MusicRepositoryPath not configured");

        var tempPath = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), $"mymusic_upload_{Guid.NewGuid()}");
        _fileSystem.Directory.CreateDirectory(tempPath);

        try
        {
            var tempFilePath = _fileSystem.Path.Combine(tempPath, _fileSystem.Path.GetFileName(path));
            await using (var stream = _fileSystem.FileStream.New(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var modifiedAtDateTime = DateTime.Parse(modifiedAt, null, DateTimeStyles.RoundtripKind);
            var createdAtDateTime = DateTime.Parse(createdAt, null, DateTimeStyles.RoundtripKind);

            var songImportMetadata = new SongImportMetadata(tempFilePath, createdAtDateTime, modifiedAtDateTime);

            var job = new MusicImportJob(_importJobLogger);

            await _musicService.ImportRepositorySongs(
                context,
                job,
                currentUser.Id,
                new[] { songImportMetadata },
                null,
                DuplicateSongsHandlingStrategy.SkipIdentical,
                cancellationToken);

            var importedSong = job.SongMapping.Values.FirstOrDefault();

            if (importedSong == null)
            {
                var skipReason = job.SkipReasons.FirstOrDefault(s => s.SourceFilePath == tempFilePath);
                var exception = job.Exceptions.FirstOrDefault();

                var errorParts = new List<string>();

                if (skipReason != null)
                {
                    errorParts.Add(FormatLogMessage(skipReason.Message, skipReason.MessageArgs));
                }

                if (exception != null)
                {
                    errorParts.Add($"Exception: {exception.Message}");
                }

                if (skipReason == null && exception == null)
                {
                    errorParts.Add("No song was imported and no skip reason was recorded");
                }

                return new UploadSongResponse { Success = false, Error = string.Join("; ", errorParts) };
            }

            _logger.LogInformation("Uploaded file {Path}, song ID: {SongId}", path, importedSong.Id);

            return new UploadSongResponse { Success = true, SongId = importedSong.Id };
        }
        finally
        {
            if (_fileSystem.Directory.Exists(tempPath))
            {
                _fileSystem.Directory.Delete(tempPath, true);
            }
        }
    }

    private static string FormatLogMessage(string template, object[] args)
    {
        if (args == null || args.Length == 0)
        {
            return template;
        }

        var index = 0;
        var formattedTemplate = System.Text.RegularExpressions.Regex.Replace(template, @"\{(\w+)\}", _ => $"{{{index++}}}");
        return string.Format(formattedTemplate, args);
    }

    [HttpPost("{id:long}/favorite", Name = "ToggleSongFavorite")]
    public async Task<ToggleFavoriteResponse> ToggleFavorite(long id, MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var song = await context.Songs
            .Where(s => s.Id == id && s.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {id}");
        }

        song.IsFavorite = !song.IsFavorite;
        await context.SaveChangesAsync(cancellationToken);

        return new ToggleFavoriteResponse
        {
            IsFavorite = song.IsFavorite,
        };
    }

    [HttpPost("favorites", Name = "ToggleFavorites")]
    public async Task<ToggleFavoritesResponse> ToggleFavorites([FromBody] ToggleFavoritesRequest request,
        MusicDbContext context, CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .Where(s => request.Ids.Contains(s.Id) && s.OwnerId == currentUser.Id)
            .ToListAsync(cancellationToken);

        var songDict = songs.ToDictionary(s => s.Id);
        var result = new List<ToggleFavoriteItem>();

        foreach (var id in request.Ids)
        {
            if (songDict.TryGetValue(id, out var song))
            {
                song.IsFavorite = !song.IsFavorite;
                result.Add(new ToggleFavoriteItem { Id = id, IsFavorite = song.IsFavorite });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ToggleFavoritesResponse
        {
            Songs = result,
        };
    }

    [HttpGet("{id:long}/devices", Name = "GetSongDevices")]
    public async Task<GetSongDevicesResponse> GetDevices(long id, MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var devices = await context.Devices
            .Where(d => d.OwnerId == currentUser.Id)
            .ToListAsync(cancellationToken);

        var songDevices = await context.SongDevices
            .Where(sd => sd.SongId == id && sd.Device.OwnerId == currentUser.Id)
            .ToListAsync(cancellationToken);

        var songDeviceDict = songDevices.ToDictionary(sd => sd.DeviceId);

        var items = devices.Select(d =>
        {
            songDeviceDict.TryGetValue(d.Id, out var sd);
            return new SongDeviceItem
            {
                DeviceId = d.Id,
                DeviceName = d.Name,
                DeviceIcon = d.Icon,
                DeviceColor = d.Color,
                Path = sd?.DevicePath,
                SyncAction = sd?.SyncAction?.ToString(),
            };
        }).ToList();

        return new GetSongDevicesResponse { Devices = items };
    }

    [HttpPut("devices", Name = "UpdateSongDevices")]
    public async Task<UpdateSongDevicesResponse> UpdateDevices(
        [FromBody] UpdateSongDevicesRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .Where(s => request.SongIds.Contains(s.Id) && s.OwnerId == currentUser.Id)
            .Include(s => s.Album)
            .ThenInclude(a => a!.Artist)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .ToListAsync(cancellationToken);

        if (songs.Count == 0)
        {
            throw new Exception("No songs found");
        }

        var deviceIds = request.Updates.Select(u => u.DeviceId).Distinct().ToList();
        var devices = await context.Devices
            .Where(d => deviceIds.Contains(d.Id) && d.OwnerId == currentUser.Id)
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        foreach (var update in request.Updates)
        {
            if (!devices.TryGetValue(update.DeviceId, out var device))
            {
                continue;
            }

            var namingStrategy = new TemplateNamingStrategy(
                device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

            var existingSongDevices = await context.SongDevices
                .Where(sd => request.SongIds.Contains(sd.SongId) && sd.DeviceId == update.DeviceId)
                .ToListAsync(cancellationToken);

            var existingDict = existingSongDevices.ToDictionary(sd => sd.SongId);

            foreach (var song in songs)
            {
                var hasExisting = existingDict.TryGetValue(song.Id, out var existing);

                if (update.Include && !hasExisting)
                {
                    var metadata = EntityConverter.ToSong(song);
                    var newSongDevice = new SongDevice
                    {
                        SongId = song.Id,
                        DeviceId = update.DeviceId,
                        DevicePath = namingStrategy.Generate(metadata),
                        SyncAction = SongSyncAction.Download,
                        AddedAt = DateTime.UtcNow,
                    };
                    context.SongDevices.Add(newSongDevice);
                }
                else if (!update.Include && hasExisting)
                {
                    if (existing!.SyncAction == SongSyncAction.Download)
                    {
                        context.SongDevices.Remove(existing);
                    }
                    else
                    {
                        existing.SyncAction = SongSyncAction.Remove;
                    }
                }
                else if (update.Include && hasExisting && existing!.SyncAction == SongSyncAction.Remove)
                {
                    existing.SyncAction = null;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new UpdateSongDevicesResponse { Success = true };
    }

    [HttpGet("filter-metadata", Name = "GetSongFilterMetadata")]
    public Task<FilterMetadataResponse> GetFilterMetadata(MusicDbContext context, CancellationToken cancellationToken)
    {
        var operators = FilterMetadataHelper.GetOperatorMetadata();
        var fields = GetSongFieldMetadata();

        return Task.FromResult(new FilterMetadataResponse
        {
            Fields = fields,
            Operators = operators,
        });
    }

    [HttpGet("filter-values", Name = "GetSongFilterValues")]
    public async Task<FilterValuesResponse> GetFilterValues(
        [FromQuery] string field,
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = field switch
        {
            "title" => context.Songs
                .Where(s => s.OwnerId == currentUser.Id)
                .Select(s => s.Title)
                .Distinct(),
            "label" => context.Songs
                .Where(s => s.OwnerId == currentUser.Id)
                .Select(s => s.Label)
                .Distinct(),
            "album.name" => context.Albums
                .Where(a => a.OwnerId == currentUser.Id)
                .Select(a => a.Name)
                .Distinct(),
            "artist.name" => context.Artists
                .Where(a => a.OwnerId == currentUser.Id)
                .Select(a => a.Name)
                .Distinct(),
            "genre.name" => context.Genres
                .Where(g => g.OwnerId == currentUser.Id)
                .Select(g => g.Name)
                .Distinct(),
            "device.name" => context.Devices
                .Where(d => d.OwnerId == currentUser.Id)
                .Select(d => d.Name)
                .Distinct(),
            "playlist.name" => context.Playlists
                .Where(p => p.OwnerId == currentUser.Id)
                .Select(p => p.Name)
                .Distinct(),
            _ => Enumerable.Empty<string>().AsQueryable(),
        };

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v.ToLower().Contains(searchLower));
        }

        var values = await query
            .OrderBy(v => v)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new FilterValuesResponse { Values = values };
    }

    [HttpPut("{id:long}", Name = "UpdateSong")]
    public async Task<UpdateSongResponse> Update(
        long id,
        [FromBody] UpdateSongRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        if (id != request.SongId)
        {
            throw new Exception("Song ID in URL does not match request body");
        }

        var update = MapToModel(request);
        var result = await _songUpdateService.UpdateSong(context, id, update, cancellationToken);

        return new UpdateSongResponse { Song = MapToDto(result) };
    }

    [HttpPut(Name = "BatchUpdateSongs")]
    public async Task<BatchUpdateSongsResponse> BatchUpdate(
        [FromBody] BatchUpdateSongsRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<BatchUpdateSongResult>();
        var update = MapPatchToModel(request.Patch);

        foreach (var songId in request.SongIds)
        {
            var result = await _songUpdateService.BatchUpdateSong(context, songId, update, cancellationToken);
            results.Add(new BatchUpdateSongResult
            {
                Id = result.Id,
                Success = result.Success,
                Error = result.Error,
                Song = result.Song != null ? MapToDto(result.Song) : null,
            });
        }

        return new BatchUpdateSongsResponse { Songs = results };
    }

    [HttpGet("autocomplete/albums", Name = "AutocompleteAlbums")]
    public async Task<AutocompleteAlbumsResponse> AutocompleteAlbums(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = context.Albums
            .Where(a => a.OwnerId == currentUser.Id)
            .Include(a => a.Artist)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        var albums = await query
            .OrderBy(a => a.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new AutocompleteAlbumsResponse
        {
            Albums = albums.Select(a => new AutocompleteAlbumItem
            {
                Id = a.Id,
                Name = a.Name,
                ArtistName = a.Artist?.Name,
            }).ToList(),
        };
    }

    [HttpGet("autocomplete/songs", Name = "AutocompleteSongs")]
    public async Task<AutocompleteSongsResponse> AutocompleteSongs(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = context.Songs
            .Where(s => s.OwnerId == currentUser.Id)
            .Include(s => s.Album)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(s =>
                s.Title.ToLower().Contains(searchLower) || s.Album.Name.ToLower().Contains(searchLower));
        }

        var songs = await query
            .OrderBy(s => s.Title)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new AutocompleteSongsResponse
        {
            Songs = songs.Select(s => new AutocompleteSongItem
            {
                Id = s.Id,
                Title = s.Title,
                AlbumName = s.Album.Name,
                CoverId = s.CoverId,
                ArtistName = s.Artists.FirstOrDefault()?.Artist?.Name,
            }).ToList(),
        };
    }

    [HttpGet("autocomplete/artists", Name = "AutocompleteArtists")]
    public async Task<AutocompleteArtistsResponse> AutocompleteArtists(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = context.Artists
            .Where(a => a.OwnerId == currentUser.Id)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        var artists = await query
            .OrderBy(a => a.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new AutocompleteArtistsResponse
        {
            Artists = artists.Select(a => new AutocompleteArtistItem
            {
                Id = a.Id,
                Name = a.Name,
            }).ToList(),
        };
    }

    [HttpGet("autocomplete/genres", Name = "AutocompleteGenres")]
    public async Task<AutocompleteGenresResponse> AutocompleteGenres(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = context.Genres
            .Where(g => g.OwnerId == currentUser.Id)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(searchLower));
        }

        var genres = await query
            .OrderBy(g => g.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new AutocompleteGenresResponse
        {
            Genres = genres.Select(g => new AutocompleteGenreItem
            {
                Id = g.Id,
                Name = g.Name,
            }).ToList(),
        };
    }

    private static List<FilterFieldMetadata> GetSongFieldMetadata() =>
    [
        new()
        {
            Name = "title",
            Type = "string",
            Description = "Song title",
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "label",
            Type = "string",
            Description = "Record label",
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "year",
            Type = "number",
            Description = "Release year",
            SupportedOperators =
                ["eq", "neq", "gt", "gte", "lt", "lte", "between", "isNull", "isNotNull", "in", "notIn"],
        },
        new()
        {
            Name = "explicit",
            Type = "boolean",
            Description = "Has explicit content",
            SupportedOperators = ["eq", "neq", "isTrue", "isFalse"],
        },
        new()
        {
            Name = "isFavorite",
            Type = "boolean",
            Description = "Is favorited",
            SupportedOperators = ["eq", "neq", "isTrue", "isFalse"],
        },
        new()
        {
            Name = "rating",
            Type = "number",
            Description = "Song rating",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between", "isNull", "isNotNull"],
        },
        new()
        {
            Name = "createdAt",
            Type = "date",
            Description = "Date added to library",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between", "isNull", "isNotNull"],
        },
        new()
        {
            Name = "addedAt",
            Type = "date",
            Description = "Date added",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between", "isNull", "isNotNull"],
        },
        new()
        {
            Name = "album.name",
            Type = "string",
            Description = "Album name",
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "album.year",
            Type = "number",
            Description = "Album release year",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between", "isNull", "isNotNull"],
        },
        new()
        {
            Name = "artist.name",
            EntityPath = "Artists.Artist.Name",
            Type = "string",
            Description = "Artist name",
            IsCollection = true,
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "genre.name",
            EntityPath = "Genres.Genre.Name",
            Type = "string",
            Description = "Genre name",
            IsCollection = true,
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "device.name",
            EntityPath = "Devices.Device.Name",
            Type = "string",
            Description = "Device name",
            IsCollection = true,
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "playlist.name",
            EntityPath = "PlaylistSongs.Playlist.Name",
            Type = "string",
            Description = "Playlist name",
            IsCollection = true,
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "searchableText",
            Type = "string",
            Description = "Combined searchable text (title + album + label)",
            IsComputed = true,
            SupportedOperators = ["contains"],
        },
        new()
        {
            Name = "durationSeconds",
            Type = "number",
            Description = "Duration in seconds",
            IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between"],
        },
        new()
        {
            Name = "durationCategory",
            Type = "string",
            Description = "Duration category (Short, Medium, Long)",
            IsComputed = true,
            SupportedOperators = ["eq", "neq", "in", "notIn"],
            Values = ["Short", "Medium", "Long"],
        },
        new()
        {
            Name = "hasLyrics",
            Type = "boolean",
            Description = "Has lyrics",
            IsComputed = true,
            SupportedOperators = ["eq", "neq", "isTrue", "isFalse"],
        },
        new()
        {
            Name = "daysSinceAdded",
            Type = "number",
            Description = "Days since song was added",
            IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between"],
        },
        new()
        {
            Name = "artistCount",
            Type = "number",
            Description = "Number of artists",
            IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
        new()
        {
            Name = "genreCount",
            Type = "number",
            Description = "Number of genres",
            IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
    ];

    private static Dictionary<string, string> GetSongFieldMappings()
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in GetSongFieldMetadata())
        {
            if (!string.IsNullOrEmpty(field.EntityPath))
            {
                mappings[field.Name] = field.EntityPath;
            }
        }

        return mappings;
    }

    private static SongUpdateModel MapToModel(UpdateSongRequest request) =>
        new()
        {
            Title = request.Title,
            AlbumId = request.AlbumId,
            AlbumName = request.AlbumName,
            AlbumArtistId = request.AlbumArtistId,
            AlbumArtistName = request.AlbumArtistName,
            ArtistIds = request.ArtistIds,
            ArtistNames = request.ArtistNames,
            GenreIds = request.GenreIds,
            GenreNames = request.GenreNames,
            Year = request.Year,
            Lyrics = request.Lyrics,
            Rating = request.Rating,
            Explicit = request.Explicit,
            Cover = request.Cover,
        };

    private static SongUpdateModel MapPatchToModel(SongPatch patch) =>
        new()
        {
            Title = patch.Title,
            AlbumId = patch.AlbumId,
            AlbumName = patch.AlbumName,
            AlbumArtistId = patch.AlbumArtistId,
            AlbumArtistName = patch.AlbumArtistName,
            ArtistIds = patch.ArtistIds,
            ArtistNames = patch.ArtistNames,
            GenreIds = patch.GenreIds,
            GenreNames = patch.GenreNames,
            Year = patch.Year,
            Lyrics = patch.Lyrics,
            Rating = patch.Rating,
            Explicit = patch.Explicit,
            Cover = patch.Cover,
        };

    private static UpdateSongItem MapToDto(SongUpdateResult result)
    {
        return new UpdateSongItem
        {
            Id = result.Id,
            Title = result.Title,
            Label = result.Label,
            Cover = result.Cover,
            Year = result.Year,
            Lyrics = result.Lyrics,
            Rating = result.Rating,
            Explicit = result.Explicit,
            RepositoryPath = result.RepositoryPath,
            Artists = result.Artists.Select(a => new UpdateSongArtist
            {
                Id = a.Id,
                Name = a.Name,
            }).ToList(),
            Album = new UpdateSongAlbum
            {
                Id = result.Album.Id,
                Name = result.Album.Name,
                Artist = result.Album.Artist != null
                    ? new UpdateSongAlbumArtist
                    {
                        Id = result.Album.Artist.Id,
                        Name = result.Album.Artist.Name,
                    }
                    : null,
            },
            Genres = result.Genres.Select(g => new UpdateSongGenre
            {
                Id = g.Id,
                Name = g.Name,
            }).ToList(),
        };
    }
}