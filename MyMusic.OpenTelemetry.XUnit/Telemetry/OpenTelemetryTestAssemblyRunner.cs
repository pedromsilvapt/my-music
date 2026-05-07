namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestAssemblyRunner :
    Xunit.v3.XunitTestAssemblyRunnerBase<OpenTelemetryTestAssemblyRunnerContext<Xunit.v3.IXunitTestAssembly, Xunit.v3.IXunitTestCase>, Xunit.v3.IXunitTestAssembly, Xunit.v3.IXunitTestCollection, Xunit.v3.IXunitTestCase>
{
    protected OpenTelemetryTestAssemblyRunner() { }

    public static readonly OpenTelemetryTestAssemblyRunner Instance = new();

    public async ValueTask<Xunit.v3.RunSummary> Run(
        Xunit.v3.IXunitTestAssembly testAssembly,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases,
        Xunit.Sdk.IMessageSink executionMessageSink,
        Xunit.Sdk.ITestFrameworkExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        await using var ctxt = new OpenTelemetryTestAssemblyRunnerContext<Xunit.v3.IXunitTestAssembly, Xunit.v3.IXunitTestCase>(
            testAssembly, testCases, executionMessageSink, executionOptions, cancellationToken);
        
        await ctxt.InitializeAsync();
        
        return await Run(ctxt);
    }

    protected override async ValueTask<Xunit.v3.RunSummary> RunTestCollection(
        OpenTelemetryTestAssemblyRunnerContext<Xunit.v3.IXunitTestAssembly, Xunit.v3.IXunitTestCase> ctxt,
        Xunit.v3.IXunitTestCollection testCollection,
        IReadOnlyCollection<Xunit.v3.IXunitTestCase> testCases)
    {
        var testCaseOrderer = ctxt.AssemblyTestCaseOrderer ?? Xunit.v3.DefaultTestCaseOrderer.Instance;

        return await OpenTelemetryTestCollectionRunner.Instance.Run(
            testCollection,
            testCases,
            ctxt.ExplicitOption,
            ctxt.MessageBus,
            testCaseOrderer,
            ctxt.Aggregator.Clone(),
            ctxt.CancellationTokenSource,
            ctxt.AssemblyFixtureMappings);
    }
}