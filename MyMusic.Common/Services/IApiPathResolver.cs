namespace MyMusic.Common.Services;

/// <summary>
/// Provides the base API path for constructing artwork URLs.
/// Implemented in the server project to access configuration.
/// </summary>
public interface IApiPathResolver
{
    string ApiBasePath { get; }
}
