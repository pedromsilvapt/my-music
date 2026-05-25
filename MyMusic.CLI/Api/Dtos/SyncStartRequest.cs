namespace MyMusic.CLI.Api.Dtos;

public record SyncStartRequest
{
    public bool DryRun { get; init; }
    public string? RepositoryPath { get; init; }
    public List<SyncScanErrorItem>? ScanErrors { get; init; }
}

public record SyncScanErrorItem
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
}