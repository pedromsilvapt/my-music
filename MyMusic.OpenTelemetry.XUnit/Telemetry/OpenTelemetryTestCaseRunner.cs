namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestCaseRunner :
    Xunit.v3.XunitTestCaseRunnerBase<OpenTelemetryTestCaseRunnerContext<Xunit.v3.IXunitTestCase, Xunit.v3.IXunitTest>, Xunit.v3.IXunitTestCase, Xunit.v3.IXunitTest>
{
    protected OpenTelemetryTestCaseRunner() { }

    public static readonly OpenTelemetryTestCaseRunner Instance = new();

    public async ValueTask<Xunit.v3.RunSummary> Run(
        Xunit.v3.IXunitTestCase testCase,
        IReadOnlyCollection<Xunit.v3.IXunitTest> tests,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        string displayName,
        string? skipReason,
        Xunit.Sdk.ExplicitOption explicitOption,
        object?[] constructorArguments)
    {
        await using var ctxt = new OpenTelemetryTestCaseRunnerContext<Xunit.v3.IXunitTestCase, Xunit.v3.IXunitTest>(
            testCase, tests, messageBus, aggregator, cancellationTokenSource, displayName, skipReason, explicitOption, constructorArguments);
        
        await ctxt.InitializeAsync();
        
        return await Run(ctxt);
    }

    protected override async ValueTask<Xunit.v3.RunSummary> RunTest(
        OpenTelemetryTestCaseRunnerContext<Xunit.v3.IXunitTestCase, Xunit.v3.IXunitTest> ctxt,
        Xunit.v3.IXunitTest test)
    {
        return await OpenTelemetryTestRunner.Instance.Run(
            test,
            ctxt.MessageBus,
            ctxt.ConstructorArguments,
            ctxt.ExplicitOption,
            ctxt.Aggregator.Clone(),
            ctxt.CancellationTokenSource,
            ctxt.BeforeAfterTestAttributes);
    }
}