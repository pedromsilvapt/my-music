namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestCollectionRunnerContext<TTestCollection, TTestCase> :
    Xunit.v3.XunitTestCollectionRunnerBaseContext<TTestCollection, TTestCase>
    where TTestCollection : class, Xunit.v3.IXunitTestCollection
    where TTestCase : class, Xunit.v3.IXunitTestCase
{
    public OpenTelemetryTestCollectionRunnerContext(
        TTestCollection testCollection,
        IReadOnlyCollection<TTestCase> testCases,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ITestCaseOrderer testCaseOrderer,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        Xunit.v3.FixtureMappingManager assemblyFixtureMappings)
        : base(testCollection, testCases, explicitOption, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, assemblyFixtureMappings)
    {
    }
}