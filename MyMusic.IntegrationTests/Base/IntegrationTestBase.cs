using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.OpenTelemetry.XUnit;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Base;

public abstract class IntegrationTestBase : PageTest
{
    private static readonly bool RecordVideoEnabled =
        Environment.GetEnvironmentVariable("PLAYWRIGHT_RECORD_VIDEO")?.ToLower() == "true";

    private static readonly string TestResultsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "test-results"));

    protected static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    private string? _tracePath;
    private IntegrationTestTelemetry? _telemetry;
    private readonly ITestOutputHelper _output;

    public static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") is { } envUrl && !string.IsNullOrEmpty(envUrl)
            ? envUrl
            : "http://localhost:5001";

    protected IAPIRequestContext RequestContext { get; private set; } = null!;
    protected string UserName { get; } = $"Test-{Guid.NewGuid()}";
    protected string ServerRepositoryBase => $"/app/data/music/{UserName}";
    protected long UserId { get; private set; }
    protected ILogger Logger => _telemetry.TestsLogger;
    protected IntegrationTestTelemetry Telemetry => _telemetry;

    public string Traceparent => Activity.Current?.Id ?? string.Empty;

    public IntegrationTestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();

        if (RecordVideoEnabled)
        {
            var videosDir = Path.Combine(TestResultsDir, "videos");
            Directory.CreateDirectory(videosDir);

            options.RecordVideoDir = videosDir;
            options.RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 };
        }

        return options;
    }

    public override async ValueTask InitializeAsync()
    {
        _telemetry = new(_output);
        await base.InitializeAsync();

        Page.SetDefaultTimeout(5000);
        Page.SetDefaultNavigationTimeout(10000);

        InitializeTelemetry();
        await StartTraceRecordingAsync();
        await InitializeRequestContextAsync();
        await CreateTestUser();
        await ConfigureBrowserContextAsync();
        await Page.GotoAsync(BaseUrl);
    }

    private void InitializeTelemetry()
        => _telemetry.ConfigurePageLogging(Page);

    private async Task StartTraceRecordingAsync()
    {
        if (!RecordVideoEnabled) return;

        var tracesDir = Path.Combine(TestResultsDir, "traces");
        Directory.CreateDirectory(tracesDir);
        _tracePath = Path.Combine(tracesDir, $"{GetType().Name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
    }

    private async Task InitializeRequestContextAsync()
    {
        RequestContext = await Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = BaseUrl,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["X-MyMusic-UserName"] = UserName,
            },
        });
    }

    private async Task ConfigureBrowserContextAsync()
    {
        await Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["X-MyMusic-UserName"] = UserName,
        });

        await Context.RouteAsync("**/*", async route =>
        {
            var request = route.Request;
            var requestId = request.GetHashCode().ToString();
            var method = request.Method;
            var url = request.Url;
            var resourceType = request.ResourceType;
            var isNavigationRequest = request.IsNavigationRequest;

            var span = _telemetry.StartParallelRequestSpan(
                requestId, 
                method, 
                url, 
                resourceType, 
                isNavigationRequest);
            
            var headers = request.Headers.ToDictionary(k => k.Key, k => k.Value);
            headers["X-MyMusic-UserName"] = UserName;
            
            if (span != null)
            {
                headers["traceparent"] = IntegrationTestTelemetry.CreateW3CTraceParent(span.Context);
            }

            await route.ContinueAsync(new RouteContinueOptions { Headers = headers });
        });

        Context.Response += (_, response) =>
        {
            var request = response.Request;
            var requestId = request.GetHashCode().ToString();
            
            var contentLength = response.Headers.TryGetValue("content-length", out var lengthStr)
                && long.TryParse(lengthStr, out var length)
                ? length
                : (long?)null;
            
            _telemetry.StopParallelRequestSpan(requestId, response.Status, contentLength);
        };

        Context.RequestFailed += (_, request) =>
        {
            var requestId = request.GetHashCode().ToString();
            _telemetry.StopParallelRequestSpan(requestId, statusCode: 0);
        };
    }

    public override async ValueTask DisposeAsync()
    {
        await RemoveTestUser();
        await RequestContext.DisposeAsync();
        await Page.CloseAsync();

        if (RecordVideoEnabled)
        {
            await SaveTraceAsync();
            await base.DisposeAsync();
            await SaveVideoAsync();
        }
        else
        {
            await base.DisposeAsync();
        }

        _telemetry.Dispose();
    }

    private async Task SaveTraceAsync()
    {
        try
        {
            if (_tracePath != null)
            {
                await Context.Tracing.StopAsync(new() { Path = _tracePath });
                Logger.LogInformation("Trace recorded: {TracePath}", _tracePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Trace failed: {Message}", ex.Message);
        }
    }

    private async Task SaveVideoAsync()
    {
        try
        {
            var video = Page.Video;
            if (video != null)
            {
                var videoPath = await video.PathAsync();
                Logger.LogInformation("Video recorded: {TracePath}", videoPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Video failed: {Message}", ex.Message);
        }
    }

    protected virtual async Task CreateTestUser()
    {
        var response = await RequestContext.PostWithTraceAsync("/api/users", new()
        {
            DataObject = new
            {
                user = new
                {
                    username = UserName,
                    name = UserName,
                },
            },
        });

        response.Ok.ShouldBeTrue($"Failed to create test user: {response.Status} {response.StatusText}");

        var json = await response.JsonAsync();
        UserId = json?.GetProperty("user").GetProperty("id").GetInt64()
            ?? throw new InvalidOperationException("Failed to get user ID from response");
    }

    protected virtual async Task RemoveTestUser()
    {
        var response = await RequestContext.DeleteWithTraceAsync($"/api/users/{UserId}");
        response.Ok.ShouldBeTrue($"Failed to delete test user: {response.Status} {response.StatusText}");
    }
}
