## Why

The MyMusic.Mobile app's media library scanner fails to sync files when `MediaLibrary.getAssetInfoAsync()` returns `localUri` as `null` or as a `content://` URI instead of a `file://` URI. This results in all scanned files showing "Could not get local file path for media asset" errors during sync, making the media library sync mode unusable for many Android devices.

## What Changes

- **Fallback URI handling**: When `localUri` is `null`, use `asset.uri` as fallback (which is guaranteed to be a `file://` URI on Android)
- **Content URI preservation**: Keep `content://` URIs as-is instead of incorrectly stripping the prefix, since expo-file-system's `File` class can handle them
- **Directory check skip**: Skip repository directory filtering for `content://` URIs since they can't be matched to file system paths

## Capabilities

### New Capabilities

- `media-uri-handling`: Capability to handle multiple URI formats (file://, content://, direct paths) in media library scanning, with proper fallback and normalization logic

### Modified Capabilities

None - this is a bug fix for existing functionality, not a behavior change.

## Impact

- **Affected Code**: `MyMusic.Mobile/src/services/mediaLibraryScanner.ts`
- **Affected Functions**: `scanFromDirectory`, `normalizePath`, `isWithinDirectory`
- **User Impact**: Users with devices that return null localUri or content URIs will now be able to sync their music files using media library mode
- **Backward Compatibility**: Fully backward compatible - existing working scenarios continue to work, broken scenarios now work
