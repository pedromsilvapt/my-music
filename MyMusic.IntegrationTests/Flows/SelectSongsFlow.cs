using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;
using MyMusic.IntegrationTests.Pages.Components;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Selects multiple songs on the songs list page.
/// Rows are selected from bottom to top so the floating selection bar appears below the first selection.
/// </summary>
public class SelectSongsFlow(params string[] songTitles) : IFlow<SongsCollectionComponent>
{
    public async Task<SongsCollectionComponent> ExecuteAsync(IPage page)
    {
        var home = new HomePage(page);
        var songsPage = await home.Navbar.GoToSongsAsync();
        var collection = songsPage.Collection;

        var rowIndices = await FindAndSortRowIndicesAsync(collection, songTitles);

        await collection.SelectRowByIndexAsync(rowIndices[0]);
        foreach (var i in rowIndices.Skip(1))
            await collection.CtrlClickRowByIndexAsync(i);

        return collection;
    }

    private static async Task<List<int>> FindAndSortRowIndicesAsync(SongsCollectionComponent collection, string[] titles)
    {
        var rowIndices = new List<int>();
        foreach (var title in titles)
        {
            var rowIndex = await collection.FindRowByTitleAsync(title);
            rowIndex.ShouldBeGreaterThanOrEqualTo(0, $"Song '{title}' not found in collection");
            rowIndices.Add(rowIndex);
        }

        rowIndices.Sort((a, b) => b.CompareTo(a));
        return rowIndices;
    }
}
