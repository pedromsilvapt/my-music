## 1. Core Implementation

- [x] 1.1 Add singular-to-plural field name mapping helper in collection.tsx
- [x] 1.2 Modify `getFieldValue` to detect collection fields via filter metadata
- [x] 1.3 Update `evaluateCondition` to handle array values with ANY/ALL semantics

## 2. Bug Fixes (Discovered During Verification)

- [x] 2.1 **CRITICAL:** Fix metadata field name lookup in `collection-filter.ts:93` - change `firstPart` to `fieldPath`
- [x] 2.2 Update test mock in `collection.test.ts` to use flat dot-notation fields matching real API (`artist.name` instead of nested `artist` → `nestedFields` → `name`)

## 3. Testing

- [x] 3.1 Write unit tests for collection field equality filtering (`=`, `!=`)
- [x] 3.2 Write unit tests for collection field substring operators (`contains`, `startsWith`, `endsWith`)
- [x] 3.3 Write unit tests for empty collection handling
- [x] 3.4 Write unit tests verifying non-collection fields remain unchanged
- [x] 3.5 Write integration test for Now Playing page artist filter

## 4. Verification

- [x] 4.1 Manual test: Now Playing page → filter by `artist.name contains "<artist>"`
- [x] 4.2 Manual test: Now Playing page → filter by `genre.name = "<genre>"`
- [x] 4.3 Manual test: Now Playing page → filter by `device.name != "<device>"`
- [x] 4.4 Verify Songs page (server-mode filtering) still works correctly
- [x] 4.5 Run all existing tests to ensure no regressions
