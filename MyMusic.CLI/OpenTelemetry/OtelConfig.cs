namespace MyMusic.OpenTelemetry;

[Obsolete("This class is a copy from MyMusic.OpenTelemetry project. " +
           "Reference the MyMusic.OpenTelemetry project instead once the build system supports cross-directory project references.")]
public class OtelConfig
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:4317";
    public string Protocol { get; set; } = "grpc";
    public string? TracesEndpoint { get; set; }
    public string? LogsEndpoint { get; set; }
    public string? MetricsEndpoint { get; set; }
}
