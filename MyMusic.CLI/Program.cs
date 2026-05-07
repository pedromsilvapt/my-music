using System.Diagnostics;
using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI;
using MyMusic.CLI.Api;
using MyMusic.CLI.Commands;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services;
using MyMusic.CLI.Services.Sync;
using MyMusic.OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Refit;
using Spectre.Console.Cli;

var traceParent = Environment.GetEnvironmentVariable("OTEL_TRACE_PARENT");
ActivityContext? parentContext = null;
if (ActivityContext.TryParse(traceParent, null, out ActivityContext context))
{
    parentContext = context;
}
var activityKind = parentContext.HasValue ? ActivityKind.Server : ActivityKind.Internal;
var argsString = string.Join(' ', args);

var logLevelOverride = ParseLogLevelOverride(args);
var verbose = IsVerbose(args);

var services = new ServiceCollection();

ConfigureConfiguration(services, args);
var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

var defaultLogLevel = configuration.GetValue<LogLevel>("Logging:LogLevel:Default", LogLevel.Information);
var effectiveLogLevel = logLevelOverride ?? defaultLogLevel;

ConfigureServices(services, args, effectiveLogLevel, verbose, configuration);

var provider = services.BuildServiceProvider();

var tracerProvider = provider.GetService<TracerProvider>();
var loggerProvider = provider.GetService<LoggerProvider>();

using var rootActivity = CliActivitySource.Instance.StartActivity(
    $"my-music {argsString}",
    activityKind,
    parentContext ?? default);

var app = new CommandApp(new TypeRegistrar(services, provider));

app.Configure(config =>
{
    config.AddCommand<SyncCommand>("sync");
    config.AddCommand<InitCommand>("init");
    config.AddBranch("history", history =>
    {
        history.AddCommand<HistoryListCommand>("ls");
        history.AddCommand<HistoryShowCommand>("show");
        history.AddCommand<HistoryRemoveCommand>("rm");
        history.AddCommand<HistoryPruneCommand>("prune");
    });
    config.PropagateExceptions();
});

var exitCode = app.Run(args);

rootActivity?.Stop();

tracerProvider?.ForceFlush(30000);
loggerProvider?.ForceFlush(30000);

tracerProvider?.Dispose();
loggerProvider?.Dispose();
provider.Dispose();

return exitCode;

static LogLevel? ParseLogLevelOverride(string[] args)
{
    var logLevelIndex = Array.FindIndex(args, a => a == "--loglevel" || a == "-l");
    if (logLevelIndex >= 0 && logLevelIndex + 1 < args.Length)
    {
        var levelString = args[logLevelIndex + 1];
        if (Enum.TryParse<LogLevel>(levelString, ignoreCase: true, out var level))
        {
            return level;
        }
    }

    return null;
}

static bool IsVerbose(string[] args) =>
    args.Contains("--verbose") || args.Contains("-v");

static void ConfigureConfiguration(IServiceCollection services, string[] args)
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

    var userConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "my-music",
        "appsettings.json");

    var configPath = Environment.GetEnvironmentVariable("MYMUSIC_CONFIG_PATH");

    var configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", true, true)
        .AddJsonFile($"appsettings.{environment.ToLower()}.json", true, true)
        .AddJsonFile(userConfigPath, true, true);

    if (!string.IsNullOrEmpty(configPath))
    {
        configurationBuilder.AddJsonFile(configPath, true, true);
    }

    configurationBuilder.AddEnvironmentVariables();

    var configuration = configurationBuilder.Build();

    services.AddOptions<MyMusicOptions>()
        .Bind(configuration.GetSection("MyMusic"))
        .ValidateDataAnnotations();

    services.AddSingleton<IConfiguration>(configuration);
}

static void ConfigureServices(IServiceCollection services, string[] args, LogLevel effectiveLogLevel, bool verbose, IConfiguration configuration)
{
    services.AddSingleton<IFileSystem, FileSystem>();

    var loggerOptions = new LoggingOptions();
    configuration.GetSection("MyMusic:Logging").Bind(loggerOptions);

    services.AddLogging(builder =>
    {
        builder.SetMinimumLevel(effectiveLogLevel);

        if (verbose)
        {
            builder.AddConsole();
        }

        if (loggerOptions.EnableFileLogging)
        {
            builder.AddProvider(new FileLoggerProvider(loggerOptions.FilePath, new FileSystem(), effectiveLogLevel));
        }
    });

    services.AddSingleton<IFileScanner, FileScanner>();

    services.AddTransient<SyncCommand>();
    services.AddTransient<InitCommand>();
    services.AddTransient<HistoryShowCommand>();
    services.AddTransient<HistoryListCommand>();
    services.AddTransient<HistoryRemoveCommand>();
    services.AddTransient<HistoryPruneCommand>();

    services.AddTransient<AuthenticatedHttpClientHandler>();
    services.AddTransient<HttpLoggingHandler>();

    services.AddRefitClient<IMyMusicClient>(GetRefitSettings)
        .ConfigureHttpClient((sp, httpClient) =>
        {
            var options = sp.GetRequiredService<IOptions<MyMusicOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.Server.BaseUrl);
        })
        .AddHttpMessageHandler<HttpLoggingHandler>()
        .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

    services.AddSingleton<IFileSystemScanner, CliFileSystemScanner>();
    services.AddSingleton<IFileOps, CliFileOps>();
    services.AddSingleton<IKeepAwake, CliKeepAwake>();
    services.AddSingleton<IUserPrompt, CliUserPrompt>();
    services.AddSingleton<ISyncConfig, CliSyncConfig>();
    services.AddSingleton<ISyncApiClient, CliSyncApiClient>();

    services.AddScoped<Phases>();
    services.AddScoped<Orchestrator>();
    services.AddScoped<ISyncService, SyncService>();

    services.AddMyMusicOpenTelemetry(configuration, "MyMusic.CLI");
}

static RefitSettings GetRefitSettings(IServiceProvider sp) =>
    new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(),
    };

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly IFileSystem _fileSystem;
    private readonly LogLevel _minimumLevel;

    public FileLoggerProvider(string filePath, IFileSystem fileSystem, LogLevel minimumLevel)
    {
        _filePath = filePath;
        _fileSystem = fileSystem;
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath, categoryName, _fileSystem, _minimumLevel);

    public void Dispose() { }
}

public class FileLogger : ILogger
{
    private static readonly object _lock = new();
    private readonly string _categoryName;
    private readonly string _filePath;
    private readonly IFileSystem _fileSystem;
    private readonly LogLevel _minimumLevel;

    public FileLogger(string filePath, string categoryName, IFileSystem fileSystem, LogLevel minimumLevel)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _fileSystem = fileSystem;
        _minimumLevel = minimumLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}] {message}";

        if (exception != null)
        {
            logEntry += Environment.NewLine + exception;
        }

        lock (_lock)
        {
            _fileSystem.File.AppendAllText(_filePath, logEntry + Environment.NewLine);
        }
    }
}
