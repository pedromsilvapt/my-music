using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

public class HistoryRemoveCommand(
    IMyMusicClient client,
    IOptions<MyMusicOptions> options,
    ILogger<HistoryRemoveCommand> logger) : AsyncCommand<HistoryRemoveCommand.Settings>
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

            if (settings.SessionIds.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Error: No session IDs provided[/]");
                return 1;
            }

            var sessionsResponse = await client.GetSessionsAsync(deviceId.Value, 1000);
            var validIds = new HashSet<long>(sessionsResponse.Sessions.Select(s => s.Id));
            var invalidIds = settings.SessionIds.Where(id => !validIds.Contains(id)).ToList();

            if (invalidIds.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: The following session IDs do not exist: {string.Join(", ", invalidIds)}[/]");
            }

            var idsToDelete = settings.SessionIds.Where(id => validIds.Contains(id)).ToList();
            if (idsToDelete.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No valid sessions to delete[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[bold]About to delete {idsToDelete.Count} session(s):[/]");
            foreach (var id in idsToDelete)
            {
                var session = sessionsResponse.Sessions.First(s => s.Id == id);
                AnsiConsole.MarkupLine($"  - Session {id}: {session.Status} ({session.StartedAt:yyyy-MM-dd HH:mm})");
            }
            AnsiConsole.WriteLine();

            if (!settings.Yes && !PromptUser($"Delete {idsToDelete.Count} session(s)?"))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                return 0;
            }

            var deletedCount = 0;
            foreach (var id in idsToDelete)
            {
                try
                {
                    await client.DeleteSessionAsync(deviceId.Value, id);
                    deletedCount++;
                    AnsiConsole.MarkupLine($"[green]Deleted session {id}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to delete session {id}: {ex.Message}[/]");
                    logger.LogError(ex, "Failed to delete session {SessionId}", id);
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Deleted {deletedCount} session(s)[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            logger.LogError(ex, "History rm command failed");
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
        [CommandArgument(0, "<ID>")]
        public List<long> SessionIds { get; set; } = [];

        [CommandOption("-y|--yes")]
        [DefaultValue(false)]
        public bool Yes { get; set; }
    }
}