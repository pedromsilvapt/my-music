namespace MyMusic.CLI.Tests.Services.Terminal;

using MyMusic.CLI.Services.Terminal;
using Shouldly;
using Xunit;

public class CliTerminalTests
{
    private readonly FakeProgressRenderer _renderer = new();
    private readonly CliTerminal _terminal;

    public CliTerminalTests()
    {
        _terminal = new CliTerminal(_renderer);
    }

    [Fact]
    public async Task PromptAsync_WithoutProgress_RunsTheQuestion()
    {
        var answer = await _terminal.PromptAsync(() => "d", TestContext.Current.CancellationToken);

        answer.ShouldBe("d");
        _renderer.Segments.ShouldBeEmpty();
    }

    [Fact]
    public async Task PromptAsync_DuringProgress_HidesTheProgressAndRestoresItsState()
    {
        bool? showingWhileAsking = null;

        var result = await _terminal.RunWithProgressAsync(async progress =>
        {
            var task = progress.AddTask("Resolving conflicts: 40/100");
            task.MaxValue = 100;
            task.Value = 40;
            await _renderer.FirstSegmentShown;

            return await _terminal.PromptAsync(() =>
            {
                showingWhileAsking = _renderer.IsShowing;
                return "d";
            });
        });

        result.ShouldBe("d");
        showingWhileAsking.ShouldBe(false);

        // The progress was erased for the question, then shown again as it was and left on the screen
        var expected = new TerminalTaskState("Resolving conflicts: 40/100", 40, 100);
        _renderer.Segments.Count.ShouldBe(2);
        _renderer.Segments[0].Cleared.ShouldBe(true);
        _renderer.Segments[0].StateAtEnd.ShouldBe([expected]);
        _renderer.Segments[1].StateAtStart.ShouldBe([expected]);
        _renderer.Segments[1].Cleared.ShouldBe(false);
    }

    [Fact]
    public async Task PromptAsync_TaskChangedWhileHidden_ShowsTheChangeAfterwards()
    {
        await _terminal.RunWithProgressAsync(async progress =>
        {
            var task = progress.AddTask("Uploading");
            await _renderer.FirstSegmentShown;

            return await _terminal.PromptAsync(() =>
            {
                task.Value = 7;
                task.Description = "Downloading";
                return true;
            });
        });

        _renderer.Segments[1].StateAtStart.ShouldBe([new TerminalTaskState("Downloading", 7, 100)]);
    }

    [Fact]
    public async Task PromptAsync_DuringProgress_DoesNotAdvanceElapsed()
    {
        var before = TimeSpan.Zero;
        var after = TimeSpan.Zero;

        await _terminal.RunWithProgressAsync(async progress =>
        {
            progress.AddTask("Uploading");
            await _renderer.FirstSegmentShown;

            return await _terminal.PromptAsync(() =>
            {
                before = progress.Elapsed;
                Thread.Sleep(50);
                after = progress.Elapsed;
                return true;
            });
        });

        after.ShouldBe(before);
    }

    [Fact]
    public async Task PromptAsync_ConcurrentQuestions_AreAskedOneAtATime()
    {
        var asking = 0;
        var maxAsking = 0;

        int Ask(int answer)
        {
            maxAsking = Math.Max(maxAsking, Interlocked.Increment(ref asking));
            Thread.Sleep(20);
            Interlocked.Decrement(ref asking);
            return answer;
        }

        var answers = await _terminal.RunWithProgressAsync(async progress =>
        {
            progress.AddTask("Uploading");
            await _renderer.FirstSegmentShown;

            return await Task.WhenAll(
                Task.Run(() => _terminal.PromptAsync(() => Ask(1))),
                Task.Run(() => _terminal.PromptAsync(() => Ask(2))),
                Task.Run(() => _terminal.PromptAsync(() => Ask(3))));
        });

        answers.ShouldBe([1, 2, 3]);
        maxAsking.ShouldBe(1);
        _renderer.Segments.Last().Cleared.ShouldBe(false);
    }

    [Fact]
    public async Task PromptAsync_QuestionThrows_FailsTheCallerAndResumesTheProgress()
    {
        var result = await _terminal.RunWithProgressAsync(async progress =>
        {
            progress.AddTask("Uploading");
            await _renderer.FirstSegmentShown;

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => _terminal.PromptAsync<string>(() => throw new InvalidOperationException("no terminal")));

            return exception.Message;
        });

        result.ShouldBe("no terminal");
        _renderer.Segments.Count.ShouldBe(2);
        _renderer.Segments[1].Cleared.ShouldBe(false);
    }

    [Fact]
    public async Task PromptAsync_AfterProgressEnded_RunsTheQuestion()
    {
        await _terminal.RunWithProgressAsync(progress =>
        {
            progress.AddTask("Uploading");
            return Task.FromResult(true);
        });

        var answer = await _terminal.PromptAsync(() => "s", TestContext.Current.CancellationToken);

        answer.ShouldBe("s");
        _renderer.Segments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunWithProgressAsync_WorkThrows_PropagatesAndAllowsANewProgress()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => _terminal.RunWithProgressAsync<bool>(async progress =>
        {
            progress.AddTask("Uploading");
            await _renderer.FirstSegmentShown;
            throw new InvalidOperationException("sync failed");
        }));

        var result = await _terminal.RunWithProgressAsync(_ => Task.FromResult(true));

        result.ShouldBe(true);
    }

    [Fact]
    public async Task AddTask_WhileShowing_ShowsTheNewTask()
    {
        await _terminal.RunWithProgressAsync(async progress =>
        {
            progress.AddTask("Uploading");
            await _renderer.FirstSegmentShown;

            progress.AddTask("Downloading");
            return true;
        });

        _renderer.Segments[0].StateAtStart.Select(s => s.Description).ShouldBe(["Uploading"]);
        _renderer.Segments.Last().StateAtStart.Select(s => s.Description).ShouldBe(["Uploading", "Downloading"]);
        _renderer.Segments.Last().Cleared.ShouldBe(false);
    }

    private sealed class Segment
    {
        public required List<TerminalTaskState> StateAtStart { get; init; }
        public List<TerminalTaskState> StateAtEnd { get; set; } = [];
        public bool? Cleared { get; set; }
    }

    /// <summary>
    /// Records each segment of the display, instead of drawing it.
    /// </summary>
    private sealed class FakeProgressRenderer : IProgressRenderer
    {
        private readonly TaskCompletionSource _firstSegmentShown = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Segment> Segments { get; } = [];
        public bool IsShowing { get; private set; }
        public Task FirstSegmentShown => _firstSegmentShown.Task;

        public async Task ShowAsync(IReadOnlyList<TerminalProgressTask> tasks, Func<Task> until, Func<bool> clear)
        {
            var segment = new Segment { StateAtStart = tasks.Select(t => t.State).ToList() };
            Segments.Add(segment);

            IsShowing = true;
            _firstSegmentShown.TrySetResult();
            await until();
            IsShowing = false;

            segment.StateAtEnd = tasks.Select(t => t.State).ToList();
            segment.Cleared = clear();
        }
    }
}
