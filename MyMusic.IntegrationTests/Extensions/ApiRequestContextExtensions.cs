using System.Diagnostics;
using Microsoft.Playwright;
using MyMusic.OpenTelemetry.XUnit;

namespace MyMusic.IntegrationTests.Extensions;

public static class ApiRequestContextExtensions
{
    public static async Task<IAPIResponse> GetWithTraceAsync(
        this IAPIRequestContext api, string url, APIRequestContextOptions? options = null)
    {
        using var span = IntegrationTestTelemetry.ActivitySource.StartActivity($"GET {url}", ActivityKind.Client);
        var headers = AddTraceparentHeader(options, span);
        return await api.GetAsync(url, headers);
    }

    public static async Task<IAPIResponse> PostWithTraceAsync(
        this IAPIRequestContext api, string url, APIRequestContextOptions? options = null)
    {
        using var span = IntegrationTestTelemetry.ActivitySource.StartActivity($"POST {url}", ActivityKind.Client);
        var headers = AddTraceparentHeader(options, span);
        return await api.PostAsync(url, headers);
    }

    public static async Task<IAPIResponse> PutWithTraceAsync(
        this IAPIRequestContext api, string url, APIRequestContextOptions? options = null)
    {
        using var span = IntegrationTestTelemetry.ActivitySource.StartActivity($"PUT {url}", ActivityKind.Client);
        var headers = AddTraceparentHeader(options, span);
        return await api.PutAsync(url, headers);
    }

    public static async Task<IAPIResponse> DeleteWithTraceAsync(
        this IAPIRequestContext api, string url, APIRequestContextOptions? options = null)
    {
        using var span = IntegrationTestTelemetry.ActivitySource.StartActivity($"DELETE {url}", ActivityKind.Client);
        var headers = AddTraceparentHeader(options, span);
        return await api.DeleteAsync(url, headers);
    }

    public static async Task<IAPIResponse> PatchWithTraceAsync(
        this IAPIRequestContext api, string url, APIRequestContextOptions? options = null)
    {
        using var span = IntegrationTestTelemetry.ActivitySource.StartActivity($"PATCH {url}", ActivityKind.Client);
        var headers = AddTraceparentHeader(options, span);
        return await api.PatchAsync(url, headers);
    }

    public static async Task<IAPIResponse> FetchWithTraceAsync(
        this IAPIRequestContext api, string url, APIRequestContextOptions? options = null)
    {
        using var span = IntegrationTestTelemetry.ActivitySource.StartActivity($"FETCH {url}", ActivityKind.Client);
        var headers = AddTraceparentHeader(options, span);
        return await api.FetchAsync(url, headers);
    }

    private static APIRequestContextOptions? AddTraceparentHeader(APIRequestContextOptions? options, Activity? span)
    {
        if (span == null) return options;

        var traceparent = IntegrationTestTelemetry.CreateW3CTraceParent(span.Context);

        options ??= new();

        var headers = options.Headers != null
            ? new Dictionary<string, string>(options.Headers)
            : new Dictionary<string, string>();

        headers["traceparent"] = traceparent;
        options.Headers = headers;

        return options;
    }
}
