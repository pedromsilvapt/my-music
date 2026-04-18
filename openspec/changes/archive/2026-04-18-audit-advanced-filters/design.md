## Context

The audit detail pages (for non-soundalike rules) currently use `filterMode="client"` on the Collection component, which only supports basic text search against a `searchVector`. The songs page already has a mature server-side advanced filter system with a Monaco-based DSL editor, IntelliSense completions, and dynamic autocomplete values driven by `/api/songs/filter-metadata` and `/api/songs/filter-values` endpoints. The same architecture needs to be brought to audit non-conformities.

The audit non-conformity data is song-based (each has an associated `Song` with title, artists, album, etc.) plus audit-specific fields (hasWaiver, createdAt). The backend's `ListNonConformities` endpoint currently returns all non-conformities for a rule without filtering.

## Goals / Non-Goals

**Goals:**
- Enable users to filter audit non-conformities using the same DSL filter editor used on the songs page
- Support server-side filtering so large audit result sets don't need to be loaded entirely
- Provide filter autocomplete (field names, operators, values) via filter-metadata and filter-values endpoints
- Maintain consistency with the existing filter architecture (same `FilterCodeEditor`, `useFilterMetadata`, `DynamicFilterBuilder`, `FilterDslParser` pipeline)

**Non-Goals:**
- Filtering on the soundalike audit page (it has a custom component, not the Collection)
- Changing the existing DSL syntax or filter editor UI
- Adding pagination to the non-conformities endpoint (separate concern)
- Adding new audit rule types or changing audit behavior

## Decisions

### 1. Filter endpoint URL pattern: `/api/audits/rules/{id}/non-conformities/filter-metadata` and `/filter-values`

**Rationale**: The non-conformity filter is scoped to a specific audit rule, matching the existing pattern for device session records (`/api/devices/{id}/sessions/{id}/records/filter-metadata`). The filter fields and their dynamic values are the same regardless of rule ID (since they all describe song non-conformities), but the URL must include the rule ID for consistency and to scope the `filter-values` query to the owner's data.

**Alternative considered**: `/api/audits/filter-metadata` (rule-agnostic). Rejected because filter-values must be scoped to the owner, and keeping the rule ID in the path allows for potential rule-specific filtering in the future.

### 2. Reuse the songs filter metadata for audit non-conformity fields, plus audit-specific additions

**Rationale**: Audit non-conformities have all the song fields (via the included Song navigation) plus `hasWaiver` and `createdAt` (detection date). Rather than duplicating song fields, define the metadata explicitly to include: `title`, `album.name`, `artist.name`, `genre.name`, `year`, `explicit`, `isFavorite`, `hasWaiver`, `createdAt`. This mirrors how song field metadata is defined but adds audit-specific fields.

**Alternative considered**: Inheriting song metadata and appending. Rejected because the audit context doesn't need all song fields (e.g., `label`, `device.name`, `playlist.name`, `searchableText`, `durationSeconds` aren't relevant), and explicit definition is clearer.

### 3. Server-side filtering via `filter` query parameter on the existing non-conformities endpoint

**Rationale**: Add `[FromQuery] string? filter` to `ListNonConformities`, parse with `FilterDslParser`, build expression with `DynamicFilterBuilder`, and apply as `.Where()` on the query. This is the same pattern used in `SongsController.List()`.

**Alternative considered**: Create a separate filtered endpoint. Rejected because it's unnecessary—the existing endpoint can accept an optional filter parameter.

### 4. Add `'audits'` to `FilterEntity` type and mapping in `useFilterMetadata`

**Rationale**: The simplest approach to wire the frontend. The `useFilterMetadata` hook uses a lookup table mapping entity type to endpoint URL. Adding `'audits'` with a parameterized URL pattern that includes the rule ID requires a small extension to the hook.

**Decision**: Extend `useFilterMetadata` to accept an optional `entityId` parameter for entities that need path-scoped metadata (like audits). The endpoint map entry for audits will be `/api/audits/rules/{id}/non-conformities/filter-metadata`, and the hook will substitute the ID.

### 5. Frontend: Change `AuditDefaultComponent` to use `filterMode="server"`

**Rationale**: The Collection component already supports `filterMode="server"` with `serverSearch`, `serverFilter`, and `onServerFilterChange` props. The audit detail page needs to manage the filter state and pass it down, similar to how `SongsPage` uses `useCollectionStateByKey`.

## Risks / Trade-offs

- **Performance**: Server-side filtering on non-conformities requires joining with Song data. The existing `IncludeSongMetadata` extension method should handle this, but large result sets could be slow. → Mitigation: EF Core's `AsSplitQuery()` is already used, and the filter is applied at the database level.
- **Filter DSL on joined data**: The `DynamicFilterBuilder` needs entity path mappings for Song-related fields accessed through `nc.Song.*`. This is a new mapping but follows the same pattern as songs. → Mitigation: Define explicit mappings (`title` → `Song.Title`, `album.name` → `Song.Album.Name`, etc.).
- **Breaking change**: Adding `filter` query parameter to existing endpoint is additive (optional), so no breaking change.