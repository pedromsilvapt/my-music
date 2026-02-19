using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using Refit;
using Path = System.IO.Path;

namespace MyMusic.CLI.Services;

public class SyncService(
    IMyMusicClient client,
    IFileScanner fileScanner,
    IFileSystem fileSystem,
    IOptions<MyMusicOptions> options,
    ILogger<SyncService> logger) : ISyncService
{
    public async Task<SyncResult> SyncAsync(bool force, bool verbose, bool dryRun, bool autoConfirm,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var result = new SyncResult(0, 0, 0, 0, 0, 0);

        var deviceId = await GetOrCreateDeviceAsync(ct);
        if (deviceId is null)
        {
            logger.LogError("Failed to get or create device");
            return result with { Failed = result.Failed + 1 };
        }

        logger.LogInformation("Using device ID: {DeviceId}", deviceId);

        var repositoryPath = options.Value.Repository.Path;
        if (string.IsNullOrEmpty(repositoryPath))
        {
            logger.LogError("Repository path is not configured");
            return result with { Failed = result.Failed + 1 };
        }

        if (!fileSystem.Directory.Exists(repositoryPath))
        {
            logger.LogError("Repository path does not exist: {Path}", repositoryPath);
            return result with { Failed = result.Failed + 1 };
        }

        logger.LogInformation("Scanning repository: {Path}", repositoryPath);
        var files = await fileScanner.ScanAsync(repositoryPath, options.Value.Repository, ct);
        logger.LogInformation("Found {Count} music files", files.Count);

        var startResponse = await client.StartSyncAsync(deviceId.Value, new SyncStartRequest { DryRun = dryRun }, ct);
        var sessionId = startResponse.SessionId;
        logger.LogInformation("Started sync session: {SessionId} (DryRun: {DryRun})", sessionId, dryRun);

        var totalFiles = files.Count;
        var processedFiles = 0;
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var downloaded = 0;
        var removed = 0;
        var failed = 0;
        var uploadedPaths = new HashSet<string>();

        try
        {
            // === PHASE 1: Upload (Device -> Server) ===
            if (files.Count > 0)
            {
                var chunks = files.Chunk(options.Value.Sync.ChunkSize).ToList();
                logger.LogInformation("Processing {ChunkCount} chunks", chunks.Count);

                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var chunkNumber = i + 1;

                    logger.LogInformation("Processing chunk {ChunkNumber}/{TotalChunks}", chunkNumber, chunks.Count);

                    var syncFiles = chunk.Select(f => new SyncFileInfoItem
                            { Path = f.RelativePath, ModifiedAt = f.ModifiedAt, CreatedAt = f.CreatedAt })
                        .ToList();
                    var syncRequest = new SyncCheckRequest { Files = syncFiles, Force = force };

                    SyncCheckResponse syncResponse;
                    try
                    {
                        syncResponse = await client.CheckSyncAsync(deviceId.Value, syncRequest, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to check sync for chunk {ChunkNumber}", chunkNumber);
                        failed += chunk.Length;
                        processedFiles += chunk.Length;
                        progress?.Report(new SyncProgress(totalFiles, processedFiles, "", created, updated, skipped,
                            downloaded, removed, failed, "upload"));
                        continue;
                    }

                    var toCreatePaths = syncResponse.ToCreate.Select(f => f.Path).ToHashSet();
                    var toUpdatePaths = syncResponse.ToUpdate.Select(f => f.Path).ToHashSet();

                    logger.LogInformation(
                        "Chunk {ChunkNumber}: {ToCreate} to create, {ToUpdate} to update, {ToSkip} to skip",
                        chunkNumber, syncResponse.ToCreate.Count, syncResponse.ToUpdate.Count,
                        chunk.Length - syncResponse.ToCreate.Count - syncResponse.ToUpdate.Count);

                    var processedInChunk = 0;
                    var recordItems = new List<SyncRecordRequestItem>();

                    foreach (var fileToCreate in syncResponse.ToCreate)
                    {
                        if (!dryRun)
                        {
                            try
                            {
                                await UploadFileAsync(deviceId.Value, repositoryPath, fileToCreate, ct);
                                created++;
                                recordItems.Add(new SyncRecordRequestItem
                                {
                                    FilePath = fileToCreate.Path, Action = "Created", Source = "Device",
                                    Reason = fileToCreate.Reason,
                                });
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to create file: {Path}", fileToCreate.Path);
                                failed++;
                                recordItems.Add(new SyncRecordRequestItem
                                {
                                    FilePath = fileToCreate.Path, Action = "Error",
                                    ErrorMessage = ExtractErrorMessage(ex),
                                    Source = "Device", Reason = fileToCreate.Reason,
                                });
                            }
                        }
                        else
                        {
                            created++;
                            recordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = fileToCreate.Path, Action = "Created", Source = "Device",
                                Reason = fileToCreate.Reason,
                            });
                        }

                        uploadedPaths.Add(fileToCreate.Path);
                        processedInChunk++;
                        progress?.Report(new SyncProgress(totalFiles, processedFiles + processedInChunk,
                            fileToCreate.Path, created, updated, skipped, downloaded, removed, failed, "upload"));
                    }

                    foreach (var fileToUpdate in syncResponse.ToUpdate)
                    {
                        if (!dryRun)
                        {
                            try
                            {
                                await UploadFileAsync(deviceId.Value, repositoryPath, fileToUpdate, ct);
                                updated++;
                                recordItems.Add(new SyncRecordRequestItem
                                {
                                    FilePath = fileToUpdate.Path, Action = "Updated", Source = "Device",
                                    Reason = fileToUpdate.Reason,
                                });
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to update file: {Path}", fileToUpdate.Path);
                                failed++;
                                recordItems.Add(new SyncRecordRequestItem
                                {
                                    FilePath = fileToUpdate.Path, Action = "Error",
                                    ErrorMessage = ExtractErrorMessage(ex),
                                    Source = "Device", Reason = fileToUpdate.Reason,
                                });
                            }
                        }
                        else
                        {
                            updated++;
                            recordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = fileToUpdate.Path, Action = "Updated", Source = "Device",
                                Reason = fileToUpdate.Reason,
                            });
                        }

                        uploadedPaths.Add(fileToUpdate.Path);
                        processedInChunk++;
                        progress?.Report(new SyncProgress(totalFiles, processedFiles + processedInChunk,
                            fileToUpdate.Path, created, updated, skipped, downloaded, removed, failed, "upload"));
                    }

                    skipped += chunk.Length - syncResponse.ToCreate.Count - syncResponse.ToUpdate.Count;

                    foreach (var file in chunk)
                    {
                        if (!toCreatePaths.Contains(file.RelativePath) && !toUpdatePaths.Contains(file.RelativePath))
                        {
                            recordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = file.RelativePath, Action = "Skipped", Source = "Device",
                                Reason = $"File unchanged (modified at {file.ModifiedAt:O})",
                            });
                        }
                    }

                    processedFiles += chunk.Length;
                    progress?.Report(new SyncProgress(totalFiles, processedFiles, "", created, updated, skipped,
                        downloaded, removed, failed, "upload"));

                    var recordsRequest = new SyncRecordsRequest { Records = recordItems };
                    await client.RecordChunkAsync(deviceId.Value, sessionId, recordsRequest, ct);
                }
            }

            // === PHASE 2: Server Actions (Server -> Device) ===
            logger.LogInformation("Fetching pending actions from server");
            var pendingActions = await client.GetPendingActionsAsync(deviceId.Value, ct);
            logger.LogInformation("Found {Count} pending actions", pendingActions.Actions.Count);

            var serverTotal = pendingActions.Actions.Count;
            var serverProcessed = 0;
            var serverRecordItems = new List<SyncRecordRequestItem>();

            foreach (var action in pendingActions.Actions)
            {
                var fullPath = Path.Combine(repositoryPath, action.Path);
                serverProcessed++;

                // Skip files we just uploaded - they're already current
                if (uploadedPaths.Contains(action.Path))
                {
                    if (!dryRun)
                    {
                        await client.AcknowledgeActionAsync(deviceId.Value,
                            new AcknowledgeActionRequest { SongId = action.SongId }, ct);
                    }

                    progress?.Report(new SyncProgress(serverTotal, serverProcessed, action.Path, created, updated,
                        skipped, downloaded, removed, failed, "server"));
                    continue;
                }

                if (action.Action == "Download")
                {
                    var fileExists = fileSystem.File.Exists(fullPath);

                    // Prompt for confirmation only if not dry-run, file exists, and not auto-confirm
                    if (!dryRun && fileExists && !autoConfirm)
                    {
                        if (!PromptUser($"Replace '{action.Path}'?"))
                        {
                            progress?.Report(new SyncProgress(serverTotal, serverProcessed, action.Path, created,
                                updated, skipped, downloaded, removed, failed, "server"));
                            continue; // Leave pending
                        }
                    }

                    if (!dryRun)
                    {
                        var tempPath = fullPath + ".tmp";
                        try
                        {
                            EnsureDirectoryExists(fullPath);
                            await DownloadAndWriteFileAsync(action.SongId, tempPath, ct);

                            if (fileExists)
                            {
                                fileSystem.File.Delete(fullPath);
                            }

                            fileSystem.File.Move(tempPath, fullPath);

                            await client.AcknowledgeActionAsync(deviceId.Value,
                                new AcknowledgeActionRequest { SongId = action.SongId }, ct);
                            downloaded++;
                            serverRecordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = action.Path, Action = "Downloaded", SongId = action.SongId,
                                Source = "Server", Reason = "Server-initiated download",
                            });
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to download file: {Path}", action.Path);
                            failed++;
                            serverRecordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = action.Path, Action = "Error", SongId = action.SongId,
                                ErrorMessage = ex.Message, Source = "Server",
                                Reason = "Server-initiated download failed",
                            });
                        }
                        finally
                        {
                            if (fileSystem.File.Exists(tempPath))
                            {
                                fileSystem.File.Delete(tempPath);
                            }
                        }
                    }
                    else
                    {
                        downloaded++;
                        serverRecordItems.Add(new SyncRecordRequestItem
                        {
                            FilePath = action.Path, Action = "Downloaded", SongId = action.SongId, Source = "Server",
                            Reason = "Server-initiated download",
                        });
                    }
                }
                else if (action.Action == "Remove")
                {
                    var fileExists = fileSystem.File.Exists(fullPath);

                    if (!fileExists)
                    {
                        if (!dryRun)
                        {
                            await client.AcknowledgeActionAsync(deviceId.Value,
                                new AcknowledgeActionRequest { SongId = action.SongId }, ct);
                        }

                        progress?.Report(new SyncProgress(serverTotal, serverProcessed, action.Path, created, updated,
                            skipped, downloaded, removed, failed, "server"));
                        continue; // Already gone
                    }

                    // Prompt for confirmation only if not dry-run and not auto-confirm
                    if (!dryRun && !autoConfirm)
                    {
                        if (!PromptUser($"Delete '{action.Path}'?"))
                        {
                            progress?.Report(new SyncProgress(serverTotal, serverProcessed, action.Path, created,
                                updated, skipped, downloaded, removed, failed, "server"));
                            continue; // Leave pending
                        }
                    }

                    if (!dryRun)
                    {
                        try
                        {
                            fileSystem.File.Delete(fullPath);
                            await client.AcknowledgeActionAsync(deviceId.Value,
                                new AcknowledgeActionRequest { SongId = action.SongId }, ct);
                            removed++;
                            serverRecordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = action.Path, Action = "Removed", SongId = action.SongId, Source = "Server",
                                Reason = "Server-initiated removal",
                            });
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to delete file: {Path}", action.Path);
                            failed++;
                            serverRecordItems.Add(new SyncRecordRequestItem
                            {
                                FilePath = action.Path, Action = "Error", SongId = action.SongId,
                                ErrorMessage = ex.Message, Source = "Server",
                                Reason = "Server-initiated removal failed",
                            });
                        }
                    }
                    else
                    {
                        removed++;
                        serverRecordItems.Add(new SyncRecordRequestItem
                        {
                            FilePath = action.Path, Action = "Removed", SongId = action.SongId, Source = "Server",
                            Reason = "Server-initiated removal",
                        });
                    }
                }

                progress?.Report(new SyncProgress(serverTotal, serverProcessed, action.Path, created, updated, skipped,
                    downloaded, removed, failed, "server"));
            }

            // Record server actions
            if (serverRecordItems.Count > 0)
            {
                var serverRecordsRequest = new SyncRecordsRequest { Records = serverRecordItems };
                await client.RecordChunkAsync(deviceId.Value, sessionId, serverRecordsRequest, ct);
            }

            // === COMPLETE ===
            var completeResponse = await client.CompleteSyncAsync(deviceId.Value, sessionId, ct);

            logger.LogInformation(
                "Sync complete: {Created} created, {Updated} updated, {Skipped} skipped, {Downloaded} downloaded, {Removed} removed, {Error} error",
                completeResponse.CreatedCount, completeResponse.UpdatedCount, completeResponse.SkippedCount,
                completeResponse.DownloadedCount, completeResponse.RemovedCount, completeResponse.ErrorCount);

            return new SyncResult(
                completeResponse.CreatedCount,
                completeResponse.UpdatedCount,
                completeResponse.SkippedCount,
                completeResponse.DownloadedCount,
                completeResponse.RemovedCount,
                completeResponse.ErrorCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed with exception");
            return new SyncResult(created, updated, skipped, downloaded, removed, failed + 1);
        }
    }

    private bool PromptUser(string message)
    {
        Console.Write($"{message} [y/N] ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response == "y" || response == "yes";
    }

    private void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
        {
            fileSystem.Directory.CreateDirectory(directory);
        }
    }

    private async Task DownloadAndWriteFileAsync(long songId, string destinationPath, CancellationToken ct)
    {
        await using var stream = await client.DownloadSongAsync(songId, ct);
        await using var fileStream = fileSystem.FileStream.New(destinationPath, FileMode.Create);
        await stream.CopyToAsync(fileStream, ct);
    }

    private async Task<long?> GetOrCreateDeviceAsync(CancellationToken ct)
    {
        try
        {
            var devicesResponse = await client.GetDevicesAsync(ct);
            var existingDevice = devicesResponse.Devices.FirstOrDefault(d => d.Name == options.Value.Device.Name);

            if (existingDevice is not null)
            {
                logger.LogInformation("Found existing device: {DeviceName} (ID: {DeviceId})",
                    existingDevice.Name, existingDevice.Id);

                var needsUpdate = existingDevice.Icon != options.Value.Device.Icon ||
                                  existingDevice.Color != options.Value.Device.Color ||
                                  existingDevice.NamingTemplate != options.Value.Device.NamingTemplate;

                if (needsUpdate)
                {
                    logger.LogInformation("Updating device properties for: {DeviceName}", existingDevice.Name);
                    await client.UpdateDeviceAsync(existingDevice.Id, new UpdateDeviceRequest
                    {
                        Icon = options.Value.Device.Icon,
                        Color = options.Value.Device.Color,
                        NamingTemplate = options.Value.Device.NamingTemplate,
                    }, ct);
                }

                return existingDevice.Id;
            }

            logger.LogInformation("Creating new device: {DeviceName}", options.Value.Device.Name);
            var newDevice = await client.CreateDeviceAsync(new CreateDeviceRequest
            {
                Name = options.Value.Device.Name,
                Icon = options.Value.Device.Icon,
                Color = options.Value.Device.Color,
                NamingTemplate = options.Value.Device.NamingTemplate,
            }, ct);
            logger.LogInformation("Created device with ID: {DeviceId}", newDevice.Device.Id);
            return newDevice.Device.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get or create device");
            return null;
        }
    }

    private async Task UploadFileAsync(long deviceId, string repositoryPath, SyncFileInfoItem fileInfo,
        CancellationToken ct)
    {
        var fullPath = Path.Combine(repositoryPath, fileInfo.Path);
        if (!fileSystem.File.Exists(fullPath))
        {
            logger.LogWarning("File not found: {Path}", fullPath);
            return;
        }

        await using var stream = fileSystem.File.OpenRead(fullPath);
        var streamPart = new StreamPart(stream, Path.GetFileName(fullPath));

        await client.UploadFileAsync(
            deviceId,
            streamPart,
            fileInfo.Path,
            fileInfo.ModifiedAt.ToString("O"),
            fileInfo.CreatedAt.ToString("O"),
            ct);
    }

    private static string ExtractErrorMessage(Exception ex)
    {
        if (ex is not ApiException apiEx || string.IsNullOrEmpty(apiEx.Content))
        {
            return ex.Message;
        }

        try
        {
            var problemDetails = JsonSerializer.Deserialize<ProblemDetailsResponse>(apiEx.Content);
            if (!string.IsNullOrEmpty(problemDetails?.Detail))
            {
                return problemDetails.Detail;
            }
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch { }

        return apiEx.Content;
    }

    private record ProblemDetailsResponse([property: JsonPropertyName("detail")] string? Detail);
}