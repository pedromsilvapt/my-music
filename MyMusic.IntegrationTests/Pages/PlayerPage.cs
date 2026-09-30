using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class PlayerPage(IPage page) : BasePage(page, "player")
{
    public SongsCollectionComponent Collection => new(Root.GetByTestId("collection"));

    /// <summary>
    /// Opens the queue switcher from the queue name in the page header.
    /// </summary>
    public async Task<QueueSwitcherComponent> OpenQueueSwitcherAsync()
    {
        await Root.GetByTestId("queue-switcher-toggle").ClickAsync();

        // The popover is rendered in a portal, outside the page root
        var switcherRoot = Page.GetByTestId("queue-switcher");
        await switcherRoot.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return new QueueSwitcherComponent(switcherRoot);
    }

    /// <summary>
    /// Starts recording the title of every song rendered in the queue list, including any that only
    /// show up for a single render. Read them back with <see cref="StopRecordingSongTitlesAsync"/>.
    /// </summary>
    public async Task StartRecordingSongTitlesAsync()
    {
        await Root.EvaluateAsync("""
            root => {
                const titles = new Set();
                const record = () => root
                    .querySelectorAll("td[data-testid^='collection-cell-title-']")
                    .forEach(cell => titles.add(cell.textContent.trim()));
                record();
                // Mutation callbacks run before the browser paints, so no rendered state is missed
                const observer = new MutationObserver(record);
                observer.observe(root, { childList: true, subtree: true, characterData: true });
                window.__songTitlesRecording = { titles, observer };
            }
            """);
    }

    /// <summary>
    /// Stops the recording started by <see cref="StartRecordingSongTitlesAsync"/> and returns every
    /// song title rendered in the queue list since then.
    /// </summary>
    public async Task<string[]> StopRecordingSongTitlesAsync()
    {
        return await Page.EvaluateAsync<string[]>("""
            () => {
                const { titles, observer } = window.__songTitlesRecording;
                observer.disconnect();
                return [...titles];
            }
            """);
    }
}
