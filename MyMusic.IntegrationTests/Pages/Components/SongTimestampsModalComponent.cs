using System.Globalization;
using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// A timestamp of a song that the "Change Timestamps" tool can set.
/// </summary>
public enum SongTimestamp
{
    CreatedAt,
    ModifiedAt,
    AddedAt,
    FileModifiedAt,
}

/// <summary>
/// The dialog of the "Change Timestamps" tool, opened from the tools menu of the edit song modal.
/// </summary>
public class SongTimestampsModalComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync();
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false");
    }

    /// <summary>
    /// Fills the timestamp with the current time, using its "Set Now" button. Nothing is saved yet.
    /// </summary>
    public async Task SetNowAsync(SongTimestamp timestamp)
    {
        await Field(timestamp).GetByRole(AriaRole.Button, new() { Name = "Set Now" }).ClickAsync();
    }

    /// <summary>
    /// Returns the value the timestamp currently has in the dialog (UTC), or null when it is empty.
    /// </summary>
    public async Task<DateTime?> GetValueAsync(SongTimestamp timestamp)
    {
        var value = await Field(timestamp).GetAttributeAsync("data-value");

        return string.IsNullOrEmpty(value)
            ? null
            : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
    }

    /// <summary>
    /// Returns the values all the timestamps currently have in the dialog.
    /// </summary>
    public async Task<Dictionary<SongTimestamp, DateTime?>> GetValuesAsync()
    {
        var values = new Dictionary<SongTimestamp, DateTime?>();
        foreach (var timestamp in Enum.GetValues<SongTimestamp>())
        {
            values[timestamp] = await GetValueAsync(timestamp);
        }

        return values;
    }

    public async Task SaveAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task CancelAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    private ILocator Field(SongTimestamp timestamp) => Root.GetByTestId(timestamp switch
    {
        SongTimestamp.CreatedAt => "song-timestamp-created-at",
        SongTimestamp.ModifiedAt => "song-timestamp-modified-at",
        SongTimestamp.AddedAt => "song-timestamp-added-at",
        SongTimestamp.FileModifiedAt => "song-timestamp-file-modified-at",
        _ => throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "Unknown song timestamp"),
    });
}
