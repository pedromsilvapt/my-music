namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestMethodRunner :
    Xunit.v3.XunitTestMethodRunnerBase<OpenTelemetryTestMethodRunnerContext<Xunit.v3.IXunitTestMethod, Xunit.v3.IXunitTestCase>, Xunit.v3.IXunitTestMethod, Xunit.v3.IXunitTestCase>
{
    protected OpenTelemetryTestMethodRunner() { }

    public static readonly OpenTelemetryTestMethodRunner Instance = new();

    public async ValueTask<Xunit.v3.RunSummary> Run(
        Xunit.v3.IXunitTestMethod testMethod,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        object?[] constructorArguments)
    {
        await using var ctxt = new OpenTelemetryTestMethodRunnerContext<Xunit.v3.IXunitTestMethod, Xunit.v3.IXunitTestCase>(
            testMethod, testCases, explicitOption, messageBus, aggregator, cancellationTokenSource, constructorArguments);
        
        await ctxt.InitializeAsync();
        
        return await Run(ctxt);
    }

    protected override async ValueTask<Xunit.v3.RunSummary> RunTestCase(
        OpenTelemetryTestMethodRunnerContext<Xunit.v3.IXunitTestMethod, Xunit.v3.IXunitTestCase> ctxt,
        Xunit.v3.IXunitTestCase testCase)
    {
        var tests = await testCase.CreateTests();

        return await OpenTelemetryTestCaseRunner.Instance.Run(
            testCase,
            tests,
            ctxt.MessageBus,
            ctxt.Aggregator.Clone(),
            ctxt.CancellationTokenSource,
            testCase.TestCaseDisplayName,
            testCase.SkipReason,
            ctxt.ExplicitOption,
            ctxt.ConstructorArguments);
    }
}