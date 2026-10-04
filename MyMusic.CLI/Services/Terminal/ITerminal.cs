namespace MyMusic.CLI.Services.Terminal;

/// <summary>
/// The one place that shows a live progress and asks the user questions. A live progress redraws itself
/// continuously, so a question written next to it is painted over: anything that asks must go through
/// <see cref="PromptAsync{T}"/> (or <see cref="AskAsync"/>), which hides the progress while asking.
/// </summary>
public interface ITerminal
{
    /// <summary>
    /// Runs <paramref name="work"/> with a live progress display. A prompt made meanwhile hides the display
    /// and shows it again afterwards, in the state it was in.
    /// </summary>
    Task<T> RunWithProgressAsync<T>(Func<ITerminalProgress, Task<T>> work);

    /// <summary>
    /// Runs <paramref name="ask"/> with no live display on the screen, one prompt at a time. Without a
    /// running progress, it simply runs it.
    /// </summary>
    Task<T> PromptAsync<T>(Func<T> ask, CancellationToken ct = default);

    /// <summary>
    /// Writes <paramref name="question"/> and reads a line. Returns <c>null</c> when the input has ended.
    /// </summary>
    Task<string?> AskAsync(string question, CancellationToken ct = default);
}

public interface ITerminalProgress
{
    ITerminalProgressTask AddTask(string description);

    /// <summary>
    /// How long the progress has been on the screen. Does not advance while a question is open.
    /// </summary>
    TimeSpan Elapsed { get; }
}

public interface ITerminalProgressTask
{
    string Description { get; set; }
    double Value { get; set; }
    double MaxValue { get; set; }
}
