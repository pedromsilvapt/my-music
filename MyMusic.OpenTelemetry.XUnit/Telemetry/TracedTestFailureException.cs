using System.Diagnostics;

namespace MyMusic.OpenTelemetry.XUnit.Telemetry;

public class TracedTestFailureException : Exception
{
    public ActivityTraceId TraceId { get; }

    public override string Message => $"{InnerException!.Message}\n\n--- Trace ID: {TraceId} ---";

    public TracedTestFailureException(Exception innerException, ActivityTraceId traceId)
        : base(innerException.Message, innerException)
    {
        TraceId = traceId;
    }
}
