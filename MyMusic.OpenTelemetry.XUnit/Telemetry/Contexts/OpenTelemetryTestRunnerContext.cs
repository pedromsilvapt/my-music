using System.Diagnostics;

namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class OpenTelemetryTestRunnerContext : Xunit.v3.XunitTestRunnerContext
{
    public Activity? Activity { get; internal set; }

    public OpenTelemetryTestRunnerContext(
        Xunit.v3.IXunitTest test,
        Xunit.v3.IMessageBus messageBus,
        Xunit.Sdk.ExplicitOption explicitOption,
        Xunit.v3.ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        IReadOnlyCollection<Xunit.v3.IBeforeAfterTestAttribute> beforeAfterTestAttributes,
        object?[] constructorArguments)
        : base(test, messageBus, explicitOption, aggregator, cancellationTokenSource, beforeAfterTestAttributes, constructorArguments)
    {
    }
}