using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Sync.Types;
using Spectre.Console;
using Spectre.Console.Cli;
using MyMusic.CLI;

namespace MyMusic.CLI.Commands;

public class HistoryShowCommand(
    IMyMusicClient client,
    IOptions<MyMusicOptions> options,
    ILogger<HistoryShowCommand> logger) : AsyncCommand<HistoryShowCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var activity = CliActivitySource.Instance.StartActivity("history show");
        
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

        AnsiConsole.MarkupLine($"Summary: {ColorizeCounter(session?.CreateRemoteCount ?? 0, "green")} created (remote), " +
                               $"{ColorizeCounter(session?.UpdateRemoteCount ?? 0, "teal")} updated (remote), " +
                               $"{ColorizeCounter(session?.SkippedCount ?? 0, "grey")} skipped, " +
                               $"{ColorizeCounter(session?.CreateLocalCount ?? 0, "blue")} downloaded, " +
                               $"{ColorizeCounter(session?.UpdateLocalCount ?? 0, "blue")} updated (local), " +
                               $"{ColorizeCounter(session?.DeleteCount ?? 0, "red")} deleted, " +
                               $"{ColorizeCounter(session?.LinkCount ?? 0, "green")} linked, " +
                               $"{ColorizeCounter(session?.UnlinkCount ?? 0, "red")} unlinked, " +
                               $"{ColorizeCounter(session?.RenameCount ?? 0, "teal")} renamed, " +
                               $"{ColorizeCounter(session?.ConflictCount ?? 0, "yellow")} conflicts, " +
                               $"{ColorizeCounter(session?.UpdateTimestampCount ?? 0, "grey")} timestamp updates, " +
                               $"{ColorizeCounter(session?.ErrorCount ?? 0, "red")} error");
        AnsiConsole.WriteLine();

        var requestedActions = GetRequestedActions(settings);
        var sourceFilter = GetSourceFilter(settings);
        var actions = GetActionsFilter(requestedActions);
        var response = await client.GetSessionRecordsAsync(deviceId, sessionId, actions, sourceFilter);

        var grouped = response.Records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.ToList());

        if (requestedActions.Contains(SyncRecordAction.CreateRemote))
        {
            PrintRecordGroup(SyncRecordAction.CreateRemote, grouped, "green");
        }

        if (requestedActions.Contains(SyncRecordAction.UpdateRemote))
        {
            PrintRecordGroup(SyncRecordAction.UpdateRemote, grouped, "teal");
        }

        if (requestedActions.Contains(SyncRecordAction.CreateLocal))
        {
            PrintRecordGroup(SyncRecordAction.CreateLocal, grouped, "blue");
        }

        if (requestedActions.Contains(SyncRecordAction.UpdateLocal))
        {
            PrintRecordGroup(SyncRecordAction.UpdateLocal, grouped, "blue");
        }

        if (requestedActions.Contains(SyncRecordAction.Delete))
        {
            PrintRecordGroup(SyncRecordAction.Delete, grouped, "red");
        }

        if (requestedActions.Contains(SyncRecordAction.Link))
        {
            PrintRecordGroup(SyncRecordAction.Link, grouped, "green");
        }

        if (requestedActions.Contains(SyncRecordAction.Unlink))
        {
            PrintRecordGroup(SyncRecordAction.Unlink, grouped, "red");
        }

        if (requestedActions.Contains(SyncRecordAction.Rename))
        {
            PrintRecordGroup(SyncRecordAction.Rename, grouped, "teal");
        }

        if (requestedActions.Contains(SyncRecordAction.Skipped))
        {
            PrintRecordGroup(SyncRecordAction.Skipped, grouped, "grey");
        }

        if (requestedActions.Contains(SyncRecordAction.Conflict))
        {
            PrintRecordGroup(SyncRecordAction.Conflict, grouped, "yellow");
        }

        if (requestedActions.Contains(SyncRecordAction.UpdateTimestamp))
        {
            PrintRecordGroup(SyncRecordAction.UpdateTimestamp, grouped, "grey");
        }

        if (requestedActions.Contains(SyncRecordAction.Error))
        {
            PrintRecordGroup(SyncRecordAction.Error, grouped, "red");
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

    private static string? GetActionsFilter(HashSet<SyncRecordAction> requestedActions)
    {
        if (requestedActions.Count == 12)
        {
            return null;
        }

        return string.Join(",", requestedActions.Select(a => a.ToString()));
    }

    private static readonly Dictionary<string, SyncRecordAction> CounterAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cr"] = SyncRecordAction.CreateRemote,
        ["ur"] = SyncRecordAction.UpdateRemote,
        ["cl"] = SyncRecordAction.CreateLocal,
        ["ul"] = SyncRecordAction.UpdateLocal,
        ["del"] = SyncRecordAction.Delete,
        ["link"] = SyncRecordAction.Link,
        ["unlink"] = SyncRecordAction.Unlink,
        ["ren"] = SyncRecordAction.Rename,
        ["skip"] = SyncRecordAction.Skipped,
        ["conf"] = SyncRecordAction.Conflict,
        ["ts"] = SyncRecordAction.UpdateTimestamp,
        ["err"] = SyncRecordAction.Error,
    };

    private static HashSet<SyncRecordAction> GetRequestedActions(Settings settings)
    {
        if (settings.ShowAll)
        {
            return Enum.GetValues<SyncRecordAction>().ToHashSet();
        }

        var actions = new HashSet<SyncRecordAction>();

        foreach (var counter in settings.Counters)
        {
            if (CounterAbbreviations.TryGetValue(counter, out var action))
            {
                actions.Add(action);
            }
            else if (Enum.TryParse<SyncRecordAction>(counter, ignoreCase: true, out var parsed))
            {
                actions.Add(parsed);
            }
        }

        if (actions.Count == 0)
        {
            actions.Add(SyncRecordAction.Error);
        }

        return actions;
    }

    private static void PrintRecordGroup(SyncRecordAction action, Dictionary<SyncRecordAction, List<SyncRecordResponseItem>> grouped,
        string color)
    {
        var actionName = action.ToString();
        var hasRecords = grouped.TryGetValue(action, out var records);
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

                if (!string.IsNullOrEmpty(record.Reason))
                {
                    var reasonStyle = record.Action == SyncRecordAction.Error ? "red" : "dim";
                    parts.Add($"[{reasonStyle}]- {record.Reason.EscapeMarkup()}[/]");
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

    public class Settings : GlobalSettings
    {
        [CommandArgument(0, "[SESSION_ID]")] public long? SessionId { get; set; }

        [CommandOption("-c|--counters")]
        [Description("Counter filters: cr=CreateRemote, ur=UpdateRemote, cl=CreateLocal, ul=UpdateLocal, del=Delete, link=Link, unlink=Unlink, ren=Rename, skip=Skipped, conf=Conflict, ts=UpdateTimestamp, err=Error. Comma-separated or repeat -c. Also accepts full names.")]
        public string[] Counters { get; set; } = [];

        [CommandOption("-a|--all")] public bool ShowAll { get; set; }

        [CommandOption("--device")] public bool FilterDevice { get; set; }

        [CommandOption("--server")] public bool FilterServer { get; set; }
    }
}