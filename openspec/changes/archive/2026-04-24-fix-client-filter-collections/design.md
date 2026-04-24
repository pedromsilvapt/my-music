## Context

The Collection component provides client-side filtering via a DSL (e.g., `artist.name = "Taylor Swift"`). The filter evaluator (`getFieldValue` in `collection.tsx`) traverses the item object using dot-notation paths, but doesn't handle arrays/collections properly.

**Current Flow:**
1. User types filter: `artist.name contains "Taylor"`
2. Tokenizer parses into: `{field: "artist.name", operator: "contains", value: "Taylor"}`
3. `getFieldValue(song, "artist.name", schema)` traverses:
   - `song.artist` → `undefined` (property doesn't exist, it's `song.artists[]`)
4. `evaluateCondition(undefined, "contains", "Taylor")` → `false`
5. Song incorrectly excluded from results

**Constraint:** Filter metadata already defines `IsCollection: true` for these fields. The fix must leverage this metadata.

## Goals / Non-Goals

**Goals:**
- Make `artist.name`, `genre.name`, `device.name` filters work in client-side mode
- Support all existing operators for collection fields (`=`, `!=`, `contains`, `startsWith`, `endsWith`)
- Maintain backward compatibility with non-collection fields
- Keep the fix localized to `collection.tsx`

**Non-Goals:**
- Server-side filtering changes (already works via SQL JOINs)
- New operators or filter syntax
- Changes to filter metadata definitions (backend)

## Decisions

### Decision 1: Singular-to-Plural Mapping Strategy

**Choice:** Use filter metadata's `IsCollection` flag + hardcode singular→plural name mapping.

**Rationale:** 
- Option A: Change filter metadata to use plural names (`artists.name`) → Rejected: breaks server-side filters which expect singular names for EF Core navigation
- Option B: Auto-detect arrays at runtime → Rejected: error-prone, requires type inspection
- Option C: Map via metadata + naming convention → **Chosen**: Clean, leverages existing `IsCollection` flag

**Mapping Table:**
| Filter Field    | Model Property | IsCollection |
|-----------------|----------------|--------------|
| `artist.name`   | `artists[].name` | true       |
| `genre.name`    | `genres[].name`  | true       |
| `device.name`   | `devices[].name` | true       |
| `album.name`    | `album.name`     | false      |
| `playlist.name` | N/A (not on song model in client) | true |

### Decision 2: Array Evaluation Semantics

**Choice:** For positive operators (`=`, `contains`, `startsWith`, `endsWith`), check if ANY element matches. For negative operators (`!=`), check if ALL elements DON'T match.

**Rationale:**
- `artist.name = "Taylor"` → "songs where ANY artist is Taylor" (intuitive)
- `artist.name != "Taylor"` → "songs where NO artist is Taylor" (exclusion semantics)
- Matches SQL `ANY`/`ALL` semantics for collection navigation

### Decision 3: Implementation Location

**Choice:** Modify `getFieldValue` and `evaluateCondition` in `collection.tsx`.

**Rationale:**
- Minimal change scope
- No changes to CollectionSchema interface
- Filter metadata already passed to `evaluateTokens` via `schema` parameter

## Bugs Discovered During Verification

### Bug 1: Metadata Field Name Lookup (CRITICAL)

**Location:** `collection-filter.ts:93`

**Problem:** The filter evaluator looks up metadata by `firstPart` (e.g., "artist") but the actual metadata fields use full dot-notation paths (e.g., "artist.name").

```typescript
// BROKEN CODE:
const fieldMetadata = schema.filterMetadata?.fields.find(f => f.name === firstPart);
// firstPart = "artist"
// metadata has: { name: "artist.name", isCollection: true }
// Result: undefined → isCollection check fails → wrong code path

// FIXED CODE:
const fieldMetadata = schema.filterMetadata?.fields.find(f => f.name === fieldPath);
// fieldPath = "artist.name"
// metadata has: { name: "artist.name", isCollection: true }
// Result: found → isCollection check succeeds → correct code path
```

**Impact:** All collection field filters (`artist.name`, `genre.name`, `device.name`, `playlist.name`) return no matches because `isCollection` is never detected.

### Bug 2: Test Mock Mismatch with Real API

**Location:** `collection.test.ts:14-64`

**Problem:** The test mock uses a nested metadata structure while the real API returns flat dot-notation fields.

```
TEST MOCK (incorrect):
fields: [
  { name: 'artist', isCollection: true, nestedFields: [{ name: 'name', ... }] }
]

REAL API (correct):
fields: [
  { name: 'artist.name', isCollection: true }
]
```

**Impact:** Tests pass with the broken code because the mock accidentally matches the bug's lookup pattern. The tests should use the real API metadata structure.

## Risks / Trade-offs

- **Risk:** New plural mapping breaks if filter metadata field names change → Mitigation: Document the mapping in code comments
- **Risk:** `!=` semantics may surprise users expecting "not all" vs "none" → Mitigation: Align with server-side filter behavior (verify during testing)
- **Trade-off:** Hardcoded mapping vs. metadata-driven → Chose hardcoded for simplicity; metadata-based would require backend changes
- **Risk:** Tests may drift from reality again → Mitigation: Use actual API response structure in mocks
