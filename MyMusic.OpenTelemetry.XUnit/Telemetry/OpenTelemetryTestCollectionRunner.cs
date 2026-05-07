namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestCollectionRunner :
    Xunit.v3.XunitTestCollectionRunnerBase<OpenTelemetryTestCollectionRunnerContext<Xunit.v3.IXunitTestCollection, Xunit.v3.IXunitTestCase>, Xunit.v3.IXunitTestCollection, Xunit.v3.IXunitTestClass, Xunit.v3.IXunitTestCase>
{
    protected OpenTelemetryTestCollectionRunner() { }

    public static readonly OpenTelemetryTestCollectionRunner Instance = new();

    public async ValueTask<Xunit.v3.RunSummary> Run(
        Xunit.v3.IXunitTestCollection testCollection,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ITestCaseOrderer testCaseOrderer,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        Xunit.v3.FixtureMappingManager assemblyFixtureMappings)
    {
        await using var ctxt = new OpenTelemetryTestCollectionRunnerContext<Xunit.v3.IXunitTestCollection, Xunit.v3.IXunitTestCase>(
            testCollection, testCases, explicitOption, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, assemblyFixtureMappings);
        
        await ctxt.InitializeAsync();
        
        return await Run(ctxt);
    }

    protected override async ValueTask<Xunit.v3.RunSummary> RunTestClass(
        OpenTelemetryTestCollectionRunnerContext<Xunit.v3.IXunitTestCollection, Xunit.v3.IXunitTestCase> ctxt,
        Xunit.v3.IXunitTestClass? testClass,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases)
    {
        if (testClass is null)
        {
            return Xunit.v3.XunitRunnerHelper.FailTestCases(
                ctxt.MessageBus,
                ctxt.CancellationTokenSource,
                testCases,
                "Test case '{0}' does not have an associated class and cannot be run by XunitTestClassRunner",
                sendTestClassMessages: true,
                sendTestMethodMessages: true);
        }

        return await OpenTelemetryTestClassRunner.Instance.Run(
            testClass,
            testCases,
            ctxt.ExplicitOption,
            ctxt.MessageBus,
            ctxt.TestCaseOrderer,
            ctxt.Aggregator.Clone(),
            ctxt.CancellationTokenSource,
            ctxt.CollectionFixtureMappings);
    }
}