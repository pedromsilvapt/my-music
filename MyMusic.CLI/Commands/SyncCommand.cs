using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services;
using MyMusic.Common.Services.Sync.Types;
using Spectre.Console;
using Spectre.Console.Cli;
using SyncDirection = MyMusic.Common.Services.Sync.Types.SyncDirection;

namespace MyMusic.CLI.Commands;

public class SyncCommand(ISyncService syncService, ILogger<SyncCommand> logger) : AsyncCommand<SyncCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold cyan]MyMusic Sync[/]");
        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run mode - no changes will be made[/]");
        }

        AnsiConsole.WriteLine();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            SyncResult syncResult = new();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new SpinnerColumn(), new TaskDescriptionColumn(), new ProgressBarColumn(),
                    new PercentageColumn())
                .StartAsync(async ctx =>
                {
                    var syncTask = ctx.AddTask("[cyan]Uploading...[/]");
                    var progress = new Progress<SyncProgress>(p =>
                    {
                        if (p.TotalFiles > 0)
                        {
                            syncTask.MaxValue(p.TotalFiles);
                            syncTask.Value(p.ProcessedFiles);
                            syncTask.Description(BuildStatus(p, stopwatch.Elapsed));
                        }
                    });

                    syncResult = await syncService.SyncAsync(settings.Force, settings.Verbose, settings.DryRun,
                        settings.AutoConfirm, settings.Direction, progress);
                });

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Sync completed in {FormatElapsedTime(stopwatch.Elapsed)}[/]");
            AnsiConsole.WriteLine();

            var summaryTable = new Table { Border = TableBorder.None };
            summaryTable.AddColumn("");
            summaryTable.AddColumn("");

            summaryTable.AddRow("↑ Created:", ColorizeCounter(syncResult.Created, "green"));
            summaryTable.AddRow("↑ Updated:", ColorizeCounter(syncResult.Updated, "teal"));
            summaryTable.AddRow("- Skipped:", ColorizeCounter(syncResult.Skipped, "grey"));
            summaryTable.AddRow("↓ Downloaded:", ColorizeCounter(syncResult.Downloaded, "blue"));
            summaryTable.AddRow("× Removed:", ColorizeCounter(syncResult.Removed, "red"));
            summaryTable.AddRow("! Failed:", ColorizeCounter(syncResult.Failed, "red"));
            summaryTable.AddRow("⚠ Conflicts:", ColorizeCounter(syncResult.Conflicts, "yellow"));

            AnsiConsole.Write(summaryTable);

            if (syncResult.Conflicts > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Run without --dry-run to resolve conflicts[/]");
            }

            if (syncResult.Failed > 0)
            {
                return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            logger.LogError(ex, "Sync failed");
            return 1;
        }

        return 0;
    }

    private static string BuildStatus(SyncProgress p, TimeSpan elapsed)
    {
        var eta = CalculateEta(elapsed, p.ProcessedFiles, p.TotalFiles);
        var phaseLabel = p.Phase == "server" ? "Server actions" : "Uploading";

        var status = $"{phaseLabel}: {p.ProcessedFiles}/{p.TotalFiles} | " +
                     $"{ColorizeCounter(p.Result.Created, "green", "↑")} " +
                     $"{ColorizeCounter(p.Result.Updated, "teal", "↑")} " +
                     $"{ColorizeCounter(p.Result.Skipped, "grey", "-")} " +
                     $"{ColorizeCounter(p.Result.Downloaded, "blue", "↓")} " +
                     $"{ColorizeCounter(p.Result.Removed, "red", "×")} " +
                     $"{ColorizeCounter(p.Result.Failed, "red", "!")} " +
                     $"{ColorizeCounter(p.Result.Conflicts, "yellow", "⚠")} | " +
                     $"[magenta]{FormatElapsedTime(elapsed)}[/] | " +
                     $"[cyan]ETA: {eta}[/]";

        if (!string.IsNullOrEmpty(p.ErrorMessage))
        {
            status += $"\n  [red]Error: {EscapeMarkup(p.ErrorMessage)}[/]";
        }
        else if (!string.IsNullOrEmpty(p.CurrentFile) && p.Phase != "scanning")
        {
            status += $"\n  [dim]{EscapeMarkup(p.CurrentFile)}[/]";
        }

        return status;
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    private static string CalculateEta(TimeSpan elapsed, int processed, int total)
    {
        if (processed == 0 || total == 0)
        {
            return "--:--";
        }

        var progress = (double)processed / total;
        if (progress <= 0)
        {
            return "--:--";
        }

        var estimatedTotalTicks = elapsed.Ticks / progress;
        var remainingTicks = estimatedTotalTicks - elapsed.Ticks;

        if (remainingTicks < 0)
        {
            return "00:00";
        }

        var remaining = TimeSpan.FromTicks((long)remainingTicks);
        return FormatElapsedTime(remaining);
    }

    private static string ColorizeCounter(int value, string color, string prefix = "")
    {
        if (value == 0)
        {
            return $"[grey]{prefix}{value}[/]";
        }

        return $"[{color}]{prefix}{value}[/]";
    }

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{elapsed:hh\\:mm\\:ss}";
        }

        return $"{elapsed:mm\\:ss}";
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-f|--force")] public bool Force { get; set; }

        [CommandOption("-v|--verbose")] public bool Verbose { get; set; }

        [CommandOption("--dry-run")] public bool DryRun { get; set; }

        [CommandOption("-y|--yes")] public bool AutoConfirm { get; set; }

        [CommandOption("-d|--direction")]
        [TypeConverter(typeof(SyncDirectionConverter))]
        public SyncDirection Direction { get; set; } = SyncDirection.Both;
    }

    public class SyncDirectionConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        {
            if (value is string str)
            {
                return str.ToLowerInvariant() switch
                {
                    "up" => SyncDirection.Up,
                    "down" => SyncDirection.Down,
                    "both" => SyncDirection.Both,
                    _ => throw new InvalidOperationException($"Invalid direction '{value}'. Valid values are: up, down, both"),
                };
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        {
            return false;
        }
    }
}