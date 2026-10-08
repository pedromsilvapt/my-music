using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using MyMusic.CLI;

namespace MyMusic.CLI.Commands;

public class InitCommand : Command<InitCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        using var activity = CliActivitySource.Instance.StartActivity("init");

        var configPath = GetConfigPath();
        EnsureConfigDirectory(configPath);

        var existing = ReadExistingConfig(configPath);
        if (!PromptOverwrite(configPath, settings.Yes))
        {
            return 0;
        }

        var serverUrl = PromptBaseUrl(settings.Server, existing?.ServerUrl);
        var userName = PromptUserName(settings.UserName, existing?.UserName);
        var deviceName = PromptDeviceName(settings.DeviceName, existing?.DeviceName);
        var repositoryPath = PromptRepositoryPath(settings.Repository, existing?.RepositoryPath);

        WriteConfig(configPath, serverUrl, userName, deviceName, repositoryPath);

        AnsiConsole.MarkupLine(
            $"[dim]If the server has no device named '{deviceName.EscapeMarkup()}' yet, create it in the web app (Devices > New device) before syncing.[/]");

        return 0;
    }

    internal static string GetConfigPath()
    {
        var envPath = Environment.GetEnvironmentVariable("MYMUSIC_CONFIG_PATH");
        return !string.IsNullOrEmpty(envPath)
            ? envPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "my-music",
                "appsettings.json");
    }

    private static void EnsureConfigDirectory(string configPath)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    internal static ExistingConfig? ReadExistingConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        var existingJson = File.ReadAllText(configPath);
        using var document = JsonDocument.Parse(existingJson);
        var doc = document.RootElement;

        if (!doc.TryGetProperty("MyMusic", out var myMusic))
        {
            return null;
        }

        string? serverUrl = null;
        string? userName = null;
        string? deviceName = null;
        string? repositoryPath = null;

        if (myMusic.TryGetProperty("Server", out var server))
        {
            if (server.TryGetProperty("BaseUrl", out var baseUrl))
            {
                serverUrl = baseUrl.GetString();
            }

            if (server.TryGetProperty("UserName", out var userNameValue))
            {
                userName = userNameValue.GetString();
            }
        }

        if (myMusic.TryGetProperty("Device", out var device))
        {
            if (device.TryGetProperty("Name", out var name))
            {
                deviceName = name.GetString();
            }
        }

        if (myMusic.TryGetProperty("Repository", out var repository))
        {
            if (repository.TryGetProperty("Path", out var path))
            {
                repositoryPath = path.GetString();
            }
        }

        return new ExistingConfig(serverUrl, userName, deviceName, repositoryPath);
    }

    private static bool PromptOverwrite(string configPath, bool yesFlag)
    {
        if (!File.Exists(configPath) || yesFlag)
        {
            return true;
        }

        var overwrite = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Config file already exists. Overwrite?")
                .AddChoices("Yes", "No"));

        return overwrite == "Yes";
    }

    private static string PromptBaseUrl(string? cliValue, string? defaultValue)
    {
        var rawValue = cliValue ?? AnsiConsole.Ask<string>(
            "Server address:",
            defaultValue ?? "http://localhost:5000");

        return NormalizeServerUrl(rawValue);
    }

    private static string NormalizeServerUrl(string url)
    {
        var trimmed = url.TrimEnd();

        if (trimmed.EndsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = trimmed.Substring(0, trimmed.Length - 5);
            AnsiConsole.MarkupLine("[yellow]Notice: The /api/ suffix is not needed and has been removed.[/]");
            return normalized;
        }

        if (trimmed.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = trimmed.Substring(0, trimmed.Length - 4);
            AnsiConsole.MarkupLine("[yellow]Notice: The /api suffix is not needed and has been removed.[/]");
            return normalized;
        }

        return trimmed;
    }

    private static string PromptUserName(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Prompt(
            new TextPrompt<string>("User name (optional):")
                .AllowEmpty()
                .DefaultValue(defaultValue ?? ""));

    private static string PromptDeviceName(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Ask<string>(
            "Device name:",
            defaultValue ?? "My Device");

    private static string PromptRepositoryPath(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Ask<string>(
            "Repository path:",
            defaultValue ?? "");

    internal static void WriteConfig(
        string configPath,
        string serverUrl,
        string userName,
        string deviceName,
        string repositoryPath)
    {
        var config = new JsonObject
        {
            ["MyMusic"] = new JsonObject
            {
                ["Server"] = new JsonObject
                {
                    ["BaseUrl"] = serverUrl,
                    ["UserName"] = userName,
                },
                ["Device"] = new JsonObject
                {
                    ["Name"] = deviceName,
                },
                ["Repository"] = new JsonObject
                {
                    ["Path"] = repositoryPath,
                    ["ExcludePatterns"] = new JsonArray("**/.*", "**/Thumbs.db", "**/*.tmp", "**/desktop.ini"),
                    ["MusicExtensions"] = new JsonArray(".mp3"),
                },
                ["Logging"] = new JsonObject
                {
                    ["EnableFileLogging"] = false,
                    ["FilePath"] = "mymusic-cli.log",
                },
            },
        };

        var json = config.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(configPath, json);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Configuration saved to:[/] [cyan]{configPath}[/]");
    }

    internal record ExistingConfig(
        string? ServerUrl,
        string? UserName,
        string? DeviceName,
        string? RepositoryPath
    );

    public class Settings : GlobalSettings
    {
        [CommandOption("-s|--server")] public string? Server { get; init; }

        [CommandOption("-u|--username")] public string? UserName { get; init; }

        [CommandOption("-d|--device-name")] public string? DeviceName { get; init; }

        [CommandOption("-r|--repository")] public string? Repository { get; init; }

        [CommandOption("-y|--yes")] public bool Yes { get; init; }
    }
}