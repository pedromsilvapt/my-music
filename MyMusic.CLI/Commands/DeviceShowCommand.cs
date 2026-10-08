using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Devices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

/// <summary>
/// Shows the server device this installation syncs with, and its options. They are edited in the web app.
/// </summary>
public class DeviceShowCommand(
    IDeviceConfigService deviceConfig,
    ILogger<DeviceShowCommand> logger) : AsyncCommand<GlobalSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        using var activity = CliActivitySource.Instance.StartActivity("device show");

        try
        {
            var device = await deviceConfig.ResolveAsync();

            var grid = new Grid();
            grid.AddColumn();
            grid.AddColumn();
            grid.AddRow("Name:", device.Name.EscapeMarkup());
            grid.AddRow("ID:", device.Id.ToString());
            grid.AddRow("Icon:", device.Icon?.EscapeMarkup() ?? "[grey]default[/]");
            grid.AddRow("Color:", device.Color?.EscapeMarkup() ?? "[grey]none[/]");
            grid.AddRow("Naming template:", device.NamingTemplate?.EscapeMarkup() ?? "[grey]default[/]");
            grid.AddRow("Import on purchase:", device.ImportOnPurchase ? "yes" : "no");
            grid.AddRow("Songs:", device.SongCount.ToString());
            grid.AddRow("Last sync:", device.LastSyncAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]never[/]");

            AnsiConsole.Write(grid);
            AnsiConsole.MarkupLine("[grey]Edit these options in the web app (Devices).[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            logger.LogError(ex, "Device show command failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return 1;
        }
    }
}
