using Microsoft.Extensions.Options;
using MyMusic.CLI.Configuration;

namespace MyMusic.CLI;

public class AuthenticatedHttpClientHandler(IOptions<MyMusicOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (options.Value.Server.UserId.HasValue)
        {
            request.Headers.Add("X-MyMusic-UserId", options.Value.Server.UserId.Value.ToString());
        }

        if (options.Value.Server.UserName is not null)
        {
            request.Headers.Add("X-MyMusic-UserName", options.Value.Server.UserName);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}