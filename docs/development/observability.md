# Observability (OpenTelemetry)

This project uses OpenTelemetry for distributed tracing and logging with Otelite - a lightweight SQLite-based collector. HTTP-only (port 4318).

## Starting Otelite

```bash
docker compose up otelite
```

Or run the binary directly:

```bash
./tools/otelite server -port 4318 -db otel.db
```

Otelite receives OTLP data on `http://localhost:4318`. Query stored data:

```bash
# List recent traces
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT service_name, span_name, trace_id FROM traces ORDER BY id DESC LIMIT 10"

# View logs
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT service_name, severity_text, body FROM logs ORDER BY id DESC LIMIT 20"

# Find traces by service
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT * FROM traces WHERE service_name = 'MyMusic.Server'"
```

### Configuring for Otelite

When using Otelite, set the protocol to HTTP/protobuf (only supports HTTP):

```bash
export OpenTelemetry__Enabled=true
export OpenTelemetry__Endpoint=http://localhost:4318
export OpenTelemetry__Protocol=http/protobuf
```

Or in `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "Endpoint": "http://localhost:4318",
    "Protocol": "http/protobuf"
  }
}
```

## Querying by Trace ID

```bash
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT * FROM traces WHERE trace_id = '<trace-id>'"
```

## Filtering Logs by Service

```bash
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT * FROM logs WHERE service_name = 'MyMusic.Server'"
```

## Interpreting Trace Timeline

A complete trace from an integration test execution shows:
1. **Test span** (MyMusic.IntegrationTests) - Top-level test execution
2. **Server spans** (MyMusic.Server) - API request processing, database queries
3. **CLI spans** (MyMusic.CLI) - Sync operations, HTTP calls back to server

All spans are correlated under a single trace ID via W3C Trace Context propagation.

## Environment Variables

Environment variables use the .NET configuration naming convention with double underscores (`__`) to map to the configuration structure:

| Variable | Description | Default |
| --- | --- | --- |
| `OpenTelemetry__Enabled` | Enable OpenTelemetry (all components) | `false` |
| `OpenTelemetry__Endpoint` | OTLP base endpoint (signals append `/v1/traces`, `/v1/logs`, `/v1/metrics`) | `http://localhost:4318` |
| `OpenTelemetry__Protocol` | Export protocol (`grpc` or `http/protobuf`) | `http/protobuf` |
| `OpenTelemetry__TracesEndpoint` | Override traces endpoint (optional) | _(Endpoint + /v1/traces)_ |
| `OpenTelemetry__LogsEndpoint` | Override logs endpoint (optional) | _(Endpoint + /v1/logs)_ |
| `OpenTelemetry__MetricsEndpoint` | Override metrics endpoint (optional) | _(Endpoint + /v1/metrics)_ |
| `OTEL_TRACE_PARENT` | W3C traceparent for context propagation (CLI) | _(none)_ |

**Example:**

```bash
# Enable OpenTelemetry with Otelite (HTTP protocol)
export OpenTelemetry__Enabled=true
export OpenTelemetry__Endpoint=http://localhost:4318
export OpenTelemetry__Protocol=http/protobuf
```

## Troubleshooting

### Otelite not receiving data

1. Verify otelite is running: `docker compose ps otelite`
2. Verify `OpenTelemetry__Enabled=true` is set
3. Verify `OpenTelemetry__Endpoint=http://localhost:4318` (HTTP port)
4. Verify `OpenTelemetry__Protocol=http/protobuf` (otelite only supports HTTP)
5. Check server/CLI startup logs for OpenTelemetry initialization errors

### Tests failing with OpenTelemetry enabled

- If the collector is not running, tests should still pass (graceful degradation)
- If tests fail, set `OpenTelemetry__Enabled=false` to rule out telemetry issues
- Integration tests generate their own trace IDs and propagate them via `traceparent` headers

### Devcontainer setup

The devcontainer Dockerfile includes `OpenTelemetry__Enabled=false`, `OpenTelemetry__Endpoint=http://localhost:4318`, and `OpenTelemetry__Protocol=http/protobuf` by default, configured for Otelite. To enable telemetry, set:

```bash
export OpenTelemetry__Enabled=true
```

To use a gRPC collector instead, override:

```bash
export OpenTelemetry__Endpoint=http://localhost:4317
export OpenTelemetry__Protocol=grpc
```
