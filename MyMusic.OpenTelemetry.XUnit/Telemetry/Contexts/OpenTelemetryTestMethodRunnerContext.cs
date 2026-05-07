namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestMethodRunnerContext<TTestMethod, TTestCase> :
    Xunit.v3.XunitTestMethodRunnerBaseContext<TTestMethod, TTestCase>
    where TTestMethod : class, Xunit.v3.IXunitTestMethod
    where TTestCase : class, Xunit.v3.IXunitTestCase
{
    public OpenTelemetryTestMethodRunnerContext(
        TTestMethod testMethod,
        IReadOnlyCollection<TTestCase> testCases,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.IMessageBus messageBus,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        object?[] constructorArguments)
        : base(testMethod, testCases, explicitOption, messageBus, aggregator, cancellationTokenSource, constructorArguments)
    {
    }
}