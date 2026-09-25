using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Models;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Validates the revision currently shown in the open song version modal: its number, navigation state,
/// and the field changes shown in its old/new panels. Leaves the modal open.
/// </summary>
public class ValidateCurrentSongVersionFlow(ValidateCurrentSongVersionOptions expected) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var modal = new SongDetailsPage(page).VersionModal;

        if (expected.Revision is not null)
        {
            var revision = await modal.GetRevisionAsync();
            revision.ShouldBe(expected.Revision.Value);
        }

        var oldValues = await modal.GetOldValuesAsync();
        if (expected.HasOldPanel is not null)
        {
            (oldValues is not null).ShouldBe(expected.HasOldPanel.Value,
                expected.HasOldPanel.Value
                    ? "A later revision should show the previous values"
                    : "The first revision should not show an old panel");
        }

        if (expected.Old is not null)
        {
            oldValues.ShouldNotBeNull("The version should have an old panel");
            oldValues.ShouldMatch(expected.Old);
        }

        if (expected.New is not null)
        {
            var newValues = await modal.GetNewValuesAsync();
            newValues.ShouldMatch(expected.New);
        }

        if (expected.CanGoToPrevious is not null)
        {
            var canGoToPrevious = await modal.CanGoToPreviousAsync();
            canGoToPrevious.ShouldBe(expected.CanGoToPrevious.Value);
        }

        if (expected.CanGoToNext is not null)
        {
            var canGoToNext = await modal.CanGoToNextAsync();
            canGoToNext.ShouldBe(expected.CanGoToNext.Value);
        }
    }
}

/// <param name="Revision">Revision number shown in the modal header.</param>
/// <param name="HasOldPanel">Whether the old panel is shown (false only for the first revision).</param>
/// <param name="Old">Field values expected in the old panel (only non-null fields are checked).</param>
/// <param name="New">Field values expected in the new panel (only non-null fields are checked).</param>
/// <param name="CanGoToPrevious">Whether navigating to an older revision is enabled.</param>
/// <param name="CanGoToNext">Whether navigating to a newer revision is enabled.</param>
public record ValidateCurrentSongVersionOptions(
    int? Revision = null,
    bool? HasOldPanel = null,
    SongVersionValues? Old = null,
    SongVersionValues? New = null,
    bool? CanGoToPrevious = null,
    bool? CanGoToNext = null);
