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

    private readonly List<TestUser> _users = [];
    private string _currentUserName = $"Test-{Guid.NewGuid()}";

    public static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") is { } envUrl && !string.IsNullOrEmpty(envUrl)
            ? envUrl
            : "http://localhost:5001";

    /// <summary>
    /// Number of test users to create at initialization. Override in a subclass to
    /// create additional users. Default is 1, preserving existing single-user behavior.
    /// </summary>
    protected virtual int UserCount => 1;

    /// <summary>
    /// All test users created during initialization. Index 0 is the primary user.
    /// </summary>
    protected IReadOnlyList<TestUser> Users => _users;

    /// <summary>
    /// The currently active user for API requests and browser context.
    /// Defaults to <see cref="Users"/>[0] after initialization.
    /// Use <see cref="SwitchUserAsync"/> to change it during a test.
    /// </summary>
    protected TestUser CurrentUser { get; private set; } = null!;

    protected IAPIRequestContext RequestContext { get; private set; } = null!;

    /// <summary>
    /// Username of the current user. Delegates to <see cref="CurrentUser"/>.
    /// </summary>
    protected string UserName => CurrentUser.UserName;

    protected string ServerRepositoryBase => $"/app/data/music/{UserName}";

    /// <summary>
    /// Id of the current user. Delegates to <see cref="CurrentUser"/>.
    /// </summary>
    protected long UserId => CurrentUser.Id;

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
        await CreateTestUsers();
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
                ["X-MyMusic-UserName"] = _currentUserName,
            },
        });
    }

    private async Task ConfigureBrowserContextAsync()
    {
        await Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["X-MyMusic-UserName"] = _currentUserName,
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
            headers["X-MyMusic-UserName"] = _currentUserName;

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

    /// <summary>
    /// Creates <see cref="UserCount"/> test users, populating <see cref="Users"/>
    /// and setting <see cref="CurrentUser"/> to the first one.
    /// </summary>
    protected virtual async Task CreateTestUsers()
    {
        for (var i = 0; i < UserCount; i++)
        {
            var userName = i == 0 ? _currentUserName : $"Test-{Guid.NewGuid()}";
            var user = await CreateOneTestUser(userName);
            _users.Add(user);
        }

        CurrentUser = _users[0];
        _currentUserName = CurrentUser.UserName;
    }

    /// <summary>
    /// Creates a single test user via the API. Override to customize user creation.
    /// The <see cref="RequestContext"/> is available and carries the first user's header
    /// (or the previous user's if creating subsequent users).
    /// </summary>
    protected virtual async Task<TestUser> CreateOneTestUser(string userName)
    {
        var response = await RequestContext.PostWithTraceAsync("/api/users", new()
        {
            DataObject = new
            {
                user = new
                {
                    username = userName,
                    name = userName,
                },
            },
        });

        response.Ok.ShouldBeTrue($"Failed to create test user: {response.Status} {response.StatusText}");

        var json = await response.JsonAsync();
        var id = json?.GetProperty("user").GetProperty("id").GetInt64()
            ?? throw new InvalidOperationException("Failed to get user ID from response");

        return new TestUser(id, userName);
    }

    /// <summary>
    /// Switches the current user to <see cref="Users"/>[<paramref name="index"/>], updating
    /// the API request context and browser context headers. Optionally reloads the browser
    /// page so the UI reflects the new user.
    /// </summary>
    /// <param name="index">Zero-based index into <see cref="Users"/>.</param>
    /// <param name="reloadPage">If true, navigates to <see cref="BaseUrl"/> after switching.</param>
    protected async Task SwitchUserAsync(int index, bool reloadPage = false)
    {
        if (index < 0 || index >= _users.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"User index {index} is out of range. {_users.Count} user(s) were created.");

        CurrentUser = _users[index];
        _currentUserName = CurrentUser.UserName;

        await RequestContext.DisposeAsync();
        await InitializeRequestContextAsync();

        await Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["X-MyMusic-UserName"] = _currentUserName,
        });

        if (reloadPage)
        {
            await Page.GotoAsync(BaseUrl);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await RemoveTestUsers();
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

    /// <summary>
    /// Deletes all created test users via the API. The server cascades all owned data
    /// (songs, albums, artists, devices, playlists, etc.) and removes the user's
    /// music directory on disk.
    /// </summary>
    protected virtual async Task RemoveTestUsers()
    {
        foreach (var user in _users)
        {
            var response = await RequestContext.DeleteWithTraceAsync($"/api/users/{user.Id}");
            response.Ok.ShouldBeTrue(
                $"Failed to delete test user '{user.UserName}': {response.Status} {response.StatusText}");
        }
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
}
