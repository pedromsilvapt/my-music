using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

public class HistoryPruneCommand(
    IMyMusicClient client,
    IOptions<MyMusicOptions> options,
    ILogger<HistoryPruneCommand> logger) : AsyncCommand<HistoryPruneCommand.Settings>
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

            var sessionsResponse = await client.GetSessionsAsync(deviceId.Value, 1000);
            var sessions = sessionsResponse.Sessions;

            if (sessions.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No sync sessions to prune[/]");
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-1);
            DateTime? keepThreshold = null;

            if (!settings.All && sessions.Count > 10)
            {
                keepThreshold = sessions[9].StartedAt;
            }

            var sessionsToDelete = sessions.Where(s =>
            {
                if (s.Status == "InProgress" && s.StartedAt > DateTime.UtcNow.AddSeconds(-10))
                {
                    return false;
                }

                if (settings.All)
                {
                    return true;
                }

                var olderThanOneDay = s.StartedAt < cutoffDate;
                var olderThanThreshold = keepThreshold.HasValue && s.StartedAt < keepThreshold.Value;

                return olderThanOneDay || olderThanThreshold;
            }).ToList();

            if (sessionsToDelete.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No sessions to prune[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[bold]About to prune {sessionsToDelete.Count} session(s):[/]");
            foreach (var session in sessionsToDelete)
            {
                AnsiConsole.MarkupLine($"  - Session {session.Id}: {session.Status} ({session.StartedAt:yyyy-MM-dd HH:mm})");
            }
            AnsiConsole.WriteLine();

            if (!settings.Yes && !PromptUser($"Prune {sessionsToDelete.Count} session(s)?"))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                return 0;
            }

            var result = await client.PruneSessionsAsync(deviceId.Value, new PruneSessionsRequest { All = settings.All });

            AnsiConsole.MarkupLine($"[bold]Pruned {result.DeletedCount} session(s)[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            logger.LogError(ex, "History prune command failed");
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

    private static bool PromptUser(string message)
    {
        Console.Write($"{message} [y/N] ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response == "y";
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-a|--all")]
        [DefaultValue(false)]
        public bool All { get; set; }

        [CommandOption("-y|--yes")]
        [DefaultValue(false)]
        public bool Yes { get; set; }
    }
}