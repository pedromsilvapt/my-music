using MyMusic.Common.Services.Devices;

namespace MyMusic.Server.DTO.Devices;

public record PreviewDeviceNamingTemplateResponse
{
    public required string DefaultNamingTemplate { get; init; }
    public required List<PreviewDeviceNamingTemplateError> Errors { get; init; }
    public required int Total { get; init; }
    public required int Renamed { get; init; }
    public required List<PreviewDeviceNamingTemplateSong> Songs { get; init; }

    public static PreviewDeviceNamingTemplateResponse FromResult(DeviceNamingPreviewResult result) =>
        new()
        {
            DefaultNamingTemplate = result.DefaultNamingTemplate,
            Errors = result.Errors
                .Select(e => new PreviewDeviceNamingTemplateError
                {
                    Message = e.Message,
                    Line = e.Line,
                    Column = e.Column,
                    EndLine = e.EndLine,
                    EndColumn = e.EndColumn,
                })
                .ToList(),
            Total = result.Total,
            Renamed = result.Renamed,
            Songs = result.Songs
                .Select(s => new PreviewDeviceNamingTemplateSong
                {
                    SongDeviceId = s.SongDeviceId,
                    SongId = s.SongId,
                    Title = s.Title,
                    CurrentPath = s.CurrentPath,
                    NewPath = s.NewPath,
                    Changed = s.Changed,
                    Error = s.Error,
                })
                .ToList(),
        };
}

public record PreviewDeviceNamingTemplateError
{
    public required string Message { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required int EndLine { get; init; }
    public required int EndColumn { get; init; }
}

public record PreviewDeviceNamingTemplateSong
{
    public required long SongDeviceId { get; init; }
    public required long SongId { get; init; }
    public required string Title { get; init; }
    public required string CurrentPath { get; init; }
    public required string NewPath { get; init; }
    public required bool Changed { get; init; }
    public string? Error { get; init; }
}
