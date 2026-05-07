namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestCaseRunnerContext<TTestCase, TTest> :
    Xunit.v3.XunitTestCaseRunnerBaseContext<TTestCase, TTest>
    where TTestCase : class, Xunit.v3.IXunitTestCase
    where TTest : class, Xunit.v3.IXunitTest
{
    public OpenTelemetryTestCaseRunnerContext(
        TTestCase testCase,
        IReadOnlyCollection<TTest> tests,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        string displayName,
        string? skipReason,
        Xunit.Sdk.ExplicitOption explicitOption,
        object?[] constructorArguments)
        : base(testCase, tests, messageBus, aggregator, cancellationTokenSource, displayName, skipReason, explicitOption, constructorArguments)
    {
    }
}