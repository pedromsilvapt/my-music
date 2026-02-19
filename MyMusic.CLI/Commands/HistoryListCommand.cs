using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

public class HistoryListCommand(
    IMyMusicClient client,
    IOptions<MyMusicOptions> options,
    ILogger<HistoryListCommand> logger) : AsyncCommand<HistoryListCommand.Settings>
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

            var response = await client.GetSessionsAsync(deviceId.Value, settings.Count);

            if (response.Sessions.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No sync sessions found[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[bold]Recent Sync Sessions[/] (device: {options.Value.Device.Name})");
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("ID");
            table.AddColumn("Started");
            table.AddColumn("Completed");
            table.AddColumn("Status");
            table.AddColumn("Dry Run");
            table.AddColumn("Created");
            table.AddColumn("Updated");
            table.AddColumn("Skipped");
            table.AddColumn("Error");

            foreach (var session in response.Sessions)
            {
                var status = session.Status switch
                {
                    "InProgress" => "[yellow]In Progress[/]",
                    "Completed" => "[green]Completed[/]",
                    "Cancelled" => "[red]Cancelled[/]",
                    _ => session.Status,
                };

                var dryRun = session.IsDryRun ? "[yellow]Yes[/]" : "[grey]No[/]";

                table.AddRow(
                    session.Id.ToString(),
                    FormatDateTime(session.StartedAt),
                    session.CompletedAt.HasValue ? FormatDateTime(session.CompletedAt.Value) : "[grey]-[/]",
                    status,
                    dryRun,
                    ColorizeCounter(session.CreatedCount, "green"),
                    ColorizeCounter(session.UpdatedCount, "teal"),
                    ColorizeCounter(session.SkippedCount, "grey"),
                    ColorizeCounter(session.ErrorCount, "red")
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Use `history show <SESSION_ID>` to view session records[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            logger.LogError(ex, "History ls command failed");
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

    private static string ColorizeCounter(int value, string color)
    {
        if (value == 0)
        {
            return $"[grey]{value}[/]";
        }

        return $"[{color}]{value}[/]";
    }

    private static string FormatDateTime(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss");

    public class Settings : CommandSettings
    {
        [CommandOption("-n|--count")]
        [DefaultValue(5)]
        public int Count { get; set; }
    }
}