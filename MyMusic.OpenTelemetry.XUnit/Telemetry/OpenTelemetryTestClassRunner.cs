namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestClassRunner :
    Xunit.v3.XunitTestClassRunnerBase<OpenTelemetryTestClassRunnerContext<Xunit.v3.IXunitTestClass, Xunit.v3.IXunitTestCase>, Xunit.v3.IXunitTestClass, Xunit.v3.IXunitTestMethod, Xunit.v3.IXunitTestCase>
{
    protected OpenTelemetryTestClassRunner() { }

    public static readonly OpenTelemetryTestClassRunner Instance = new();

    public async ValueTask<Xunit.v3.RunSummary> Run(
        Xunit.v3.IXunitTestClass testClass,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ITestCaseOrderer testCaseOrderer,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        Xunit.v3.FixtureMappingManager collectionFixtureMappings)
    {
        await using var ctxt = new OpenTelemetryTestClassRunnerContext<Xunit.v3.IXunitTestClass, Xunit.v3.IXunitTestCase>(
            testClass, testCases, explicitOption, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings);
        
        await ctxt.InitializeAsync();
        
        return await ctxt.Aggregator.RunAsync(() => Run(ctxt), default);
    }

    protected override async ValueTask<Xunit.v3.RunSummary> RunTestMethod(
        OpenTelemetryTestClassRunnerContext<Xunit.v3.IXunitTestClass, Xunit.v3.IXunitTestCase> ctxt,
        Xunit.v3.IXunitTestMethod? testMethod,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases,
        object?[] constructorArguments)
    {
        if (testMethod is null)
        {
            return Xunit.v3.XunitRunnerHelper.FailTestCases(
                ctxt.MessageBus,
                ctxt.CancellationTokenSource,
                testCases,
                "Test case '{0}' does not have an associated method and cannot be run by XunitTestMethodRunner",
                sendTestMethodMessages: true);
        }

        return await OpenTelemetryTestMethodRunner.Instance.Run(
            testMethod,
            testCases,
            ctxt.ExplicitOption,
            ctxt.MessageBus,
            ctxt.Aggregator.Clone(),
            ctxt.CancellationTokenSource,
            constructorArguments);
    }
}