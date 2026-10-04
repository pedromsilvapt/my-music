namespace MyMusic.CLI.Services.Terminal;

using System.Diagnostics;

public class CliTerminal(IProgressRenderer renderer) : ITerminal
{
    private readonly SemaphoreSlim _promptLock = new(1, 1);
    private readonly Lock _lock = new();
    private ProgressSession? _session;

    public async Task<T> RunWithProgressAsync<T>(Func<ITerminalProgress, Task<T>> work)
    {
        var session = new ProgressSession();
        lock (_lock)
        {
            if (_session != null)
            {
                throw new InvalidOperationException("A progress display is already running");
            }
            _session = session;
        }

        try
        {
            Task<T> workTask;
            try
            {
                workTask = work(session);
            }
            catch (Exception ex)
            {
                workTask = Task.FromException<T>(ex);
            }

            // The live display cannot be paused, so it is shown in segments: an interruption (a prompt)
            // ends the current one, erasing it, and a new one is then built from the tasks' state
            while (true)
            {
                var interrupted = session.BeginSegment();
                var cleared = false;

                await renderer.ShowAsync(
                    session.Tasks,
                    () => Task.WhenAny(workTask, interrupted),
                    () => cleared = session.HasInterruptions);

                session.EndSegment();

                // The last segment stays on the screen, so the work cannot end on an erased one
                if (session.RunInterruptions(closeWhenNone: workTask.IsCompleted && !cleared))
                {
                    break;
                }
            }

            return await workTask;
        }
        finally
        {
            lock (_lock)
            {
                _session = null;
            }
        }
    }

    public async Task<T> PromptAsync<T>(Func<T> ask, CancellationToken ct = default)
    {
        await _promptLock.WaitAsync(ct);
        try
        {
            ProgressSession? session;
            lock (_lock)
            {
                session = _session;
            }

            var answer = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var hidesProgress = session?.TryInterrupt(() =>
            {
                try
                {
                    answer.SetResult(ask());
                }
                catch (Exception ex)
                {
                    answer.SetException(ex);
                }
            });

            return hidesProgress == true ? await answer.Task : ask();
        }
        finally
        {
            _promptLock.Release();
        }
    }

    public Task<string?> AskAsync(string question, CancellationToken ct = default) =>
        PromptAsync(() =>
        {
            Console.Write(question);
            return Console.ReadLine();
        }, ct);

    private sealed class ProgressSession : ITerminalProgress
    {
        private readonly Lock _lock = new();
        private readonly List<TerminalProgressTask> _tasks = [];
        private readonly Queue<Action> _interruptions = new();
        private readonly Stopwatch _stopwatch = new();
        private TaskCompletionSource _interrupted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _showing;
        private bool _closed;

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public IReadOnlyList<TerminalProgressTask> Tasks
        {
            get { lock (_lock) { return _tasks.ToList(); } }
        }

        public bool HasInterruptions
        {
            get { lock (_lock) { return _interruptions.Count > 0; } }
        }

        public ITerminalProgressTask AddTask(string description)
        {
            var task = new TerminalProgressTask(description);
            lock (_lock)
            {
                _tasks.Add(task);

                // A segment only shows the tasks it started with
                if (_showing)
                {
                    Interrupt(() => { });
                }
            }
            return task;
        }

        /// <summary>
        /// Starts a segment of the display. The returned task completes when it must end.
        /// </summary>
        public Task BeginSegment()
        {
            lock (_lock)
            {
                _interrupted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (_interruptions.Count > 0)
                {
                    _interrupted.TrySetResult();
                }
                _showing = true;
                _stopwatch.Start();
                return _interrupted.Task;
            }
        }

        public void EndSegment()
        {
            lock (_lock)
            {
                _showing = false;
                _stopwatch.Stop();
            }
        }

        /// <summary>
        /// Ends the current segment to run <paramref name="action"/> with nothing on the screen. Returns
        /// <c>false</c> when the progress is over, and the action will not run.
        /// </summary>
        public bool TryInterrupt(Action action)
        {
            lock (_lock)
            {
                if (_closed)
                {
                    return false;
                }

                Interrupt(action);
                return true;
            }
        }

        /// <summary>
        /// Runs the pending interruptions. With none left and <paramref name="closeWhenNone"/> set, closes
        /// the session and returns <c>true</c>.
        /// </summary>
        public bool RunInterruptions(bool closeWhenNone)
        {
            while (true)
            {
                Action? action;
                lock (_lock)
                {
                    if (!_interruptions.TryDequeue(out action))
                    {
                        _closed = closeWhenNone;
                        return _closed;
                    }
                }

                action();
            }
        }

        private void Interrupt(Action action)
        {
            _interruptions.Enqueue(action);
            _interrupted.TrySetResult();
        }
    }
}
