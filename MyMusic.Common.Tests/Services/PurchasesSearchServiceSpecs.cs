using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Services;
using MyMusic.Common.Sources;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class PurchasesSearchServiceSpecs
{
    private const long SourceId = 1;

    private readonly ISource _source = Substitute.For<ISource>();
    private readonly PurchasesSearchService _service;

    public PurchasesSearchServiceSpecs()
    {
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(SourceId, Arg.Any<CancellationToken>()).Returns(_source);

        _service = new PurchasesSearchService(
            sourcesService,
            Options.Create(new Config { MusicRepositoryPath = "/data" }),
            Substitute.For<ILogger<PurchasesSearchService>>());
    }

    [Fact]
    public async Task SearchAsync_ModifiersAndFreeText_KeepsResultsMatchingFreeText()
    {
        // Arrange
        SetupSourceResults(CreateSong("1", "Just Keep Swimming"), CreateSong("2", "Nemo Egg"));

        // Act
        var results = await _service.SearchAsync(SourceId, "Nemo artist:UCQplaZZ2mv8fo8Zd-1hQPzg video:true");

        // Assert
        results.Select(s => s.Id).ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_OnlyModifiers_DoesNotFilterResults()
    {
        // Arrange
        SetupSourceResults(CreateSong("1", "Just Keep Swimming"), CreateSong("2", "Nemo Egg"));

        // Act
        var results = await _service.SearchAsync(SourceId, "kind:album video:true");

        // Assert
        results.Select(s => s.Id).ShouldBe(["1", "2"]);
    }

    [Fact]
    public async Task SearchAsync_QueryWithModifiers_PassesOriginalQueryToSource()
    {
        // Arrange
        const string query = "Nemo  artist:UCQplaZZ2mv8fo8Zd-1hQPzg kind:album";
        SetupSourceResults();

        // Act
        await _service.SearchAsync(SourceId, query);

        // Assert
        await _source.Received(1).SearchSongsAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_NoModifiers_FuzzyMatchesQuery()
    {
        // Arrange
        SetupSourceResults(CreateSong("1", "Just Keep Swimming"), CreateSong("2", "Nemo Egg"));

        // Act
        var results = await _service.SearchAsync(SourceId, "nemo egg");

        // Assert
        results.Select(s => s.Id).ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchForHashAsync_ModifiersAndFreeText_ReturnsIdsMatchingFreeText()
    {
        // Arrange
        SetupSourceResults(CreateSong("1", "Just Keep Swimming"), CreateSong("2", "Nemo Egg"));

        // Act
        var ids = await _service.SearchForHashAsync(SourceId, "Nemo kind:album");

        // Assert
        ids.ShouldBe(["2"]);
    }

    private void SetupSourceResults(params SourceSong[] songs) =>
        _source.SearchSongsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(songs.ToList());

    private static SourceSong CreateSong(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Artists = [new SourceSongArtist { Id = "a1", Name = "Thomas Newman" }],
        Genres = ["Soundtrack"],
    };
}
