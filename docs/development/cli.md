# MyMusic.CLI Development Guide

## OpenTelemetry

The CLI supports OpenTelemetry tracing and logging, disabled by default.

### Configuration

Add to `appsettings.json` or set environment variables:

```json
{
  "OpenTelemetry": {
    "Enabled": false,
    "Endpoint": "http://localhost:4317",
    "Protocol": "grpc"
  }
}
```

| Environment Variable | Description | Default |
| --- | --- | --- |
| `OpenTelemetry__Enabled` | Enable OpenTelemetry | `false` |
| `OpenTelemetry__Endpoint` | OTLP base endpoint | `http://localhost:4317` |
| `OpenTelemetry__Protocol` | Export protocol (`grpc` or `http/protobuf`) | `grpc` |

### Trace Context Propagation

When the CLI is spawned by integration tests or other parent processes, trace context is propagated via the `OTEL_TRACE_PARENT` environment variable in W3C traceparent format:

```
OTEL_TRACE_PARENT=00-<trace-id>-<parent-span-id>-01
```

This allows CLI operations to be correlated with the parent trace in the observability backend (Otelite for development).

### Setup

1. Start a collector: Otelite (`docker compose up otelite`) for development, for example
2. Set `OpenTelemetry__Enabled=true`
3. For Otelite, also set `OpenTelemetry__Endpoint=http://localhost:4318` and `OpenTelemetry__Protocol=http/protobuf`
4. Run the CLI normally

### Graceful Degradation

If the OTLP endpoint is unavailable, the CLI continues operating normally without any telemetry export.

## Other Development Topics

Development documentation for other MyMusic.CLI topics will be added here as the project evolves.
