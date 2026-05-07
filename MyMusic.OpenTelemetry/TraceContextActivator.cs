using System.Diagnostics;

namespace MyMusic.OpenTelemetry;

public static class TraceContextActivator
{
    public static ActivityContext? TryParseParentContext()
    {
        var traceParent = Environment.GetEnvironmentVariable("OTEL_TRACE_PARENT");
        if (string.IsNullOrEmpty(traceParent))
        {
            return null;
        }

        try
        {
            var parts = traceParent.Split('-');
            if (parts.Length != 4 || parts[0] != "00")
            {
                return null;
            }

            var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
            var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
            var flags = parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

            return new ActivityContext(traceId, spanId, flags);
        }
        catch
        {
            return null;
        }
    }
}
