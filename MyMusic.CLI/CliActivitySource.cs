using System.Diagnostics;

namespace MyMusic.CLI;

public static class CliActivitySource
{
    public static readonly ActivitySource Instance = new("MyMusic.CLI");
}
