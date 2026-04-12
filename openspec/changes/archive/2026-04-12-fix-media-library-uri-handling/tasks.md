## 1. URI Normalization

- [x] 1.1 Update `normalizePath()` function to preserve `content://` URIs instead of stripping prefix
- [x] 1.2 Add logic to detect content URIs and return them as-is

## 2. Asset URI Fallback

- [x] 2.1 Modify `scanFromDirectory()` to use `assetInfo.localUri || asset.uri` as fallback
- [x] 2.2 Update error handling to only throw when both `localUri` and `asset.uri` are null

## 3. Directory Filtering

- [x] 3.1 Update `isWithinDirectory()` call to skip check for content URIs
- [x] 3.2 Ensure content URIs pass through to extension/exclude pattern filtering

## 4. Relative Path Calculation

- [x] 4.1 Handle relative path calculation for content URIs using asset filename
- [x] 4.2 Ensure file:// URIs continue to calculate relative paths correctly

## 5. Testing

- [x] 5.1 Test with device where localUri returns null (requires manual testing on Android device/emulator)
- [x] 5.2 Test with device where localUri returns content:// URI (requires manual testing on Android device/emulator)
- [x] 5.3 Verify existing file:// URI scenarios still work correctly (requires manual testing on Android device/emulator)
