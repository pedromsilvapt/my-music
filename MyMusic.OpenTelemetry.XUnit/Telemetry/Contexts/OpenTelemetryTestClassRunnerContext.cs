namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestClassRunnerContext<TTestClass, TTestCase> :
    Xunit.v3.XunitTestClassRunnerBaseContext<TTestClass, TTestCase>
    where TTestClass : class, Xunit.v3.IXunitTestClass
    where TTestCase : class, Xunit.v3.IXunitTestCase
{
    public OpenTelemetryTestClassRunnerContext(
        TTestClass testClass,
        IReadOnlyCollection<TTestCase> testCases,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ITestCaseOrderer testCaseOrderer,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        Xunit.v3.FixtureMappingManager collectionFixtureMappings)
        : base(testClass, testCases, explicitOption, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
    {
    }
}