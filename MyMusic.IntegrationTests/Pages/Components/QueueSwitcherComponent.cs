using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// Wraps the now playing page's popover that lists the user's queues and lets them view another one.
/// </summary>
public class QueueSwitcherComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// Views the queue whose name contains <paramref name="queueName"/>, without changing what is playing.
    /// </summary>
    public async Task ViewQueueAsync(string queueName)
    {
        await Root.GetByText(queueName).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
