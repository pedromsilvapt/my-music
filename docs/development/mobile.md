# MyMusic.Mobile Development Guide

MyMusic.Mobile is a React Native (Expo) mobile application that provides the same sync functionality as MyMusic.CLI but
with a mobile-friendly interface.

## Technology Stack

- **Framework**: Expo SDK 55 with Expo Router for navigation
- **Language**: TypeScript
- **State Management**: Zustand for UI state only
- **Config Management**: Centralized configService (single source of truth)
- **UI**: React Native built-in components with custom styling
- **API Client**: Manual fetch with Zod for validation
- **Storage**: AsyncStorage for config, SecureStore for credentials

## Project Structure

```
MyMusic.Mobile/
├── app/                          # Expo Router routes (file-based routing)
│   ├── _layout.tsx               # Root layout
│   ├── index.tsx                 # Home/Dashboard screen
│   ├── settings/
│   │   ├── index.tsx             # Settings main
│   │   └── device.tsx            # Device configuration
│   ├── history/
│   │   ├── index.tsx             # Sessions list
│   │   └── [sessionId].tsx       # Session detail
│   └── sync/
│       └── progress.tsx          # Active sync screen
├── src/
│   ├── api/                      # API client & types
│   │   ├── client.ts             # Base fetch wrapper with auth
│   │   ├── devices.ts            # Device API functions
│   │   ├── sync.ts               # Sync API functions
│   │   └── types.ts              # Zod schemas
│   ├── stores/                   # Zustand stores (UI state only)
│   │   ├── configStore.ts        # UI loading state
│   │   ├── authStore.ts          # Auth state
│   │   └── syncStore.ts          # Sync progress state
│   ├── services/                 # Business logic
│   │   ├── configService.ts      # Centralized config management (single source of truth)
│   │   ├── fileScanner.ts        # Music file scanner
│   │   └── syncService.ts        # Core sync orchestration
│   ├── components/ui/            # Reusable UI components
│   └── constants/                # Theme & device icons
├── modules/
│   ├── xxhash/                   # Local native module: XXH3-128 file hashing (Android, JNI)
│   └── repo-files/               # Local native module: writes to the music repository in shared storage (Android)
└── app.json
```

## Use configService for All Config Access

All configuration (server URL, device settings, user info) should be accessed through `configService`. Never directly
modify AsyncStorage or use separate stores for persisted config.

```typescript
// Good - use configService for all config access
import { getServerUrl, setServerUrl, getUserName, getDeviceId, ... } from './services/configService';

// Bad - don't use separate stores for persisted config
import { useConfigStore } from './stores/configStore';  // Only for UI state
```

The configService provides:

- **Single source of truth** - All config goes through one service
- **Automatic sync** - Setting a value updates both runtime AND storage
- **Type-safe** - All config access goes through proper getters/setters

## Device Options

The device options set on the device settings screen (icon, naming template, import on purchase) live in
`configService` and are saved to the server device in three places:

- **Saving the settings screen** calls `saveDeviceConfig()` (`services/deviceConfigService.ts`), which registers the
  device or saves the options that differ. If the server can't be reached, the settings are kept locally and the user is
  told they were not saved to the server.
- **A real sync** saves them before the session starts (`saveDeviceOptionsPhase`).
- **A dry run** leaves the server device untouched and sends the naming template with the session instead, so the dry
  run previews the local template (see "Device Options in a Dry-Run" in [sync.md](sync.md)).

The server replaces every option on update, so always send the full set; `saveDeviceOptions`
(`services/sync/device-options.ts`) sends the server's color back, as the app has no setting for it.

## Exclusion Rules

The "Excluded Files" field of the device settings screen holds the rules that keep local paths out of the sync, one
per line (syntax and behaviour in "Exclusion Rules" in [sync.md](sync.md)). They are stored in `configService`
(`getExcludePatterns` / `setExcludePatterns`) and stay on the device: nothing is saved to the server device.

`createExclusionMatcher` (`services/sync/exclusions.ts`) builds the matcher. The scanners skip the paths it matches,
and the sync context carries it (`ctx.isExcluded`) so the actions in `services/sync/sync-actions-device.ts` fail
every server action on one (`reportExcluded`).

## Session Counters

The session cards (history list and session details, which is also where a finished sync lands) show the record counts
through `SessionCounters` (`components/ui/SessionCounters.tsx`), in six slots defined in
`services/sync/sessionCounters.ts`:

| Slot     | Actions                                       |
|----------|-----------------------------------------------|
| Remote   | CreateRemote, UpdateRemote                    |
| Local    | CreateLocal, UpdateLocal, DeleteLocal, Rename |
| Links    | Link, Unlink, UpdateTimestamp                 |
| Conflict | Conflict                                      |
| Error    | Error                                         |
| Skipped  | Skipped                                       |

A slot shows an icon and value, each in its own color, for every action that has records, or a single `0` when none
has. Every sync record action must belong to a slot (a test enforces it), so a session with records never shows only
zeros.

## Conflict Prompt

When a sync finds a real conflict (see "Resolving a Real Conflict" in [sync.md](sync.md)), `createDefaultUserPrompt`
(`services/sync/defaults.ts`) shows a dialog with the choices the sync direction allows: Upload, Download and Skip.
The dialog is shown in a dry run as well. With "Treat Conflicts as Errors" on, nothing is asked and every conflict is
left unresolved. `actionConflict` (`services/sync/sync-actions-device.ts`) applies the answer: downloads are sent to the
server in one `conflict-choices` request per check chunk, uploads go through `actionUpdateRemote` with the id of the
conflict they resolve.

## Uploads

`uploadFile` (`api/sync.ts`) sends the file part with a content type taken from its extension. React Native on
Android fails the request without sending it when a file part has none ("Binary FormData part needs a content-type
header"), so a new extension in `getMusicExtensions` needs an entry in `UPLOAD_CONTENT_TYPES`, or it is sent as
`application/octet-stream`.

A file that fails to upload is reported to the server by `actionCreateRemote` / `actionUpdateRemote`
(`services/sync/sync-actions-device.ts`) and shows in the session as an `Error` record.

## Running the App

```bash
# Install dependencies
cd MyMusic.Mobile && npm install

# Start Metro bundler
npm start

# Run on iOS simulator
npm run ios

# Run on Android emulator
npm run android

# Build for production
npx expo prebuild
npx expo run:android
```

## Local Native Modules

`modules/` holds native code that ships with the app. The `android/` folder is gitignored and regenerated by
`expo prebuild`, so native code cannot live there; Expo autolinks every module under `modules/` instead.

- **`modules/xxhash`** — `hashFile(uri)` computes the XXH3-128 checksum of a file (base64 of the canonical bytes, the
  same value the server stores as `XxHash128`). Sync uses it to resolve conflicts by sending only the checksum instead
  of uploading the file. It is the official `xxhash.h` (vendored, unmodified) behind a small JNI function
  (`android/src/main/cpp/xxhash_jni.cpp`), exposed through `XxhashModule.kt`. Android only.

- **`modules/repo-files`** — the file operations that write to the music repository: `downloadFile`, `ensureDirectory`,
  `moveFile`, `copyFile`, `deleteFile`, plus `hasAllFilesAccess` / `requestAllFilesAccess`. The repository lives in
  shared storage (e.g. `/storage/emulated/0/Music`), where expo-file-system cannot write: it decides permissions with
  `File.canRead()` / `File.canWrite()`, which are false for a file that does not exist yet, so every download, move or
  copy to a new path is rejected with "Missing 'WRITE' permission". The module uses `java.io.File` directly
  (`RepoFilesModule.kt`) and relies on Android's **All files access** (`MANAGE_EXTERNAL_STORAGE`, declared in
  `app.json`). `src/services/storageAccess.ts` checks the grant before a non-dry-run sync and when the repository
  folder is picked, and sends the user to the system settings screen when it is missing. Reads (existence, listing,
  modification time) still go through expo-file-system. Android only.

Adding or changing a native module needs a new dev build: run `npm run android`. A Metro reload only updates the
JavaScript, so the app would keep running the old native code (or fail with "Cannot find native module 'Xxhash'").

The module has no instrumented test. Its output must match the shared checksum vectors pinned in
`MyMusic.Common.Tests/Services/ChecksumServiceSpecs.cs`; to check it on a device, touch the modification time of a
synced file without changing its content and sync: the record must be `UpdateTimestamp`, not `Conflict`.

## Key Features

1. **Configuration**: Set server URL, username, device name, device type, and repository path
2. **Sync Music**: Upload local music to server, download server music to device
3. **View History**: See past sync sessions with detailed records
4. **Progress Tracking**: Real-time sync progress with counts and ETA

## API Integration

The mobile app uses the same API endpoints as the web client and CLI:

- Device management: `/api/devices`
- Sync operations: `/api/devices/{deviceId}/sync/*`
- Sessions: `/api/devices/{deviceId}/sessions`

Authentication is handled via headers (`X-MyMusic-UserId`, `X-MyMusic-UserName`) stored securely.
