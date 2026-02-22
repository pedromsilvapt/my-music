using MyMusic.Common.Filters;
using Shouldly;

namespace MyMusic.Common.Tests.Filters;

public class FilterDslParserSpecs
{
    [Fact]
    public void Parse_EmptyString_ReturnsEmptyFilter()
    {
        var result = FilterDslParser.Parse("");

        result.Rules.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceString_ReturnsEmptyFilter()
    {
        var result = FilterDslParser.Parse("   ");

        result.Rules.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_SingleEqualsCondition_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("title = \"Test\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("title");
        condition.Operator.ShouldBe(FilterOperator.Eq);
        condition.Value.ShouldBe("Test");
    }

    [Fact]
    public void Parse_DoubleEquals_ParsesAsEquals()
    {
        var result = FilterDslParser.Parse("year == 2020");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Eq);
        condition.Value.ShouldBe(2020);
    }

    [Fact]
    public void Parse_GreaterThan_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("year > 2000");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Gt);
        condition.Value.ShouldBe(2000);
    }

    [Fact]
    public void Parse_GreaterThanOrEqual_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("year >= 2000");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Gte);
        condition.Value.ShouldBe(2000);
    }

    [Fact]
    public void Parse_LessThan_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("year < 2020");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Lt);
        condition.Value.ShouldBe(2020);
    }

    [Fact]
    public void Parse_LessThanOrEqual_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("year <= 2020");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Lte);
        condition.Value.ShouldBe(2020);
    }

    [Fact]
    public void Parse_NotEquals_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("title != \"Test\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Neq);
        condition.Value.ShouldBe("Test");
    }

    [Fact]
    public void Parse_ContainsOperator_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("title contains \"love\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Contains);
        condition.Value.ShouldBe("love");
    }

    [Fact]
    public void Parse_TildeOperator_ParsesAsContains()
    {
        var result = FilterDslParser.Parse("title ~ \"love\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Contains);
        condition.Value.ShouldBe("love");
    }

    [Fact]
    public void Parse_StartsWithOperator_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("title startsWith \"The\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.StartsWith);
        condition.Value.ShouldBe("The");
    }

    [Fact]
    public void Parse_EndsWithOperator_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("title endsWith \"Mix\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.EndsWith);
        condition.Value.ShouldBe("Mix");
    }

    [Fact]
    public void Parse_BooleanTrue_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("isFavorite = true");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Value.ShouldBe(true);
    }

    [Fact]
    public void Parse_BooleanFalse_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("explicit = false");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Value.ShouldBe(false);
    }

    [Fact]
    public void Parse_IsTrue_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("hasLyrics isTrue");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.IsTrue);
    }

    [Fact]
    public void Parse_IsFalse_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("explicit isFalse");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.IsFalse);
    }

    [Fact]
    public void Parse_BetweenOperator_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("year between 2000 and 2020");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.Between);
        condition.Value.ShouldBe(2000);
        condition.Value2.ShouldBe(2020);
    }

    [Fact]
    public void Parse_NestedProperty_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("album.name = \"Test Album\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("album.name");
        condition.Value.ShouldBe("Test Album");
    }

    [Fact]
    public void Parse_InArray_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("year in [2020, 2021, 2022]");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Operator.ShouldBe(FilterOperator.In);
        var values = condition.Value as List<object>;
        values.ShouldNotBeNull();
        values.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_AndCondition_ParsesMultipleRules()
    {
        var result = FilterDslParser.Parse("year >= 2000 and isFavorite = true");

        result.Rules.Count.ShouldBe(2);
        result.Combinator.ShouldBe(FilterCombinator.And);
    }

    [Fact]
    public void Parse_OrCondition_ParsesMultipleRules()
    {
        var result = FilterDslParser.Parse("title contains \"love\" or title contains \"heart\"");

        result.Rules.Count.ShouldBe(2);
        result.Combinator.ShouldBe(FilterCombinator.Or);
    }

    [Fact]
    public void Parse_GroupedExpression_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("(year >= 2000 and year <= 2020)");

        result.Rules.Count.ShouldBe(1);
        var group = result.Rules[0] as FilterGroupRule;
        group.ShouldNotBeNull();
        group.Rules.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_ComplexExpression_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("(year >= 2000 or isFavorite = true) and title contains \"love\"");

        result.Rules.Count.ShouldBe(2);
        var group = result.Rules[0] as FilterGroupRule;
        group.ShouldNotBeNull();
        group.Rules.Count.ShouldBe(2);

        var condition = result.Rules[1] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("title");
    }

    [Fact]
    public void Parse_FieldWithAnyQuantifier_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("artist[any].name = \"Pink Floyd\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("artist[any].name");
        condition.Operator.ShouldBe(FilterOperator.Eq);
        condition.Value.ShouldBe("Pink Floyd");
    }

    [Fact]
    public void Parse_FieldWithAllQuantifier_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("genre[all].name != \"Unknown\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("genre[all].name");
        condition.Operator.ShouldBe(FilterOperator.Neq);
        condition.Value.ShouldBe("Unknown");
    }

    [Fact]
    public void Parse_FieldWithoutQuantifier_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("artist.name = \"Pink Floyd\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("artist.name");
        condition.Quantifier.ShouldBeNull();
    }

    [Fact]
    public void Parse_NestedQuantifiers_ParsesCorrectly()
    {
        var result = FilterDslParser.Parse("song[any].genre[all].name = \"Rock\"");

        result.Rules.Count.ShouldBe(1);
        var condition = result.Rules[0] as FilterConditionRule;
        condition.ShouldNotBeNull();
        condition.Field.ShouldBe("song[any].genre[all].name");
    }
}