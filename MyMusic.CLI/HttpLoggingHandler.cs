using System.Text;
using Microsoft.Extensions.Logging;

namespace MyMusic.CLI;

public class HttpLoggingHandler(ILogger<HttpLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LogRequest(request);

        var response = await base.SendAsync(request, cancellationToken);

        LogResponse(response);

        return response;
    }

    private void LogRequest(HttpRequestMessage request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("HTTP Request:");
        sb.AppendLine($"  {request.Method} {request.RequestUri}");

        if (request.Headers.Any())
        {
            sb.AppendLine("  Headers:");
            foreach (var header in request.Headers)
            {
                sb.AppendLine($"    {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        if (request.Content?.Headers.Any() == true)
        {
            sb.AppendLine("  Content Headers:");
            foreach (var header in request.Content.Headers)
            {
                sb.AppendLine($"    {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        logger.LogTrace(sb.ToString());
    }

    private void LogResponse(HttpResponseMessage response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("HTTP Response:");
        sb.AppendLine($"  {(int)response.StatusCode} {response.StatusCode}");
        sb.AppendLine($"  Request: {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}");

        if (response.Headers.Any())
        {
            sb.AppendLine("  Headers:");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"    {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        if (response.Content?.Headers.Any() == true)
        {
            sb.AppendLine("  Content Headers:");
            foreach (var header in response.Content.Headers)
            {
                sb.AppendLine($"    {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        logger.LogTrace(sb.ToString());
    }
}
