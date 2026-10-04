using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Library;

/// <summary>
/// Verifies the layout of the table view shared by every collection.
/// </summary>
public class CollectionTableTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();

    [Fact]
    public async Task Table_ColumnWidths_ShouldStayStableWhileScrolling()
    {
        // Seed enough songs to virtualize the table, with titles and albums of very different lengths
        var songs = await _songs.SeedAsync(RequestContext, UserId);

        // Open the songs table and take note of the column widths at the top of the list
        var songsPage = await new HomePage(Page).Navbar.GoToSongsAsync();
        await songsPage.Collection.WaitForLoadedAsync();
        var widthsAtTop = await songsPage.Collection.GetColumnHeaderWidthsAsync();
        widthsAtTop.ShouldNotBeEmpty();

        // Scroll to the bottom, so a completely different set of rows is rendered
        await songsPage.Collection.ScrollTableToEndAsync(songs.Count);

        // The columns should not have been resized to fit the new rows
        var widthsAtBottom = await songsPage.Collection.GetColumnHeaderWidthsAsync();
        widthsAtBottom.ShouldBe(widthsAtTop);
    }
}
