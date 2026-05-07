using MyMusic.IntegrationTests.Fixtures;
using Shouldly;

namespace MyMusic.IntegrationTests.Extensions;

public static class CliResultShouldlyExtensions
{
    public static void ShouldBeSuccessful(this CliResult result)
    {
        result.Success.ShouldBeTrue($"CLI failed (exit={result.ExitCode})");
    }
}
