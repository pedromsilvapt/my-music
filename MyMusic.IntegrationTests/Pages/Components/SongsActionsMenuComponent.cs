using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// Wraps the floating Actions menu dropdown (the open <c>.mantine-Menu-dropdown</c>)
/// shown above a songs collection when rows are selected.
/// </summary>
public class SongsActionsMenuComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// Matches the "Import" menu item.
    /// </summary>
    public static Regex Import { get; } = new("Import");

    /// <summary>
    /// Matches the "Delete N Songs" menu item for any count via <c>\d+</c>.
    /// </summary>
    public static Regex DeleteSongs { get; } = new(@"^Delete \d+ Songs$");

    /// <summary>
    /// Matches the "Stop After This Song" menu item.
    /// </summary>
    public static Regex StopAfterThisSong { get; } = new("^Stop After This Song$");

    /// <summary>
    /// Matches the "Go to Details" menu item.
    /// </summary>
    public static Regex GoToDetails { get; } = new("^Go to Details$");

    /// <summary>
    /// Matches the queue menu items that only apply to songs listed in a queue
    /// (play, play next/last, skip, shuffle and remove from queue).
    /// </summary>
    public static Regex QueueOnlyActions { get; } = new("^(Play|Play Next|Play Last|Skip This Song|Shuffle|Remove from Queue)$");

    /// <summary>
    /// Returns the menu item locator matching the given name pattern.
    /// </summary>
    public ILocator GetItem(Regex name) =>
        Root.GetByRole(AriaRole.Menuitem, new() { NameRegex = name });

    /// <summary>
    /// Returns <see langword="true"/> when a menu item matching the given name pattern is present.
    /// </summary>
    public async Task<bool> HasItemAsync(Regex name) =>
        await GetItem(name).CountAsync() > 0;

    /// <summary>
    /// Clicks the menu item matching the given name pattern.
    /// </summary>
    public async Task ClickItemAsync(Regex name) =>
        await GetItem(name).ClickAsync();

    /// <summary>
    /// Clicks the "Import" menu item.
    /// </summary>
    public Task ImportAsync() =>
        ClickItemAsync(Import);

    /// <summary>
    /// Clicks the "Delete N Songs" menu item.
    /// </summary>
    public Task DeleteAsync() =>
        ClickItemAsync(DeleteSongs);

    /// <summary>
    /// Closes the menu by pressing Escape.
    /// </summary>
    public Task CloseAsync() =>
        Root.Page.Keyboard.PressAsync("Escape");
}