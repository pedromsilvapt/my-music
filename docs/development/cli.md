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

## Native AOT

The CLI ships as a Native AOT binary (~15x faster startup than the JIT build). `dotnet build`, `dotnet run` and the
integration tests' default `bin/Debug/net10.0/my-music` stay JIT; only `dotnet publish` compiles to native code (needs
`clang` and `zlib1g-dev`, both in the dev container).

```bash
# Native AOT (default)
dotnet publish MyMusic.CLI -c Release -r linux-x64 -o /tmp/cli-aot

# Run integration tests against a published binary
CLI_PATH=/tmp/cli-aot/my-music dotnet test MyMusic.IntegrationTests --filter "FullyQualifiedName~DesktopSyncTests"
```

The Earthfile has two build targets: `+build-aot` (default) and `+build-jit` (self-contained single-file, for distros
older than the build image's glibc 2.39). Choose which one to package with `--variant`:
`earth ./MyMusic.CLI+package --variant=jit`.

Rules to keep the AOT build working:

- **JSON goes through source-generated contexts.** Reflection-based `System.Text.Json` is disabled
  (`JsonSerializerIsReflectionEnabledByDefault=false`), even in JIT builds, so a missing type fails loudly.
  - New request/response types of `IMyMusicClient` go in `Api/CliJsonContext.cs` (`CliJsonContextTests` fails otherwise).
  - Types parsed from sync record `Data` go in `Services/Sync/SyncDataJsonContext.cs`.
  - For ad-hoc JSON, use `JsonDocument` / `System.Text.Json.Nodes` instead of `JsonSerializer` with `object`/dictionaries.
- **Trim/AOT analyzer warnings are errors** in `dotnet build` (`IL2026`, `IL2067`, `IL3050`). Don't suppress them without a
  justification that explains why the code is safe.
- **Spectre.Console.Cli is not AOT-annotated**; it discovers commands and settings via reflection, so the `my-music` and
  `Spectre.Console.Cli` assemblies are rooted (`TrimmerRootAssembly`) in the csproj.
- `dotnet publish` still reports `IL2104`/`IL3053` (and a few `IL2026`/`IL3000`) from inside Refit and Spectre. These
  are expected. Any warning pointing at MyMusic.CLI code is not.

## Device Options

The device options in the `MyMusic:Device` configuration section (icon, color, naming template, import on purchase)
are saved to the server device by `IDeviceConfigService`:

- `my-music sync` saves them before the session starts.
- `my-music sync --dry-run` leaves the server device untouched and sends the naming template with the session instead,
  so the dry run previews the local template (see "Device Options in a Dry-Run" in [sync.md](sync.md)).
- `my-music device save` saves them without running a sync.

## Sync Conflicts

A file changed differently on the device and on the server is a real conflict (see "Resolving a Real Conflict" in
[sync.md](sync.md)). `my-music sync` decides what to do with them through `--conflicts`:

| Value | Behaviour |
| --- | --- |
| `ask` | Prompts for each conflict, offering the choices the direction allows. The default. |
| `upload` | Keeps the local file: it is uploaded over the server's version. |
| `download` | Takes the server's version: it is downloaded over the local file. |
| `skip` | Leaves every conflict unresolved. The default with `--yes`, so unattended runs never wait for an answer. |

The option applies to a `--dry-run` as well, which then records what the real sync would do without doing it.

## Prompts and Progress

A live progress redraws itself several times a second, so anything else written to the console meanwhile is painted
over. `ITerminal` (`Services/Terminal/`) owns both the progress and the questions:

- `RunWithProgressAsync` runs the work with a live progress display.
- `PromptAsync` (or `AskAsync`, for a plain line of text) asks a question from anywhere in the code. While a progress
  is running, it is hidden for the question and shown again afterwards in the state it was in; its elapsed time does
  not advance meanwhile.

Never call `Console.ReadLine` or `AnsiConsole.Prompt` directly from code that can run during a sync: ask through
`ITerminal`.

## Other Development Topics

Development documentation for other MyMusic.CLI topics will be added here as the project evolves.
