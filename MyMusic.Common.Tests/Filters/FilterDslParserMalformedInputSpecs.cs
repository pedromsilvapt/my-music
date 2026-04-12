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
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\" @@");
    }

    [Fact]
    public void InvalidChars_Only_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("@@@");
    }

    [Fact]
    public void InvalidChars_InGroup_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("(@@)");
    }

    [Fact]
    public void InvalidChars_InArray_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year in [@@]");
    }

    [Fact]
    public void Unclosed_Parenthesis_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("(title = \"test\"");
    }

    [Fact]
    public void Unclosed_Array_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year in [2020");
    }

    [Fact]
    public void Unclosed_String_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test");
    }

    [Fact]
    public void Missing_Operator_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title");
    }

    [Fact]
    public void Missing_Value_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title =");
    }

    [Fact]
    public void Invalid_Operator_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title ?? \"test\"");
    }

    [Fact]
    public void SpecialChars_Only_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("!@#$%^&*");
    }

    [Fact]
    public void Empty_Parenthesis_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("()");
    }

    [Fact]
    public void TrailingCombinator_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\" and");
    }

    [Fact]
    public void DoubleCombinator_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\" and or title = \"test2\"");
    }

    [Fact]
    public void BinaryOperatorAtStart_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("= \"test\"");
    }

    [Fact]
    public void DeeplyNested_Unclosed_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("(((((title = \"test\")");
    }

    [Fact]
    public void Mixed_InvalidChars_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\" && year > 2020");
    }

    [Fact]
    public void Newlines_InField_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("tit\nle = \"test\"");
    }

    [Fact]
    public void Unicode_InvalidChars_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\" 🎵");
    }

    [Fact]
    public void VeryLongInvalidInput_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout(new string('@', 10000));
    }

    [Fact]
    public void JustOpenParen_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("(");
    }

    [Fact]
    public void JustCloseParen_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout(")");
    }

    [Fact]
    public void JustOpenBracket_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("[");
    }

    [Fact]
    public void JustCloseBracket_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("]");
    }

    [Fact]
    public void Nested_UnclosedBrackets_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year in [[2020");
    }

    [Fact]
    public void Comma_WithoutValue_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year in [,,]");
    }

    [Fact]
    public void Operator_WithoutField_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("= 123");
    }

    [Fact]
    public void MultipleOperators_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title == != \"test\"");
    }

    [Fact]
    public void SingleDot_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout(".");
    }

    [Fact]
    public void MultipleDots_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("...");
    }

    [Fact]
    public void BackslashAtEnd_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\\");
    }

    [Fact]
    public void OnlyWhitespace_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("   ");
    }

    [Fact]
    public void TabCharacters_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("\t\t\t");
    }

    [Fact]
    public void NullCharInInput_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"te\0st\"");
    }

    [Fact]
    public void GreekLetters_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("αβγ = \"test\"");
    }

    [Fact]
    public void EmojiOnly_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("🎵🎶🎼");
    }

    [Fact]
    public void ReversedBrackets_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("][year");
    }

    [Fact]
    public void FieldWithSpecialChars_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("field$name = 123");
    }

    [Fact]
    public void NumberAsField_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("123 = 456");
    }

    [Fact]
    public void Between_WithoutSecondValue_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year between 2000");
    }

    [Fact]
    public void Between_WithoutAnd_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year between 2000 2020");
    }

    [Fact]
    public void In_WithNestedArray_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("year in [[1, 2], [3, 4]]");
    }

    [Fact]
    public void IncompleteEscapeSequence_Completes()
    {
        // Act & Assert
        AssertParseCompletesWithinTimeout("title = \"test\\");
    }
}