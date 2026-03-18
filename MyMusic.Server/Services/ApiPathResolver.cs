using Microsoft.Extensions.Options;
using MyMusic.Common.Services;

namespace MyMusic.Server.Services;

/// <summary>
/// Server implementation of IApiPathResolver that reads from ServerConfig.
/// </summary>
public class ApiPathResolver(IOptions<ServerConfig> serverConfig) : IApiPathResolver
{
    public string ApiBasePath => serverConfig.Value.ApiBasePath;
}
