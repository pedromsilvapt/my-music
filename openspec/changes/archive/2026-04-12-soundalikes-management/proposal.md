## Why

Users need a streamlined interface to manage duplicate songs detected by acoustic fingerprinting. Currently, soundalike groups are detected but there's no way to review, compare, and resolve them - users must manually delete files or use CLI tools. This creates friction and risks data loss when valuable metadata (lyrics, artwork, genre tags) exists only on duplicate tracks.

## What Changes

- **Add Bitrate field** to Song entity (currently missing)
- **Populate Bitrate** during music import process using audio file metadata
- **Create API endpoint** to fetch soundalike groups with complete song details
- **Create React component** with TypeScript type safety for displaying and managing duplicate groups
- **Implement merge workflow** where users select primary song (to keep) and secondaries (to delete), with metadata merging before deletion
- **Add "Remove Duplicates (N)" button** that processes all groups with primary selections

## Capabilities

### New Capabilities

- `soundalike-api`: Backend API endpoint to fetch soundalike groups with detailed song information, including artwork preview URLs
- `soundalike-ui`: React component page for reviewing, comparing, and resolving duplicate songs with merge-and-delete workflow
- `bitrate-storage`: Add bitrate field to Song entity and populate during import

### Modified Capabilities

None - all capabilities are new

## Impact

**Database Changes**:
- Add `Bitrate` column to Songs table (nullable int, migration required)
- Update import logic to extract and store bitrate from audio files

**Backend Changes**:
- New API endpoint: `GET /api/audits/soundalikes` - returns groups with full song details
- New API endpoint: `POST /api/audits/soundalikes/resolve` - merge metadata and delete duplicates
- Modify MusicService to extract bitrate during import

**Frontend Changes**:
- Custom page component for soundalike audit rule (referenced in SoundalikeAuditRule.CustomPage)
- Dynamic rendering in audit detail page based on rule's CustomPage property
- New React component with TypeScript types and guards
- Integration with existing artwork preview system

**Dependencies**:
- TagLib (already used) supports bitrate extraction
- Existing acoustic fingerprinting infrastructure
- Existing artwork handling system
