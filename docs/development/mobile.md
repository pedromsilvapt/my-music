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
