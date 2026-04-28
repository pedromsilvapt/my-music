using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

public interface IFlow
{
    Task ExecuteAsync(IPage page);
}

public interface IFlow<T>
{
    Task<T> ExecuteAsync(IPage page);
}
