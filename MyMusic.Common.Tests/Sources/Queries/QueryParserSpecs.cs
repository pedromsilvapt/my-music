using MyMusic.Common.Sources.Queries;
using Shouldly;

namespace MyMusic.Common.Tests.Sources.Queries;

public class QueryParserSpecs
{
    public enum Color
    {
        Red,
        Blue,
    }

    public sealed record TestQuery
    {
        [QueryText]
        public string Rest { get; init; } = "";

        [QueryParam("color", "colour")]
        public Color? Color { get; init; }

        [QueryParam("name")]
        public string? Name { get; init; }
    }

    public sealed record DuplicateLabelQuery
    {
        [QueryParam("name")]
        public string? First { get; init; }

        [QueryParam("NAME")]
        public string? Second { get; init; }
    }

    [Fact]
    public void Parse_SetsAnnotatedProperties()
    {
        var query = QueryParser.Parse<TestQuery>("hello COLOUR:blue name:x color:2 world");

        query.ShouldBe(new TestQuery { Rest = "hello color:2 world", Color = Color.Blue, Name = "x" });
    }

    [Fact]
    public void Parse_MissingParamsKeepDefaults()
    {
        QueryParser.Parse<TestQuery>("hello").ShouldBe(new TestQuery { Rest = "hello" });
    }

    [Fact]
    public void Parse_DuplicateLabels_Throws()
    {
        Should.Throw<InvalidOperationException>(() => QueryParser.Parse<DuplicateLabelQuery>("name:x"));
    }

    [Fact]
    public void Upsert_AppendsWhenMissing()
    {
        QueryParser.Upsert("Nemo", "artist", "UC1").ShouldBe("Nemo artist:UC1");
    }

    [Fact]
    public void Upsert_ReplacesInPlaceAndDropsDuplicates()
    {
        QueryParser.Upsert("a artist:A b ARTIST:C", "artist", "B").ShouldBe("a artist:B b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Upsert_EmptyValue_Removes(string? value)
    {
        QueryParser.Upsert("a artist:A b artist:C", "artist", value).ShouldBe("a b");
    }

    [Fact]
    public void Upsert_KeepsOtherTokensAsIs()
    {
        QueryParser.Upsert("  Re:Zero  foo:bar Nemo ", "kind", "album").ShouldBe("Re:Zero foo:bar Nemo kind:album");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("a:b")]
    [InlineData("a1")]
    public void Upsert_InvalidKey_Throws(string key)
    {
        Should.Throw<ArgumentException>(() => QueryParser.Upsert("Nemo", key, "x"));
    }

    [Fact]
    public void Upsert_ValueWithWhitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => QueryParser.Upsert("Nemo", "artist", "a b"));
    }

    [Fact]
    public void Upsert_RoundTripsThroughParse()
    {
        var query = QueryParser.Upsert("Nemo name:A colour:blue Re:Zero", "name", "x");

        QueryParser.Parse<TestQuery>(query)
            .ShouldBe(new TestQuery { Rest = "Nemo Re:Zero", Color = Color.Blue, Name = "x" });
    }

    [Fact]
    public void RemoveModifiers_KeepsFreeTextInOrder()
    {
        QueryParser.RemoveModifiers("Finding artist:UC1 Nemo kind:album OST").ShouldBe("Finding Nemo OST");
    }

    [Fact]
    public void RemoveModifiers_OnlyModifiers_ReturnsEmpty()
    {
        QueryParser.RemoveModifiers("kind:album video:true").ShouldBe("");
    }

    [Fact]
    public void RemoveModifiers_NoModifiers_NormalizesWhitespace()
    {
        QueryParser.RemoveModifiers("  Finding \t Nemo\n OST ").ShouldBe("Finding Nemo OST");
    }

    [Fact]
    public void RemoveModifiers_UnknownKeys_AreRemoved()
    {
        QueryParser.RemoveModifiers("Re:Zero Nemo whatever:value").ShouldBe("Nemo");
    }

    [Theory]
    [InlineData(":foo")]
    [InlineData("foo:")]
    [InlineData("a1:b")]
    [InlineData("-a:b")]
    public void RemoveModifiers_NonModifierTokens_AreKept(string token)
    {
        QueryParser.RemoveModifiers($"Nemo {token}").ShouldBe($"Nemo {token}");
    }
}
