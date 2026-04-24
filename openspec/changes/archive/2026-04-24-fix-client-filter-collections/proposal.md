## Why

Client-side filtering of collection fields (artists, genres, devices) is broken in the Collection component. When users apply advanced filters like `artist.name = "Taylor Swift"` on the Now Playing page, the filter silently fails to match any songs. This is due to two issues: (1) filter metadata uses singular field names (`artist.name`) while the actual data model uses plural properties (`artists[]`), and (2) the filter evaluator doesn't handle array values - it returns `undefined` when traversing collection paths.

## What Changes

- Fix `getFieldValue` function in `collection.tsx` to handle collection/array fields properly
- Map singular filter field names to plural model property names (e.g., `artist` → `artists`)
- For collection fields marked with `IsCollection: true` in filter metadata, check if ANY array element satisfies the filter condition
- Support all operators (`=`, `!=`, `contains`, `startsWith`, `endsWith`) for array values
- Add unit tests for client-side filter evaluation with collection fields

## Capabilities

### New Capabilities

- `client-filter-collections`: Client-side filtering support for array/collection fields in the Collection component's advanced filter DSL

### Modified Capabilities

(none - this is a bug fix for existing filtering capability, not a requirement change)

## Impact

- **Files Modified**:
  - `MyMusic.Client/src/components/common/collection/collection.tsx` - Core filter evaluation logic
  - `MyMusic.Common.Tests/` - New unit tests for collection field filtering
- **Affected Pages**: Now Playing page, any page using `filterMode="client"` (default)
- **Filter Metadata**: Existing `artist.name`, `genre.name`, `device.name` fields already have `IsCollection: true` flag - no backend changes needed
- **Breaking Changes**: None - this is a bug fix that makes existing filter syntax work as expected
