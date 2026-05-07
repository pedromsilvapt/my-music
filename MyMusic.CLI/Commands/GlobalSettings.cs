using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

public class GlobalSettings : CommandSettings
{
    [CommandOption("-l|--loglevel <LEVEL>")]
    [Description("Minimum log level: Trace, Debug, Information, Warning, Error, Critical")]
    public string? LogLevel { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Enable console log output")]
    public bool Verbose { get; set; }

    public LogLevel? GetLogLevelOverride()
    {
        if (!string.IsNullOrEmpty(LogLevel) && Enum.TryParse<LogLevel>(LogLevel, ignoreCase: true, out var level))
        {
            return level;
        }

        return null;
    }
}
