using OpenTelemetry.Exporter;

namespace MyMusic.OpenTelemetry;

public class OtelConfig
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:4317";
    public string Protocol { get; set; } = "grpc";
    public string? TracesEndpoint { get; set; }
    public string? LogsEndpoint { get; set; }
    public string? MetricsEndpoint { get; set; }
    public int ForceFlushTimeoutMilliseconds { get; set; } = 30000;
}
