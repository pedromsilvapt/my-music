using System.Diagnostics;
using MyMusic.OpenTelemetry.XUnit;

namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestRunner :
    Xunit.v3.XunitTestRunnerBase<OpenTelemetryTestRunnerContext, Xunit.v3.IXunitTest>
{
    internal static readonly ActivitySource ActivitySource = IntegrationTestTelemetry.ActivitySource;

    protected OpenTelemetryTestRunner() { }

    public static readonly OpenTelemetryTestRunner Instance = new();

    public async ValueTask<Xunit.v3.RunSummary> Run(
        Xunit.v3.IXunitTest test,
        Xunit.v3.IMessageBus messageBus,
        object?[] constructorArguments,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        IReadOnlyCollection<Xunit.v3.IBeforeAfterTestAttribute> beforeAfterAttributes)
    {
        await using var ctxt = new OpenTelemetryTestRunnerContext(
            test, messageBus, explicitOption, aggregator, cancellationTokenSource, beforeAfterAttributes, constructorArguments);
        
        await ctxt.InitializeAsync();
        
        using var activity = ActivitySource.StartActivity(test.TestDisplayName);
        ctxt.Activity = activity;
        
        activity?.SetTag("test.framework", "xunit");
        activity?.SetTag("test.class", test.TestCase.TestClass.Class.Name);
        activity?.SetTag("test.method", test.TestCase.TestMethod.Method.Name);
        activity?.SetTag("test.display_name", test.TestDisplayName);
        
        return await Run(ctxt);
    }

    protected override async ValueTask<TimeSpan> InvokeTest(
        OpenTelemetryTestRunnerContext ctxt,
        object? testClassInstance)
    {
        TimeSpan timeTaken;

        try
        {
            timeTaken = await base.InvokeTest(ctxt, testClassInstance);
        }
        catch (Exception ex)
        {
            ctxt.Activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            ctxt.Activity?.AddException(ex);
            throw;
        }

        if (ctxt.Aggregator.HasExceptions)
        {
            var ex = ctxt.Aggregator.ToException();
            ctxt.Activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            ctxt.Activity?.AddException(ex);
            ctxt.Activity?.SetTag("test.result", "failed");
            ctxt.Activity?.SetTag("test.exception.type", ex.GetType().FullName);
            ctxt.Activity?.SetTag("test.exception.message", ex.Message);
            ctxt.Activity?.SetTag("test.exception.stackTrace", ex.StackTrace);
        }
        else
        {
            ctxt.Activity?.SetStatus(ActivityStatusCode.Ok);
            ctxt.Activity?.SetTag("test.result", "passed");
        }

        ctxt.Activity?.SetTag("test.duration_ms", timeTaken.TotalMilliseconds);

        return timeTaken;
    }
}
