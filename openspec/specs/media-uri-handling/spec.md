### Requirement: URI normalization handles multiple formats
The media library scanner SHALL normalize URIs from multiple formats including `file://`, `content://`, and direct file paths, preserving the original URI format when it cannot be converted to a file path.

#### Scenario: Normalizing file:// URI
- **WHEN** a `file:///storage/emulated/0/Music/song.mp3` URI is normalized
- **THEN** the result is `/storage/emulated/0/Music/song.mp3`

#### Scenario: Normalizing content:// URI
- **WHEN** a `content://media/external/audio/media/12345` URI is normalized
- **THEN** the result is `content://media/external/audio/media/12345` (preserved as-is)

#### Scenario: Normalizing direct path
- **WHEN** a `/storage/emulated/0/Music/song.mp3` path is normalized
- **THEN** the result is `/storage/emulated/0/Music/song.mp3`

### Requirement: Asset URI fallback when localUri is null
The media library scanner SHALL use `asset.uri` as a fallback when `MediaLibrary.getAssetInfoAsync()` returns `localUri` as null.

#### Scenario: localUri is null
- **WHEN** `getAssetInfoAsync` returns an asset with `localUri` set to null
- **THEN** the scanner uses `asset.uri` instead of throwing an error

#### Scenario: localUri is available
- **WHEN** `getAssetInfoAsync` returns an asset with a valid `localUri`
- **THEN** the scanner uses `localUri` as the primary URI source

### Requirement: Directory filtering skips content URIs
The media library scanner SHALL skip the repository directory filter check for `content://` URIs since they cannot be matched to file system paths.

#### Scenario: file:// URI within repository
- **WHEN** a `file:///storage/emulated/0/Music/song.mp3` URI is within the configured repository path `/storage/emulated/0/Music`
- **THEN** the file is included in scan results

#### Scenario: file:// URI outside repository
- **WHEN** a `file:///storage/emulated/0/Downloads/song.mp3` URI is outside the configured repository path `/storage/emulated/0/Music`
- **THEN** the file is excluded from scan results

#### Scenario: content:// URI
- **WHEN** a `content://media/external/audio/media/12345` URI is encountered
- **THEN** the directory filter is bypassed and the file is included (if it passes extension/exclude filters)

### Requirement: Relative path calculation handles content URIs
The media library scanner SHALL use the asset's filename for relative path calculation when the URI is a content URI.

#### Scenario: file:// URI relative path
- **WHEN** a `file:///storage/emulated/0/Music/Artist/song.mp3` URI is processed with repository `/storage/emulated/0/Music`
- **THEN** the relative path is `Artist/song.mp3`

#### Scenario: content:// URI relative path
- **WHEN** a `content://media/external/audio/media/12345` URI is processed with filename `song.mp3`
- **THEN** the relative path is `song.mp3` (using filename only)
