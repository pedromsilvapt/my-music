namespace MyMusic.CLI.Api.Dtos;

public record SyncDeduplicatePrepareResponse
{
    public required int Total { get; init; }
    public required int Processed { get; init; }
    public required bool Done { get; init; }
}
