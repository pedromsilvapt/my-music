using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class AuditRuleFieldMapperSpecs
{
    private readonly AuditRuleFieldMapper _mapper = new();

    [Theory]
    [InlineData(1, new[] { "cover" })]      // MissingCover
    [InlineData(2, new[] { "year" })]       // MissingYear
    [InlineData(3, new[] { "genres" })]     // MissingGenres
    [InlineData(4, new[] { "lyrics" })]     // MissingLyrics
    [InlineData(5, new[] { "cover" })]      // MediumCover
    [InlineData(6, new[] { "cover" })]      // SmallCover
    [InlineData(7, new[] { "cover" })]      // NonJpegCover
    [InlineData(8, new[] { "cover" })]      // NonSquareCover
    public void GetFieldsForRule_KnownRule_ReturnsExpectedFields(long ruleId, string[] expectedFields)
    {
        // Act
        var fields = _mapper.GetFieldsForRule(ruleId);

        // Assert
        fields.ShouldBe(expectedFields, ignoreOrder: true);
    }

    [Fact]
    public void GetFieldsForRule_UnknownRule_ReturnsEmptyList()
    {
        // Arrange
        long unknownRuleId = 999;

        // Act
        var fields = _mapper.GetFieldsForRule(unknownRuleId);

        // Assert
        fields.ShouldBeEmpty();
    }

    [Fact]
    public void GetFieldsForRule_MissingYearRule_ReturnsYearField()
    {
        // Act
        var fields = _mapper.GetFieldsForRule(2);

        // Assert
        fields.Count.ShouldBe(1);
        fields[0].ShouldBe("year");
    }

    [Fact]
    public void GetFieldsForRule_CoverRelatedRules_ReturnsCoverField()
    {
        // Cover-related rules: 1, 5, 6, 7, 8
        var coverRules = new[] { 1L, 5, 6, 7, 8 };

        foreach (var ruleId in coverRules)
        {
            var fields = _mapper.GetFieldsForRule(ruleId);
            fields.ShouldContain("cover");
        }
    }

    [Fact]
    public void GetFieldsForRules_MultipleRules_ReturnsCombinedUniqueFields()
    {
        // Arrange
        var ruleIds = new[] { 1L, 2, 3 }; // cover, year, genres

        // Act
        var fields = _mapper.GetFieldsForRules(ruleIds);

        // Assert
        fields.Count.ShouldBe(3);
        fields.ShouldContain("cover");
        fields.ShouldContain("year");
        fields.ShouldContain("genres");
    }

    [Fact]
    public void GetFieldsForRules_DuplicateFields_ReturnsUniqueFieldsOnly()
    {
        // Arrange - multiple rules that all map to "cover"
        var ruleIds = new[] { 1L, 5, 6 }; // all cover-related

        // Act
        var fields = _mapper.GetFieldsForRules(ruleIds);

        // Assert
        fields.Count.ShouldBe(1);
        fields[0].ShouldBe("cover");
    }

    [Fact]
    public void GetFieldsForRules_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var ruleIds = Array.Empty<long>();

        // Act
        var fields = _mapper.GetFieldsForRules(ruleIds);

        // Assert
        fields.ShouldBeEmpty();
    }

    [Fact]
    public void GetFieldsForRules_UnknownRules_ReturnsEmptyList()
    {
        // Arrange
        var ruleIds = new[] { 999L, 1000 };

        // Act
        var fields = _mapper.GetFieldsForRules(ruleIds);

        // Assert
        fields.ShouldBeEmpty();
    }

    [Fact]
    public void GetFieldsForRules_MixOfKnownAndUnknown_ReturnsKnownFieldsOnly()
    {
        // Arrange
        var ruleIds = new[] { 1L, 999, 2 }; // cover, unknown, year

        // Act
        var fields = _mapper.GetFieldsForRules(ruleIds);

        // Assert
        fields.Count.ShouldBe(2);
        fields.ShouldContain("cover");
        fields.ShouldContain("year");
    }
}
