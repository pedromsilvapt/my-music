namespace MyMusic.CLI.Tests.Services.Sync;

using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using MyMusic.CLI.Services.Terminal;
using NSubstitute;
using Shouldly;
using Xunit;

public class CliUserPromptTests
{
    private static readonly ConflictResolution[] AllChoices =
        [ConflictResolution.Upload, ConflictResolution.Download, ConflictResolution.Skip];

    private readonly ITerminal _terminal = Substitute.For<ITerminal>();
    private readonly CliUserPrompt _userPrompt;

    public CliUserPromptTests()
    {
        _userPrompt = new CliUserPrompt(_terminal);
    }

    private void Answer(string? answer) =>
        _terminal.AskAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(answer);

    [Theory]
    [InlineData("u", ConflictResolution.Upload)]
    [InlineData(" Upload ", ConflictResolution.Upload)]
    [InlineData("d", ConflictResolution.Download)]
    [InlineData("DOWNLOAD", ConflictResolution.Download)]
    [InlineData("s", ConflictResolution.Skip)]
    [InlineData("anything else", ConflictResolution.Skip)]
    [InlineData("", ConflictResolution.Skip)]
    [InlineData(null, ConflictResolution.Skip)]
    public async Task PromptConflictResolutionAsync_MapsTheAnswer(string? answer, ConflictResolution expected)
    {
        Answer(answer);

        var resolution = await _userPrompt.PromptConflictResolutionAsync("Artist/Song.mp3", AllChoices, TestContext.Current.CancellationToken);

        resolution.ShouldBe(expected);
    }

    [Fact]
    public async Task PromptConflictResolutionAsync_AnswerOutsideTheChoices_Skips()
    {
        Answer("d");

        var resolution = await _userPrompt.PromptConflictResolutionAsync(
            "Artist/Song.mp3", [ConflictResolution.Upload, ConflictResolution.Skip], TestContext.Current.CancellationToken);

        resolution.ShouldBe(ConflictResolution.Skip);
    }

    [Fact]
    public async Task PromptConflictResolutionAsync_AsksThroughTheTerminal_OfferingOnlyTheChoices()
    {
        Answer("s");

        await _userPrompt.PromptConflictResolutionAsync(
            "Artist/Song.mp3", [ConflictResolution.Download, ConflictResolution.Skip], TestContext.Current.CancellationToken);

        await _terminal.Received(1).AskAsync(
            "Conflict detected for 'Artist/Song.mp3'. Choose one of: download, skip [d/s]: ", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData(" YES ", true)]
    [InlineData("n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task ConfirmDeletionAsync_MapsTheAnswer(string? answer, bool expected)
    {
        Answer(answer);

        var confirmed = await _userPrompt.ConfirmDeletionAsync("Artist/Song.mp3", TestContext.Current.CancellationToken);

        confirmed.ShouldBe(expected);
    }
}
