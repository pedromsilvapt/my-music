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
using Refit;
using Spectre.Console.Cli;

var services = new ServiceCollection();

ConfigureConfiguration(services, args);
ConfigureServices(services, args);

var provider = services.BuildServiceProvider();

var app = new CommandApp(new TypeRegistrar(services));

app.Configure(config =>
{
    config.AddCommand<SyncCommand>("sync");
    config.AddBranch("history", history =>
    {
        history.AddCommand<HistoryListCommand>("ls");
        history.AddCommand<HistoryShowCommand>("show");
    });
    config.PropagateExceptions();
});

return app.Run(args);

static void ConfigureConfiguration(IServiceCollection services, string[] args)
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

    var userConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "my-music",
        "appsettings.json");

    var configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", false, true)
        .AddJsonFile($"appsettings.{environment.ToLower()}.json", true, true)
        .AddJsonFile(userConfigPath, true, true)
        .AddEnvironmentVariables();

    var configuration = configurationBuilder.Build();

    services.AddOptions<MyMusicOptions>()
        .Bind(configuration.GetSection("MyMusic"))
        .ValidateDataAnnotations();

    services.AddSingleton(configuration);
}

static void ConfigureServices(IServiceCollection services, string[] args)
{
    services.AddSingleton<IFileSystem, FileSystem>();

    var loggerOptions = new LoggingOptions();
    var configuration = services.BuildServiceProvider().GetService<IConfiguration>();

    if (configuration != null)
    {
        configuration.GetSection("MyMusic:Logging").Bind(loggerOptions);
    }

    var isVerbose = args.Contains("--verbose") || args.Contains("-v");

    services.AddLogging(builder =>
    {
        if (isVerbose)
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        }

        if (loggerOptions.EnableFileLogging)
        {
            builder.AddProvider(new FileLoggerProvider(loggerOptions.FilePath, new FileSystem()));
            builder.SetMinimumLevel(LogLevel.Information);
        }
    });

    services.AddSingleton<IFileScanner, FileScanner>();

    services.AddTransient<SyncCommand>();
    services.AddTransient<HistoryShowCommand>();
    services.AddTransient<HistoryListCommand>();

    services.AddTransient<AuthenticatedHttpClientHandler>();

    services.AddRefitClient<IMyMusicClient>(GetRefitSettings)
        .ConfigureHttpClient((sp, httpClient) =>
        {
            var options = sp.GetRequiredService<IOptions<MyMusicOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.Server.BaseUrl);
        })
        .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

    services.AddScoped<ISyncService, SyncService>();
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

    public FileLoggerProvider(string filePath, IFileSystem fileSystem)
    {
        _filePath = filePath;
        _fileSystem = fileSystem;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath, categoryName, _fileSystem);

    public void Dispose() { }
}

public class FileLogger : ILogger
{
    private static readonly object _lock = new();
    private readonly string _categoryName;
    private readonly string _filePath;
    private readonly IFileSystem _fileSystem;

    public FileLogger(string filePath, string categoryName, IFileSystem fileSystem)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _fileSystem = fileSystem;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

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