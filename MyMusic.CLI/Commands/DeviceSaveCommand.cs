using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Devices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

/// <summary>
/// Saves the configured device options (icon, color, naming template, import on purchase) to the
/// server, without running a sync.
/// </summary>
public class DeviceSaveCommand(
    IDeviceConfigService deviceConfig,
    IOptions<MyMusicOptions> options,
    ILogger<DeviceSaveCommand> logger) : AsyncCommand<GlobalSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings)
    {
        using var activity = CliActivitySource.Instance.StartActivity("device save");

        try
        {
            var result = await deviceConfig.ResolveAsync(saveOptions: true);
            var name = options.Value.Device.Name.EscapeMarkup();

            AnsiConsole.MarkupLine(result.Outcome switch
            {
                DeviceConfigOutcome.Created => $"[green]Registered device '{name}' (ID: {result.DeviceId})[/]",
                DeviceConfigOutcome.Updated => $"[green]Saved options of device '{name}' (ID: {result.DeviceId})[/]",
                _ => $"[grey]Device '{name}' (ID: {result.DeviceId}) is already up to date[/]",
            });
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            logger.LogError(ex, "Device save command failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return 1;
        }
    }
}
