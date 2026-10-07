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

    private const string AllHint = "(add \"all\" to answer every remaining conflict, \"none\" skips them all)";

    [Theory]
    [InlineData("u", ConflictResolution.Upload)]
    [InlineData(" Upload ", ConflictResolution.Upload)]
    [InlineData("d", ConflictResolution.Download)]
    [InlineData("DOWNLOAD", ConflictResolution.Download)]
    [InlineData("s", ConflictResolution.Skip)]
    [InlineData("anything else", ConflictResolution.Skip)]
    [InlineData("all", ConflictResolution.Skip)]
    [InlineData("", ConflictResolution.Skip)]
    [InlineData(null, ConflictResolution.Skip)]
    public async Task PromptConflictResolutionAsync_MapsTheAnswer(string? answer, ConflictResolution expected)
    {
        Answer(answer);

        var resolution = await _userPrompt.PromptConflictResolutionAsync("Artist/Song.mp3", AllChoices, TestContext.Current.CancellationToken);

        resolution.ShouldBe(new PromptAnswer<ConflictResolution>(expected));
    }

    [Theory]
    [InlineData("ua", ConflictResolution.Upload)]
    [InlineData("u all", ConflictResolution.Upload)]
    [InlineData(" Upload All ", ConflictResolution.Upload)]
    [InlineData("da", ConflictResolution.Download)]
    [InlineData("download all", ConflictResolution.Download)]
    [InlineData("sa", ConflictResolution.Skip)]
    [InlineData("skip all", ConflictResolution.Skip)]
    [InlineData("none", ConflictResolution.Skip)]
    public async Task PromptConflictResolutionAsync_AnswerForAll_AppliesToAll(string answer, ConflictResolution expected)
    {
        Answer(answer);

        var resolution = await _userPrompt.PromptConflictResolutionAsync("Artist/Song.mp3", AllChoices, TestContext.Current.CancellationToken);

        resolution.ShouldBe(new PromptAnswer<ConflictResolution>(expected, ApplyToAll: true));
    }

    [Theory]
    [InlineData("d")]
    [InlineData("download all")]
    public async Task PromptConflictResolutionAsync_AnswerOutsideTheChoices_SkipsThisConflictOnly(string answer)
    {
        Answer(answer);

        var resolution = await _userPrompt.PromptConflictResolutionAsync(
            "Artist/Song.mp3", [ConflictResolution.Upload, ConflictResolution.Skip], TestContext.Current.CancellationToken);

        resolution.ShouldBe(new PromptAnswer<ConflictResolution>(ConflictResolution.Skip));
    }

    [Fact]
    public async Task PromptConflictResolutionAsync_AsksThroughTheTerminal_OfferingOnlyTheChoices()
    {
        Answer("s");

        await _userPrompt.PromptConflictResolutionAsync(
            "Artist/Song.mp3", [ConflictResolution.Download, ConflictResolution.Skip], TestContext.Current.CancellationToken);

        await _terminal.Received(1).AskAsync(
            $"Conflict detected for 'Artist/Song.mp3'. Choose one of: download, skip [d/s] {AllHint}: ", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("y", true, false)]
    [InlineData(" YES ", true, false)]
    [InlineData("n", false, false)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    [InlineData("a", true, true)]
    [InlineData(" All ", true, true)]
    [InlineData("o", false, true)]
    [InlineData("NONE", false, true)]
    public async Task ConfirmDeletionAsync_MapsTheAnswer(string? answer, bool expected, bool expectedForAll)
    {
        Answer(answer);

        var confirmed = await _userPrompt.ConfirmDeletionAsync("Artist/Song.mp3", TestContext.Current.CancellationToken);

        confirmed.ShouldBe(new PromptAnswer<bool>(expected, expectedForAll));
    }

    [Fact]
    public async Task ConfirmDeletionAsync_AsksThroughTheTerminal_OfferingAllAndNone()
    {
        Answer("n");

        await _userPrompt.ConfirmDeletionAsync("Artist/Song.mp3", TestContext.Current.CancellationToken);

        await _terminal.Received(1).AskAsync("Delete 'Artist/Song.mp3'? [y]es, [n]o, [a]ll, n[o]ne: ", Arg.Any<CancellationToken>());
    }
}
