using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MyMusic.CLI.Commands;

public class InitCommand : Command<InitCommand.Settings>
{
    private static readonly Dictionary<string, string> DeviceIcons = new()
    {
        { "Desktop", "IconDevicesPc" },
        { "Laptop", "IconDeviceLaptop" },
        { "Smartphone", "IconDeviceMobile" },
        { "Tablet", "IconDeviceTablet" },
        { "USB Drive", "IconUsb" },
        { "MP3 Player", "IconDeviceMp3" },
    };

    private static readonly Dictionary<string, string> DeviceIconsReverse =
        DeviceIcons.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public override int Execute(CommandContext context, Settings settings)
    {
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
        var deviceType = PromptDeviceType(settings.DeviceType, existing?.DeviceIcon);
        var repositoryPath = PromptRepositoryPath(settings.Repository, existing?.RepositoryPath);

        WriteConfig(configPath, serverUrl, userName, deviceName, deviceType, repositoryPath);

        return 0;
    }

    private static string GetConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "my-music",
            "appsettings.json");

    private static void EnsureConfigDirectory(string configPath)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static ExistingConfig? ReadExistingConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        var existingJson = File.ReadAllText(configPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(existingJson);

        if (!doc.TryGetProperty("MyMusic", out var myMusic))
        {
            return null;
        }

        string? serverUrl = null;
        string? userName = null;
        string? deviceName = null;
        string? deviceIcon = null;
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

            if (device.TryGetProperty("Icon", out var icon))
            {
                deviceIcon = icon.GetString();
            }
        }

        if (myMusic.TryGetProperty("Repository", out var repository))
        {
            if (repository.TryGetProperty("Path", out var path))
            {
                repositoryPath = path.GetString();
            }
        }

        return new ExistingConfig(serverUrl, userName, deviceName, deviceIcon, repositoryPath);
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

    private static string PromptBaseUrl(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Ask<string>(
            "Server address:",
            defaultValue ?? "http://localhost:5000/api");

    private static string PromptUserName(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Prompt(
            new TextPrompt<string>("User name (optional):")
                .AllowEmpty()
                .DefaultValue(defaultValue ?? ""));

    private static string PromptDeviceName(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Ask<string>(
            "Device name:",
            defaultValue ?? "My Device");

    private static string PromptDeviceType(string? cliValue, string? defaultIconValue)
    {
        string? resolvedDeviceType = null;

        if (!string.IsNullOrEmpty(cliValue))
        {
            if (DeviceIcons.TryGetValue(cliValue, out var iconName))
            {
                resolvedDeviceType = cliValue;
            }
            else if (DeviceIconsReverse.TryGetValue(cliValue, out var displayName))
            {
                resolvedDeviceType = displayName;
            }
        }

        var defaultDeviceType = resolvedDeviceType ?? (defaultIconValue != null &&
                                                       DeviceIconsReverse.TryGetValue(defaultIconValue,
                                                           out var existing)
            ? existing
            : DeviceIcons.Keys.First());

        var selectionPrompt = new SelectionPrompt<string>()
            .Title("Device type:");

        foreach (var key in new[] { defaultDeviceType }.Concat(DeviceIcons.Keys).Distinct())
        {
            selectionPrompt.AddChoice(key);
        }

        var deviceType = AnsiConsole.Prompt(selectionPrompt);
        AnsiConsole.MarkupLine($"[dim]Device type: {deviceType} ({DeviceIcons[deviceType]})[/]");

        return deviceType;
    }

    private static string PromptRepositoryPath(string? cliValue, string? defaultValue) =>
        cliValue ?? AnsiConsole.Ask<string>(
            "Repository path:",
            defaultValue ?? "");

    private static void WriteConfig(
        string configPath,
        string serverUrl,
        string userName,
        string deviceName,
        string deviceType,
        string repositoryPath)
    {
        var config = new Dictionary<string, object>
        {
            ["MyMusic"] = new Dictionary<string, object>
            {
                ["Server"] = new Dictionary<string, object>
                {
                    ["BaseUrl"] = serverUrl,
                    ["UserName"] = userName,
                },
                ["Device"] = new Dictionary<string, object>
                {
                    ["Name"] = deviceName,
                    ["Icon"] = DeviceIcons[deviceType],
                },
                ["Repository"] = new Dictionary<string, object>
                {
                    ["Path"] = repositoryPath,
                    ["ExcludePatterns"] = new[] { "**/.*", "**/Thumbs.db", "**/*.tmp", "**/desktop.ini" },
                    ["MusicExtensions"] = new[] { ".mp3" },
                },
                ["Sync"] = new Dictionary<string, object>
                {
                    ["ChunkSize"] = 50,
                },
                ["Logging"] = new Dictionary<string, object>
                {
                    ["EnableFileLogging"] = false,
                    ["FilePath"] = "mymusic-cli.log",
                },
            },
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(configPath, json);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Configuration saved to:[/] [cyan]{configPath}[/]");
    }

    private record ExistingConfig(
        string? ServerUrl,
        string? UserName,
        string? DeviceName,
        string? DeviceIcon,
        string? RepositoryPath
    );

    public class Settings : CommandSettings
    {
        [CommandOption("-s|--server")] public string? Server { get; init; }

        [CommandOption("-u|--username")] public string? UserName { get; init; }

        [CommandOption("-d|--device-name")] public string? DeviceName { get; init; }

        [CommandOption("-t|--device-type")] public string? DeviceType { get; init; }

        [CommandOption("-r|--repository")] public string? Repository { get; init; }

        [CommandOption("-y|--yes")] public bool Yes { get; init; }
    }
}