namespace MyMusic.CLI.Services.Terminal;

using Spectre.Console;

public interface IProgressRenderer
{
    /// <summary>
    /// Shows <paramref name="tasks"/> live until the task returned by <paramref name="until"/> completes.
    /// <paramref name="clear"/> is then asked whether the display is erased from the screen, or left
    /// there as its last frame.
    /// </summary>
    Task ShowAsync(IReadOnlyList<TerminalProgressTask> tasks, Func<Task> until, Func<bool> clear);
}

public class SpectreProgressRenderer : IProgressRenderer
{
    public async Task ShowAsync(IReadOnlyList<TerminalProgressTask> tasks, Func<Task> until, Func<bool> clear)
    {
        var progress = AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn(), new ProgressBarColumn(),
                new PercentageColumn());

        await progress.StartAsync(async ctx =>
        {
            foreach (var task in tasks)
            {
                var liveTask = ctx.AddTask(task.State.Description);
                task.Attach(state =>
                {
                    liveTask.MaxValue = state.MaxValue;
                    liveTask.Value = state.Value;
                    liveTask.Description = state.Description;
                });
            }

            try
            {
                await until();
            }
            finally
            {
                foreach (var task in tasks)
                {
                    task.Detach();
                }

                // Read by Spectre when the display ends
                progress.AutoClear = clear();
            }
        });
    }
}
