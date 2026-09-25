using System.Collections;
using MyMusic.IntegrationTests.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Extensions;

public static class SongVersionValuesShouldlyExtensions
{
    /// <summary>
    /// Asserts that every non-null field of <paramref name="expected"/> equals the same field of
    /// <paramref name="actual"/>. Fields left null in <paramref name="expected"/> are not checked, since
    /// revisions also carry unpredictable changes (e.g. <c>modifiedAt</c>). Lists are compared in order.
    /// </summary>
    public static void ShouldMatch(this SongVersionValues actual, SongVersionValues expected)
    {
        foreach (var property in typeof(SongVersionValues).GetProperties())
        {
            var expectedValue = property.GetValue(expected);
            if (expectedValue is null)
            {
                continue;
            }

            var actualValue = property.GetValue(actual);
            actualValue.ShouldNotBeNull($"Expected field '{property.Name}' to be present");

            if (expectedValue is IEnumerable expectedItems and not string)
            {
                ((IEnumerable)actualValue).Cast<object>()
                    .ShouldBe(expectedItems.Cast<object>(), $"Field '{property.Name}' does not match");
            }
            else
            {
                actualValue.ShouldBe(expectedValue, $"Field '{property.Name}' does not match");
            }
        }
    }
}
