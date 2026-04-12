## Context

The MyMusic.Mobile app uses `expo-media-library` to scan music files on Android devices. The scanner calls `MediaLibrary.getAssetInfoAsync()` to get the `localUri` property for each asset. However:

1. **Null localUri**: Some Android devices/versions return `null` for `localUri`
2. **Content URIs**: Some scenarios return `content://media/external/audio/media/12345` instead of a `file://` path
3. **Current behavior**: The scanner throws "Could not get local file path for media asset" and skips the file

According to expo-media-library documentation, `asset.uri` is **always** a `file://` URI on Android, even when `localUri` is null or a content URI. This provides a reliable fallback.

The `expo-file-system` `File` class can handle both `file://` and `content://` URIs for reading operations.

## Goals / Non-Goals

**Goals:**
- Enable sync for devices where `localUri` is null by using `asset.uri` fallback
- Enable sync for content URIs by preserving them for File operations
- Maintain backward compatibility with existing working scenarios

**Non-Goals:**
- Converting content URIs to file system paths (would require native code)
- Changing how uploads work (FormData already handles URIs)
- Modifying file scanner behavior (only media library scanner affected)

## Decisions

### Decision 1: Use `asset.uri` as primary fallback

**Chosen**: Use `assetInfo.localUri || asset.uri` as the source URI

**Alternatives considered**:
1. **Copy to cache**: Copy content URIs to cache directory first - rejected because it requires extra storage and time
2. **Native resolution**: Write native code to resolve content URIs - rejected as too complex for this bug fix
3. **Asset.uri only**: Always use asset.uri - rejected because localUri may have more accurate path when available

**Rationale**: `asset.uri` is guaranteed to be a valid `file://` URI on Android per expo-media-library docs. This is the simplest fix with minimal code changes.

### Decision 2: Preserve content URIs instead of stripping prefix

**Chosen**: Return content URIs as-is from `normalizePath()`

**Alternatives considered**:
1. **Strip content:// prefix**: Currently implemented, breaks because the remaining string is not a valid path
2. **Throw error**: Reject content URIs - rejected, excludes valid files

**Rationale**: The `expo-file-system` `File` class can handle content URIs directly. Stripping the prefix creates invalid paths.

### Decision 3: Skip directory filtering for content URIs

**Chosen**: Only apply `isWithinDirectory()` check for non-content URIs

**Alternatives considered**:
1. **Use album info**: Match content URIs to albums then to repository path - rejected as too complex
2. **Include all content URIs**: Skip directory check entirely for content URIs - **chosen**

**Rationale**: Content URIs cannot be matched to file system directories. Since media library already filters by media type (audio), we include all content URIs that pass extension/exclude pattern filters.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Content URIs may include files outside user's intended repository | Users can still use file system scanner mode for precise control; media library mode trades precision for speed |
| File upload may fail with content URIs on some devices | Test on multiple devices; fallback to file system scanner if issues persist |
| Relative path calculation may be incorrect for content URIs | Use filename from asset for relative path when URI is content:// |

## Migration Plan

No migration needed - this is a bug fix that only affects previously broken scenarios. Users who had working sync will see no change; users with broken sync will now have working sync.
