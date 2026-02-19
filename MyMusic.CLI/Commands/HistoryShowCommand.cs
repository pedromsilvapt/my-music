using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

public class HistoryShowCommand(
    IMyMusicClient client,
    IOptions<MyMusicOptions> options,
    ILogger<HistoryShowCommand> logger) : AsyncCommand<HistoryShowCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var deviceId = await GetDeviceIdAsync();
            if (deviceId is null)
            {
                AnsiConsole.MarkupLine("[red]Error: Could not find device[/]");
                return 1;
            }

            var sessionId = settings.SessionId ?? await GetMostRecentSessionIdAsync(deviceId.Value);
            if (sessionId is null)
            {
                AnsiConsole.MarkupLine("[yellow]No sync sessions found[/]");
                return 0;
            }

            return await ShowSessionRecordsAsync(deviceId.Value, sessionId.Value, settings);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            logger.LogError(ex, "History command failed");
            return 1;
        }
    }

    private async Task<long?> GetDeviceIdAsync()
    {
        try
        {
            var devicesResponse = await client.GetDevicesAsync();
            var existingDevice = devicesResponse.Devices.FirstOrDefault(d => d.Name == options.Value.Device.Name);
            return existingDevice?.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get device");
            return null;
        }
    }

    private async Task<long?> GetMostRecentSessionIdAsync(long deviceId)
    {
        var response = await client.GetSessionsAsync(deviceId, 1);
        return response.Sessions.FirstOrDefault()?.Id;
    }

    private async Task<int> ShowSessionRecordsAsync(long deviceId, long sessionId, Settings settings)
    {
        var session = await GetSessionInfoAsync(deviceId, sessionId);

        AnsiConsole.MarkupLine($"[bold]Session {sessionId} - Records[/]");
        if (session is not null)
        {
            AnsiConsole.MarkupLine($"[grey]Started: {FormatDateTime(session.StartedAt)}[/]");
            if (session.CompletedAt.HasValue)
            {
                AnsiConsole.MarkupLine($"[grey]Completed: {FormatDateTime(session.CompletedAt.Value)}[/]");
            }

            AnsiConsole.MarkupLine($"[grey]Status: {session.Status}[/]");
        }

        AnsiConsole.WriteLine();

        var createdCount = session?.CreatedCount ?? 0;
        var updatedCount = session?.UpdatedCount ?? 0;
        var skippedCount = session?.SkippedCount ?? 0;
        var downloadedCount = session?.DownloadedCount ?? 0;
        var removedCount = session?.RemovedCount ?? 0;
        var errorCount = session?.ErrorCount ?? 0;

        AnsiConsole.MarkupLine($"Summary: {ColorizeCounter(createdCount, "green")} created, " +
                               $"{ColorizeCounter(updatedCount, "teal")} updated, " +
                               $"{ColorizeCounter(skippedCount, "grey")} skipped, " +
                               $"{ColorizeCounter(downloadedCount, "blue")} downloaded, " +
                               $"{ColorizeCounter(removedCount, "red")} removed, " +
                               $"{ColorizeCounter(errorCount, "red")} error");
        AnsiConsole.WriteLine();

        var requestedActions = GetRequestedActions(settings);
        var sourceFilter = GetSourceFilter(settings);
        var actions = GetActionsFilter(requestedActions);
        var response = await client.GetSessionRecordsAsync(deviceId, sessionId, actions, sourceFilter);

        var grouped = response.Records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.ToList());

        if (requestedActions.Contains(RecordAction.Created))
        {
            PrintRecordGroup(RecordAction.Created, grouped, "green");
        }

        if (requestedActions.Contains(RecordAction.Updated))
        {
            PrintRecordGroup(RecordAction.Updated, grouped, "teal");
        }

        if (requestedActions.Contains(RecordAction.Skipped))
        {
            PrintRecordGroup(RecordAction.Skipped, grouped, "grey");
        }

        if (requestedActions.Contains(RecordAction.Downloaded))
        {
            PrintRecordGroup(RecordAction.Downloaded, grouped, "blue");
        }

        if (requestedActions.Contains(RecordAction.Removed))
        {
            PrintRecordGroup(RecordAction.Removed, grouped, "red");
        }

        if (requestedActions.Contains(RecordAction.Error))
        {
            PrintRecordGroup(RecordAction.Error, grouped, "red");
        }

        return 0;
    }

    private async Task<SyncSessionItem?> GetSessionInfoAsync(long deviceId, long sessionId)
    {
        var response = await client.GetSessionsAsync(deviceId, 100);
        return response.Sessions.FirstOrDefault(s => s.Id == sessionId);
    }

    private static string? GetSourceFilter(Settings settings)
    {
        if (settings.FilterDevice && !settings.FilterServer)
        {
            return "Device";
        }

        if (settings.FilterServer && !settings.FilterDevice)
        {
            return "Server";
        }

        return null;
    }

    private static string? GetActionsFilter(HashSet<RecordAction> requestedActions)
    {
        if (requestedActions.Count == 6)
        {
            return null;
        }

        return string.Join(",", requestedActions.Select(a => a.ToString()));
    }

    private static HashSet<RecordAction> GetRequestedActions(Settings settings)
    {
        if (settings.ShowAll)
        {
            return new HashSet<RecordAction>
            {
                RecordAction.Created, RecordAction.Updated, RecordAction.Skipped, RecordAction.Downloaded,
                RecordAction.Removed, RecordAction.Error,
            };
        }

        var actions = new HashSet<RecordAction>();

        if (settings.ShowCreated)
        {
            actions.Add(RecordAction.Created);
        }

        if (settings.ShowUpdated)
        {
            actions.Add(RecordAction.Updated);
        }

        if (settings.ShowSkipped)
        {
            actions.Add(RecordAction.Skipped);
        }

        if (settings.ShowDownloaded)
        {
            actions.Add(RecordAction.Downloaded);
        }

        if (settings.ShowRemoved)
        {
            actions.Add(RecordAction.Removed);
        }

        if (settings.ShowError)
        {
            actions.Add(RecordAction.Error);
        }

        if (actions.Count == 0)
        {
            actions.Add(RecordAction.Error);
        }

        return actions;
    }

    private static void PrintRecordGroup(RecordAction action, Dictionary<string, List<SyncRecordResponseItem>> grouped,
        string color)
    {
        var actionName = action.ToString();
        var hasRecords = grouped.TryGetValue(actionName, out var records);
        var count = hasRecords ? records!.Count : 0;

        AnsiConsole.MarkupLine($"[{color}]{actionName} ({count}):[/]");

        if (!hasRecords || records!.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey](none)[/]");
        }
        else
        {
            foreach (var record in records!)
            {
                var sourceLabel = record.Source == "Server" ? "[blue]↓[/]" : "[green]↑[/]";
                var parts = new List<string> { $"  {sourceLabel} [grey]{record.FilePath.EscapeMarkup()}[/]" };

                if (!string.IsNullOrEmpty(record.ErrorMessage))
                {
                    parts.Add($"[red]- {record.ErrorMessage.EscapeMarkup()}[/]");
                }
                else if (!string.IsNullOrEmpty(record.Reason))
                {
                    parts.Add($"[dim]- {record.Reason.EscapeMarkup()}[/]");
                }

                AnsiConsole.MarkupLine(string.Join(" ", parts));
            }
        }

        AnsiConsole.WriteLine();
    }

    private static string ColorizeCounter(int value, string color)
    {
        if (value == 0)
        {
            return $"[grey]{value}[/]";
        }

        return $"[{color}]{value}[/]";
    }

    private static string FormatDateTime(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss");

    private enum RecordAction
    {
        Created,
        Updated,
        Skipped,
        Downloaded,
        Removed,
        Error,
    }

    private enum RecordSource
    {
        Device,
        Server,
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[SESSION_ID]")] public long? SessionId { get; set; }

        [CommandOption("-c|--created")] public bool ShowCreated { get; set; }

        [CommandOption("-u|--updated")] public bool ShowUpdated { get; set; }

        [CommandOption("-s|--skipped")] public bool ShowSkipped { get; set; }

        [CommandOption("-d|--downloaded")] public bool ShowDownloaded { get; set; }

        [CommandOption("-r|--removed")] public bool ShowRemoved { get; set; }

        [CommandOption("-e|--error")] public bool ShowError { get; set; }

        [CommandOption("-a|--all")] public bool ShowAll { get; set; }

        [CommandOption("--device")] public bool FilterDevice { get; set; }

        [CommandOption("--server")] public bool FilterServer { get; set; }
    }
}