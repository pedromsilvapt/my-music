namespace MyMusic.CLI.Services.Terminal;

public readonly record struct TerminalTaskState(string Description, double Value, double MaxValue);

/// <summary>
/// The state of a progress task, kept apart from the live display: the display comes and goes around
/// prompts, and each new one is rebuilt from this state.
/// </summary>
public sealed class TerminalProgressTask(string description) : ITerminalProgressTask
{
    private readonly Lock _lock = new();
    private TerminalTaskState _state = new(description, 0, 100);
    private Action<TerminalTaskState>? _display;

    public TerminalTaskState State
    {
        get { lock (_lock) { return _state; } }
    }

    public string Description
    {
        get => State.Description;
        set => Update(state => state with { Description = value });
    }

    public double Value
    {
        get => State.Value;
        set => Update(state => state with { Value = value });
    }

    public double MaxValue
    {
        get => State.MaxValue;
        set => Update(state => state with { MaxValue = value });
    }

    /// <summary>
    /// Connects a live display: <paramref name="display"/> receives the current state now, and every
    /// change until <see cref="Detach"/>.
    /// </summary>
    public void Attach(Action<TerminalTaskState> display)
    {
        lock (_lock)
        {
            _display = display;
            display(_state);
        }
    }

    public void Detach()
    {
        lock (_lock)
        {
            _display = null;
        }
    }

    private void Update(Func<TerminalTaskState, TerminalTaskState> change)
    {
        lock (_lock)
        {
            _state = change(_state);
            _display?.Invoke(_state);
        }
    }
}
