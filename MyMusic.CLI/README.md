# MyMusic CLI

Command-line interface for MyMusic sync functionality.

## Installation

```bash
dotnet build
```

## Commands

### Global Options

These options apply to all commands.

| Option       | Shortcut | Description                                                    |
|--------------|----------|----------------------------------------------------------------|
| `--loglevel` | `-l`     | Minimum log level: Trace, Debug, Information, Warning, Error, Critical |
| `--verbose`  | `-v`     | Enable console log output                                      |

The default log level is `Information`, configured via `Logging:LogLevel:Default` in appsettings.

### init

Initializes or updates the configuration file at `%APPDATA%/my-music/appsettings.json`.

```bash
my-music init [OPTIONS]
```

#### Options

| Option          | Shortcut | Description                                                              |
|-----------------|----------|--------------------------------------------------------------------------|
| `--server`      | `-s`     | Server URL                                                               |
| `--username`    | `-u`     | User name                                                                |
| `--device-name` | `-d`     | Device name                                                              |
| `--device-type` | `-t`     | Device type (Desktop, Laptop, Smartphone, Tablet, USB Drive, MP3 Player) |
| `--repository`  | `-r`     | Repository path                                                          |
| `--yes`         | `-y`     | Skip overwrite confirmation                                              |

#### Examples

```bash
# Interactive mode
my-music init

# Non-interactive (all options provided)
my-music init -s http://localhost:5000/api -u pedro -d "My Laptop" -t Laptop -r /home/pedro/Music -y
```

---

### sync

Synchronizes the local music repository with the MyMusic server.

```bash
my-music sync [OPTIONS]
```

#### Options

| Option      | Shortcut | Description                                      |
|-------------|----------|--------------------------------------------------|
| `--force`   | `-f`     | Force full sync                                  |
| `--dry-run` |          | Show what would be synced without making changes |
| `--yes`     | `-y`     | Auto-confirm prompts                             |
| `--record-skipped` |   | Keep the records of skipped (unchanged) files in the sync history; by default only their count is kept |

---

### history

Manage sync history. Contains two sub-commands.

#### history ls

List recent sync sessions.

```bash
my-music history ls [OPTIONS]
```

| Option    | Shortcut | Description                             |
|-----------|----------|-----------------------------------------|
| `--count` | `-n`     | Number of sessions to show (default: 5) |

#### history show

Show details of a sync session.

```bash
my-music history show <SESSION_ID> [OPTIONS]
```

| Option         | Shortcut | Description                  |
|----------------|----------|------------------------------|
| `--created`    | `-c`     | Show created records         |
| `--updated`    | `-u`     | Show updated records         |
| `--skipped`    | `-s`     | Show skipped records         |
| `--downloaded` | `-d`     | Show downloaded records      |
| `--removed`    | `-r`     | Show removed records         |
| `--error`      | `-e`     | Show error records (default) |
| `--all`        | `-a`     | Show all records             |
| `--device`     |          | Filter to device source      |
| `--server`     |          | Filter to server source      |

---

## Configuration

The CLI reads configuration from (in order of precedence):

1. Command-line options
2. `%APPDATA%/my-music/appsettings.json` (user config)
3. `appsettings.json` in current directory (local config)
4. `appsettings.{Environment}.json` (environment-specific)
5. Environment variables

### Sync Performance

The `MyMusic:Sync` section sets how many files each request of a sync carries. By default the sizes adjust
themselves to how fast the server answers, inside a range:

| Setting                              | Default         | Description                                                                  |
|--------------------------------------|-----------------|------------------------------------------------------------------------------|
| `AdaptiveChunks`                     | `true`          | Adjust the sizes automatically. When `false`, every request has its `Size`   |
| `TargetRequestSeconds`               | `0.5`           | How long a request should take: sizes grow below half of it, shrink above it |
| `CheckChunk`: `Size`, `Min`, `Max`   | `50, 10, 1000`  | Files per check request: starting size and range                             |
| `ResolveChunk`: `Size`, `Min`, `Max` | `200, 25, 1000` | Files per conflict resolution request: starting size and range               |

```json
{
  "MyMusic": {
    "Sync": {
      "AdaptiveChunks": true,
      "TargetRequestSeconds": 0.5,
      "CheckChunk": { "Size": 50, "Min": 10, "Max": 1000 },
      "ResolveChunk": { "Size": 200, "Min": 25, "Max": 1000 }
    }
  }
}
```

`my-music init` does not write this section: add it to the configuration file by hand to change a value.
