using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using System.Text.Json;
using Shouldly;

namespace MyMusic.IntegrationTests.Pages.Components;

public class SongsCollectionComponent(ILocator root) : CollectionComponent(root)
{
    public async Task<string> GetCellValueAsync(int rowIndex, string column)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        var cell = row.Locator($"td[data-testid^='collection-cell-{column}-']");
        return await cell.InnerTextAsync();
    }

    public async Task<long> GetSongIdAsync(int rowIndex, IAPIRequestContext api, long userId)
    {
        var title = await GetCellValueAsync(rowIndex, "title");
        var response = await api.GetAsync($"/api/songs?search={Uri.EscapeDataString(title)}");
        var data = await response.JsonAsync();
        var songs = data!.Value.GetProperty("songs");
        return songs.EnumerateArray().First().GetProperty("id").GetInt64();
    }

    public async Task<int> FindRowByTitleAsync(string title)
    {
        var rowCount = await GetRowCountAsync();
        for (int i = 0; i < rowCount; i++)
        {
            var cellTitle = await GetCellValueAsync(i, "title");
            if (cellTitle == title)
                return i;
        }
        return -1;
    }

    public async Task<SongDetailsPage> GoToSongDetailsAsync(int rowIndex)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        var titleCell = row.Locator("td[data-testid^='collection-cell-title-']");
        var anchor = titleCell.Locator("a");
        await anchor.ClickAsync();
        var page = new SongDetailsPage(Root.Page);
        await page.WaitForLoadedAsync();
        return page;
    }
}
