using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MyMusic.OpenTelemetry.XUnit.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace MyMusic.OpenTelemetry.XUnit;

public class IntegrationTestTelemetry : IDisposable
{
    public static readonly ActivitySource ActivitySource = new("MyMusic.IntegrationTests");
    public static readonly ActivitySource BrowserActivitySource = new("MyMusic.Client");

    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;
    private ActivityContext _rootContext;
    private TracerProvider? _clientTracerProvider;

    public ILogger TestsLogger { get; }
    public ILogger PlaywrightLogger { get; }

    public int MaxBodyLogLength { get; set; } = 1000;

    private readonly ConcurrentDictionary<string, Activity> _inflightRequests = new();

    public IntegrationTestTelemetry(ITestOutputHelper output)
    {
        var framework = OpenTelemetryTestFramework.Current!;
        var otelConfig = framework.OtelConfig ?? new OtelConfig();

        _loggerFactory = framework.Services!.GetRequiredService<ILoggerFactory>();
        TestsLogger = _loggerFactory.CreateLogger("MyMusic.IntegrationTests");
        PlaywrightLogger = _loggerFactory.CreateLogger("MyMusic.Playwright");

        _rootContext = Activity.Current!.Context;

        if (otelConfig.Enabled)
        {
            _clientTracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureResource(resource => resource.AddService("MyMusic.Client"))
                .AddSource(BrowserActivitySource.Name)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = otelConfig.GetTracesEndpoint();
                    options.Protocol = otelConfig.GetProtocol();
                })
                .Build();
        }
    }

    public void ConfigurePageLogging(IPage page)
    {
        page.Request += (_, request) => LogRequest(request);
        page.Response += (_, response) => LogResponse(response);
        page.RequestFailed += (_, request) => LogRequestFailed(request);
        page.Console += (_, msg) => LogConsoleMessage(msg);
    }

    public Activity? StartParallelRequestSpan(
        string requestId, 
        string method, 
        string url, 
        string resourceType, 
        bool isNavigationRequest)
    {
        var previousCurrent = Activity.Current;

        var span = BrowserActivitySource.StartActivity(
            $"{method} {url}",
            ActivityKind.Client,
            parentContext: _rootContext);

        Activity.Current = previousCurrent;

        if (span != null)
        {
            _inflightRequests[requestId] = span;
            
            span.SetTag("http.method", method);
            span.SetTag("http.url", url);
            span.SetTag("request.resource_type", resourceType);
            span.SetTag("request.is_navigation", isNavigationRequest);
        }

        return span;
    }

    public void StopParallelRequestSpan(string requestId, int statusCode, long? contentLength = null)
    {
        if (_inflightRequests.TryRemove(requestId, out var activity))
        {
            activity.SetTag("http.status_code", statusCode);
            
            if (contentLength.HasValue)
            {
                activity.SetTag("http.response_content_length", contentLength.Value);
            }
            
            activity.Stop();
            activity.Dispose();
        }
    }

    public static string CreateW3CTraceParent(ActivityContext context)
    {
        var traceId = context.TraceId.ToHexString();
        var spanId = context.SpanId.ToHexString();
        var flags = context.TraceFlags == ActivityTraceFlags.Recorded ? "01" : "00";
        return $"00-{traceId}-{spanId}-{flags}";
    }

    public Activity? StartProcessSpan(string processName, string? args = null)
    {
        var spanName = args is not null
            ? $"{processName} {args}"
            : processName;
        return ActivitySource.StartActivity("SPAWN " + spanName, ActivityKind.Client);
    }

    private void LogRequest(IRequest request)
    {
        var method = request.Method;
        var url = request.Url;

        PlaywrightLogger.LogInformation(
            "Request: {Method} {Url}",
            method,
            url);

        var headers = request.Headers;
        if (headers?.Count > 0)
        {
            PlaywrightLogger.LogDebug(
                "Request Headers: {@Headers}",
                headers);
        }

        LogBodyAsync(request, "Request");
    }

    private void LogResponse(IResponse response)
    {
        var status = response.Status;
        var url = response.Url;

        PlaywrightLogger.LogInformation(
            "Response: {Status} {Url}",
            status,
            url);

        var headers = response.Headers;
        if (headers?.Count > 0)
        {
            PlaywrightLogger.LogDebug(
                "Response Headers: {@Headers}",
                headers);
        }

        LogBodyAsync(response, "Response");
    }

    private void LogRequestFailed(IRequest request)
    {
        var method = request.Method;
        var url = request.Url;
        var failure = request.Failure;

        PlaywrightLogger.LogError(
            "Request Failed: {Method} {Url} - {Failure}",
            method,
            url,
            failure);

        var headers = request.Headers;
        if (headers?.Count > 0)
        {
            PlaywrightLogger.LogDebug(
                "Request Failed Headers: {@Headers}",
                headers);
        }

        LogBodyAsync(request, "Request Failed");
    }

    private void LogBodyAsync(object source, string prefix)
    {
        _ = LogBodyInternalAsync(source, prefix);
    }

    private async Task LogBodyInternalAsync(object source, string prefix)
    {
        try
        {
            string? body = null;

            if (source is IRequest request)
            {
                body = request.PostData;
            }
            else if (source is IResponse response)
            {
                try
                {
                    body = await response.TextAsync();
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(body))
            {
                var truncatedBody = body.Length > MaxBodyLogLength
                    ? body.Substring(0, MaxBodyLogLength) + "...[truncated]"
                    : body;

                PlaywrightLogger.LogDebug(
                    "{Prefix} Body: {Body}",
                    prefix,
                    truncatedBody);
            }
        }
        catch (Exception ex)
        {
            PlaywrightLogger.LogWarning(
                "Failed to read {Prefix} body: {Error}",
                prefix,
                ex.Message);
        }
    }

    private void LogConsoleMessage(IConsoleMessage msg)
    {
        var logLevel = MapConsoleMessageTypeToLogLevel(msg.Type);
        PlaywrightLogger.Log(
            logLevel,
            "Console: {Message}",
            msg.Text);
    }

    private static LogLevel MapConsoleMessageTypeToLogLevel(string type)
    {
        return type.ToLower() switch
        {
            "error" => LogLevel.Error,
            "warning" => LogLevel.Warning,
            "warn" => LogLevel.Warning,
            "info" => LogLevel.Information,
            "log" => LogLevel.Information,
            "debug" => LogLevel.Debug,
            "trace" => LogLevel.Trace,
            "dir" => LogLevel.Debug,
            "dirxml" => LogLevel.Debug,
            "table" => LogLevel.Debug,
            "startgroup" => LogLevel.Debug,
            "startgroupcollapsed" => LogLevel.Debug,
            "endgroup" => LogLevel.Debug,
            "clear" => LogLevel.Debug,
            "count" => LogLevel.Debug,
            "countreset" => LogLevel.Debug,
            "time" => LogLevel.Debug,
            "timeend" => LogLevel.Debug,
            "timestamp" => LogLevel.Debug,
            "profile" => LogLevel.Debug,
            "profileend" => LogLevel.Debug,
            "assert" => LogLevel.Error,
            _ => LogLevel.Debug
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var activity in _inflightRequests.Values)
            {
                activity.Stop();
                activity.Dispose();
            }
            _inflightRequests.Clear();

            var framework = OpenTelemetryTestFramework.Current;
            var timeoutMs = framework?.OtelConfig?.ForceFlushTimeoutMilliseconds ?? 30000;

            if (_clientTracerProvider != null)
            {
                _clientTracerProvider.ForceFlush(timeoutMs);
                _clientTracerProvider.Dispose();
            }

            _disposed = true;
        }
    }
}
