using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Dates;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

/// <summary>
/// Gives the files already synced to the device the date set in <c>MyMusic:Sync:FileModifiedAt</c>, which a
/// sync only gives to the files it downloads.
/// </summary>
public class RepairDatesCommand(
    IRepairDatesService repairDates,
    ILogger<RepairDatesCommand> logger) : AsyncCommand<RepairDatesCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var activity = CliActivitySource.Instance.StartActivity("repair-dates");

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
        }

        try
        {
            var result = await repairDates.RepairAsync(settings.DryRun);

            AnsiConsole.WriteLine(BuildSummary(result));
            if (result.Failed > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]{result.Failed} could not be re-dated (see the log)[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            logger.LogError(ex, "Repair dates command failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return 1;
        }
    }

    internal static string BuildSummary(RepairDatesResult result)
    {
        var summary = $"{result.Redated} re-dated, {result.Skipped} skipped";

        (int Count, string Label)[] reasons =
        [
            (result.UpToDate, "already dated"),
            (result.EditedLocally, "edited locally"),
            (result.ChangedOnServer, "changed on server"),
            (result.Missing, "missing"),
            (result.Excluded, "excluded"),
            (result.NotSynced, "not synced"),
        ];
        var details = reasons.Where(r => r.Count > 0).Select(r => $"{r.Count} {r.Label}").ToList();

        return details.Count > 0 ? $"{summary} ({string.Join(", ", details)})" : summary;
    }

    public class Settings : GlobalSettings
    {
        [CommandOption("--dry-run")]
        [Description("Show how many files would be re-dated without changing any")]
        public bool DryRun { get; set; }
    }
}
