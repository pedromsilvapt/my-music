using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Sync;
using Scriban;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceNamingPreviewService"/>.
/// </summary>
public class DeviceNamingPreviewService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncPathResolver pathResolver,
    IOptions<Config> config) : IDeviceNamingPreviewService
{
    /// <inheritdoc />
    public async Task<DeviceNamingPreviewResult?> PreviewAsync(
        long ownerId,
        long? deviceId,
        string? namingTemplate,
        CancellationToken cancellationToken)
    {
        if (deviceId is { } id && await deviceLookup.FindDeviceAsync(db, id, ownerId, cancellationToken) == null)
        {
            return null;
        }

        var defaultNamingTemplate = config.Value.DefaultNamingTemplate;
        var template = string.IsNullOrWhiteSpace(namingTemplate) ? defaultNamingTemplate : namingTemplate;

        var errors = ParseErrors(template);
        if (errors.Count > 0 || deviceId == null)
        {
            return new DeviceNamingPreviewResult
            {
                DefaultNamingTemplate = defaultNamingTemplate,
                Errors = errors,
                Total = 0,
                Renamed = 0,
                Songs = [],
            };
        }

        var songs = await PreviewSongsAsync(deviceId.Value, template, cancellationToken);

        return new DeviceNamingPreviewResult
        {
            DefaultNamingTemplate = defaultNamingTemplate,
            Errors = [],
            Total = songs.Count,
            Renamed = songs.Count(s => s.Changed),
            Songs = songs,
        };
    }

    private static List<DeviceNamingPreviewError> ParseErrors(string template)
    {
        if (template.Length > DeviceValidator.NamingTemplateMaxLength)
        {
            return
            [
                new DeviceNamingPreviewError
                {
                    Message = $"Naming template cannot be longer than {DeviceValidator.NamingTemplateMaxLength} characters",
                    Line = 1,
                    Column = 1,
                    EndLine = 1,
                    EndColumn = 1,
                },
            ];
        }

        // Scriban positions are 0-based, with an inclusive end
        return Template.Parse(template).Messages
            .Where(m => m.Type == Scriban.Parsing.ParserMessageType.Error)
            .Select(m => new DeviceNamingPreviewError
            {
                Message = m.Message,
                Line = m.Span.Start.Line + 1,
                Column = m.Span.Start.Column + 1,
                EndLine = m.Span.End.Line + 1,
                EndColumn = m.Span.End.Column + 2,
            })
            .ToList();
    }

    private async Task<List<DeviceNamingPreviewSong>> PreviewSongsAsync(
        long deviceId, string template, CancellationToken cancellationToken)
    {
        // The files a sync removes are not renamed, and their paths are free for the other files
        var songDevices = await db.SongDevices
            .AsNoTracking()
            .AsSplitQuery()
            .Include(sd => sd.Song!.Album.Artist)
            .Include(sd => sd.Song!.Artists).ThenInclude(a => a.Artist)
            .Include(sd => sd.Song!.Genres).ThenInclude(g => g.Genre)
            .Where(sd => sd.DeviceId == deviceId && sd.SongId != null && sd.SyncAction != SongSyncAction.Remove)
            .OrderBy(sd => sd.DevicePath)
            .ToListAsync(cancellationToken);

        var usedPaths = new SyncUsedPaths(await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId && sd.SyncAction != SongSyncAction.Remove)
            .Select(sd => sd.DevicePath)
            .ToListAsync(cancellationToken));

        var namingStrategy = new TemplateNamingStrategy(template);
        var songs = new List<DeviceNamingPreviewSong>(songDevices.Count);

        foreach (var sd in songDevices)
        {
            var newPath = sd.DevicePath;
            string? error = null;

            try
            {
                var (path, previousPath) = usedPaths.Take(pathResolver, sd, namingStrategy);
                if (previousPath != null)
                {
                    usedPaths.Rename(previousPath, path);
                    newPath = path;
                }
            }
            catch (Exception ex) when (ex is Scriban.Syntax.ScriptRuntimeException or InvalidOperationException)
            {
                // The template parses, but fails for this song (e.g. an index out of range)
                error = ex.Message;
            }

            songs.Add(new DeviceNamingPreviewSong
            {
                SongDeviceId = sd.Id,
                SongId = sd.SongId!.Value,
                Title = sd.Song!.Title,
                CurrentPath = sd.DevicePath,
                NewPath = newPath,
                Changed = newPath != sd.DevicePath,
                Error = error,
            });
        }

        return songs;
    }
}
