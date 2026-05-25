namespace MyMusic.CLI.Services.Sync;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync.Types;

public class Orchestrator(
    ISyncConfig config,
    IFileOps fileOps,
    IKeepAwake keepAwake,
    Phases phases,
    ILogger<Orchestrator> logger)
{
    public async Task<SyncResult> OrchestrateSyncAsync(
        SyncOptions options,
        IProgress<SyncProgress>? progress,
        CancellationToken ct = default)
    {
        var deviceId = await config.GetDeviceIdAsync(ct);
        if (deviceId is null)
        {
            logger.LogError("Failed to get or create device");
            return new SyncResult { Error = 1 };
        }

        logger.LogInformation("Using device ID: {DeviceId}", deviceId);

        var repositoryPath = config.GetRepositoryPath();
        if (string.IsNullOrEmpty(repositoryPath))
        {
            logger.LogError("Repository path is not configured");
            return new SyncResult { Error = 1 };
        }

        if (!fileOps.DirectoryExists(repositoryPath))
        {
            logger.LogError("Repository path does not exist: {Path}", repositoryPath);
            return new SyncResult { Error = 1 };
        }

        var ctx = new SyncContext
        {
            DeviceId = deviceId.Value,
            RepositoryPath = repositoryPath,
            Options = options
        };

        try
        {
            await keepAwake.ActivateAsync(ct);

            var scanResult = await phases.ScanPhaseAsync(ctx, progress, ct);

            await phases.StartSessionAsync(ctx, scanResult.Errors, ct);

            await phases.UploadPhaseAsync(ctx, scanResult.Files, progress, ct);

            await phases.ServerActionsPhaseAsync(ctx, progress, ct);

            await phases.CommitPhaseAsync(ctx, progress, ct);

            await phases.CompleteAsync(ctx, scanResult.Files.Count, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Sync cancelled by user");
            return ctx.Result with { Error = ctx.Result.Error + 1, Cancelled = true, SessionId = ctx.SessionId };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed with exception");
            return ctx.Result with { Error = ctx.Result.Error + 1, SessionId = ctx.SessionId };
        }
        finally
        {
            keepAwake.Deactivate();
        }

        return ctx.Result with { SessionId = ctx.SessionId };
    }
}
