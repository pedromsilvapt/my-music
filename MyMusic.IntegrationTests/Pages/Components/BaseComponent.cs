using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public abstract class BaseComponent(ILocator root)
{
    protected ILocator Root { get; } = root;

    protected async Task WaitForAttributeAsync(string name, string value, int timeout = 10000)
    {
        await Assertions.Expect(Root).ToHaveAttributeAsync(name, value, new() { Timeout = timeout });
    }
}
