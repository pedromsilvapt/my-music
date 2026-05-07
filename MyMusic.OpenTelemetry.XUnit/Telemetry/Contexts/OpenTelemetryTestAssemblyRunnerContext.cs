namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestAssemblyRunnerContext<TTestAssembly, TTestCase> :
    Xunit.v3.XunitTestAssemblyRunnerBaseContext<TTestAssembly, TTestCase>
    where TTestAssembly : class, Xunit.v3.IXunitTestAssembly
    where TTestCase : class, Xunit.v3.IXunitTestCase
{
    public OpenTelemetryTestAssemblyRunnerContext(
        TTestAssembly testAssembly,
        IReadOnlyCollection<TTestCase> testCases,
        Xunit.Sdk.IMessageSink executionMessageSink,
        Xunit.Sdk.ITestFrameworkExecutionOptions executionOptions,
        CancellationToken cancellationToken)
        : base(testAssembly, testCases, executionMessageSink, executionOptions, cancellationToken)
    {
    }
}