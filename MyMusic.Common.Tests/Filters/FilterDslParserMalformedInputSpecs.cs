using System.Diagnostics;
using MyMusic.Common.Filters;
using Shouldly;

namespace MyMusic.Common.Tests.Filters;

public class FilterDslParserMalformedInputSpecs
{
    private const int TimeoutMs = 1000;

    private static void AssertParseCompletesWithinTimeout(string input)
    {
        var sw = Stopwatch.StartNew();
        Exception? caughtException = null;

        var task = Task.Run(() =>
        {
            try
            {
                FilterDslParser.Parse(input);
            }
            catch (FormatException ex)
            {
                caughtException = ex;
            }
            catch (InvalidOperationException ex)
            {
                caughtException = ex;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                caughtException = ex;
            }
            catch (IndexOutOfRangeException ex)
            {
                caughtException = ex;
            }
        });

        var completed = task.Wait(TimeoutMs);
        sw.Stop();

        completed.ShouldBeTrue(
            $"Parser appears stuck in infinite loop for input: '{EscapeInput(input)}' " +
            $"(elapsed: {sw.ElapsedMilliseconds}ms, expected < {TimeoutMs}ms)");
    }

    private static string EscapeInput(string input) =>
        input.Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

    [Fact]
    public void InvalidChars_AfterValidCondition_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\" @@");

    [Fact]
    public void InvalidChars_Only_Completes()
        => AssertParseCompletesWithinTimeout("@@@");

    [Fact]
    public void InvalidChars_InGroup_Completes()
        => AssertParseCompletesWithinTimeout("(@@)");

    [Fact]
    public void InvalidChars_InArray_Completes()
        => AssertParseCompletesWithinTimeout("year in [@@]");

    [Fact]
    public void Unclosed_Parenthesis_Completes()
        => AssertParseCompletesWithinTimeout("(title = \"test\"");

    [Fact]
    public void Unclosed_Array_Completes()
        => AssertParseCompletesWithinTimeout("year in [2020");

    [Fact]
    public void Unclosed_String_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test");

    [Fact]
    public void Missing_Operator_Completes()
        => AssertParseCompletesWithinTimeout("title");

    [Fact]
    public void Missing_Value_Completes()
        => AssertParseCompletesWithinTimeout("title =");

    [Fact]
    public void Invalid_Operator_Completes()
        => AssertParseCompletesWithinTimeout("title ?? \"test\"");

    [Fact]
    public void SpecialChars_Only_Completes()
        => AssertParseCompletesWithinTimeout("!@#$%^&*");

    [Fact]
    public void Empty_Parenthesis_Completes()
        => AssertParseCompletesWithinTimeout("()");

    [Fact]
    public void TrailingCombinator_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\" and");

    [Fact]
    public void DoubleCombinator_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\" and or title = \"test2\"");

    [Fact]
    public void BinaryOperatorAtStart_Completes()
        => AssertParseCompletesWithinTimeout("= \"test\"");

    [Fact]
    public void DeeplyNested_Unclosed_Completes()
        => AssertParseCompletesWithinTimeout("(((((title = \"test\")");

    [Fact]
    public void Mixed_InvalidChars_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\" && year > 2020");

    [Fact]
    public void Newlines_InField_Completes()
        => AssertParseCompletesWithinTimeout("tit\nle = \"test\"");

    [Fact]
    public void Unicode_InvalidChars_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\" 🎵");

    [Fact]
    public void VeryLongInvalidInput_Completes()
        => AssertParseCompletesWithinTimeout(new string('@', 10000));

    [Fact]
    public void JustOpenParen_Completes()
        => AssertParseCompletesWithinTimeout("(");

    [Fact]
    public void JustCloseParen_Completes()
        => AssertParseCompletesWithinTimeout(")");

    [Fact]
    public void JustOpenBracket_Completes()
        => AssertParseCompletesWithinTimeout("[");

    [Fact]
    public void JustCloseBracket_Completes()
        => AssertParseCompletesWithinTimeout("]");

    [Fact]
    public void Nested_UnclosedBrackets_Completes()
        => AssertParseCompletesWithinTimeout("year in [[2020");

    [Fact]
    public void Comma_WithoutValue_Completes()
        => AssertParseCompletesWithinTimeout("year in [,,]");

    [Fact]
    public void Operator_WithoutField_Completes()
        => AssertParseCompletesWithinTimeout("= 123");

    [Fact]
    public void MultipleOperators_Completes()
        => AssertParseCompletesWithinTimeout("title == != \"test\"");

    [Fact]
    public void SingleDot_Completes()
        => AssertParseCompletesWithinTimeout(".");

    [Fact]
    public void MultipleDots_Completes()
        => AssertParseCompletesWithinTimeout("...");

    [Fact]
    public void BackslashAtEnd_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\\");

    [Fact]
    public void OnlyWhitespace_Completes()
        => AssertParseCompletesWithinTimeout("   ");

    [Fact]
    public void TabCharacters_Completes()
        => AssertParseCompletesWithinTimeout("\t\t\t");

    [Fact]
    public void NullCharInInput_Completes()
        => AssertParseCompletesWithinTimeout("title = \"te\0st\"");

    [Fact]
    public void GreekLetters_Completes()
        => AssertParseCompletesWithinTimeout("αβγ = \"test\"");

    [Fact]
    public void EmojiOnly_Completes()
        => AssertParseCompletesWithinTimeout("🎵🎶🎼");

    [Fact]
    public void ReversedBrackets_Completes()
        => AssertParseCompletesWithinTimeout("][year");

    [Fact]
    public void FieldWithSpecialChars_Completes()
        => AssertParseCompletesWithinTimeout("field$name = 123");

    [Fact]
    public void NumberAsField_Completes()
        => AssertParseCompletesWithinTimeout("123 = 456");

    [Fact]
    public void Between_WithoutSecondValue_Completes()
        => AssertParseCompletesWithinTimeout("year between 2000");

    [Fact]
    public void Between_WithoutAnd_Completes()
        => AssertParseCompletesWithinTimeout("year between 2000 2020");

    [Fact]
    public void In_WithNestedArray_Completes()
        => AssertParseCompletesWithinTimeout("year in [[1, 2], [3, 4]]");

    [Fact]
    public void IncompleteEscapeSequence_Completes()
        => AssertParseCompletesWithinTimeout("title = \"test\\");
}