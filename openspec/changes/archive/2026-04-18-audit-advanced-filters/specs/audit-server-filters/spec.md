## ADDED Requirements

### Requirement: Audit non-conformities filter metadata endpoint
The system SHALL expose a `GET /api/audits/rules/{id}/non-conformities/filter-metadata` endpoint that returns field definitions and operators for filtering audit non-conformities.

The response SHALL include fields for: `title` (string, song title, dynamic values), `album.name` (string, dynamic values), `artist.name` (string, collection, dynamic values), `genre.name` (string, collection, dynamic values), `year` (number), `explicit` (boolean), `isFavorite` (boolean), `hasWaiver` (boolean), `createdAt` (date).

The response SHALL include all standard filter operators via `FilterMetadataHelper.GetOperatorMetadata()`.

#### Scenario: Fetching filter metadata for an audit rule
- **WHEN** a GET request is made to `/api/audits/rules/{id}/non-conformities/filter-metadata`
- **THEN** the system returns a `FilterMetadataResponse` with field definitions for all supported non-conformity filter fields and the standard operator set

### Requirement: Audit non-conformities filter values endpoint
The system SHALL expose a `GET /api/audits/rules/{id}/non-conformities/filter-values` endpoint with `field`, `search` (optional), and `limit` (default 15) query parameters that returns distinct values for the specified field, scoped to the current owner.

For fields with `supportsDynamicValues`, the endpoint SHALL return autocomplete values from the database. For boolean fields (`hasWaiver`, `explicit`, `isFavorite`), the endpoint SHALL return `["true", "false"]`. For other fields without dynamic values, the endpoint SHALL return an empty list.

#### Scenario: Fetching dynamic values for a song title field
- **WHEN** a GET request is made to `/api/audits/rules/{id}/non-conformities/filter-values?field=title&search=great&limit=15`
- **THEN** the system returns distinct song titles matching the search, owned by the current user, ordered alphabetically, limited to 15 results

#### Scenario: Fetching values for a boolean field
- **WHEN** a GET request is made to `/api/audits/rules/{id}/non-conformities/filter-values?field=hasWaiver`
- **THEN** the system returns `["true", "false"]`

### Requirement: Server-side filter parameter on non-conformities endpoint
The `GET /api/audits/rules/{id}/non-conformities` endpoint SHALL accept an optional `filter` query parameter containing a filter DSL expression. When provided, the system SHALL parse the expression using `FilterDslParser`, resolve entity paths using the audit non-conformity field mappings, and apply the resulting filter expression to the query before returning results.

Field mappings for audit non-conformities SHALL map: `title` → `Song.Title`, `album.name` → `Song.Album.Name`, `artist.name` → `Song.Artists.Artist.Name`, `genre.name` → `Song.Genres.Genre.Name`, `year` → `Song.Year`, `explicit` → `Song.Explicit`, `isFavorite` → `Song.IsFavorite`, `hasWaiver` → `HasWaiver`, `createdAt` → `CreatedAt`.

#### Scenario: Listing non-conformities with a filter
- **WHEN** a GET request is made to `/api/audits/rules/{id}/non-conformities?filter=hasWaiver = false and year >= 2020`
- **THEN** the system returns only non-conformities matching the filter expression, with song data loaded

#### Scenario: Listing non-conformities without a filter
- **WHEN** a GET request is made to `/api/audits/rules/{id}/non-conformities` without a filter parameter
- **THEN** the system returns all non-conformities for the rule (preserving backward compatibility)

### Requirement: Frontend audit filter metadata integration
The frontend SHALL add `'audits'` to the `FilterEntity` union type. The `useFilterMetadata` hook SHALL support a `resourceId` parameter for entities whose filter endpoints are scoped to a parent resource (e.g., `/api/audits/rules/{id}/non-conformities/filter-metadata`).

When `resourceId` is provided for the `audits` entity type, the hook SHALL substitute `{id}` in the endpoint URL with the provided resource ID.

#### Scenario: Fetching filter metadata for audit rule 5
- **WHEN** `useFilterMetadata('audits', 5)` is called
- **THEN** the hook fetches from `/api/audits/rules/5/non-conformities/filter-metadata` and caches the result with key `["filter-metadata", "audits", 5]`

### Requirement: Audit default component server-side filtering
The `AuditDefaultComponent` SHALL use `filterMode="server"` on the Collection component with `filterMetadata` and `fetchFilterValues` wired from the schema hook, enabling the advanced filter code editor with IntelliSense completions.

The `AuditDetailPage` SHALL manage the filter state (search and expression) using `useCollectionActions` and `useCollectionStateByKey`, passing `serverSearch`, `serverFilter`, and `onServerFilterChange` props to the Collection.

The `useAuditNonConformitiesSchema` hook SHALL provide `filterMetadata` and `fetchFilterValues` properties in the returned schema, mirroring the pattern used in `useSongsSchema`.

#### Scenario: User opens filter editor on audit detail page
- **WHEN** the user clicks the "Filters" button on the audit detail page
- **THEN** the FilterCodeEditor opens with IntelliSense showing audit-specific fields (title, album.name, artist.name, genre.name, year, explicit, isFavorite, hasWaiver, createdAt) and appropriate operators

#### Scenario: User applies a filter on audit non-conformities
- **WHEN** the user types `hasWaiver = false and year >= 2020` and presses Ctrl+Enter
- **THEN** the Collection triggers `onServerFilterChange` which updates the collection store, the `useListAuditNonConformities` hook re-fetches with the filter parameter, and only matching non-conformities are displayed

### Requirement: Audit non-conformities API client filter support
The `useListAuditNonConformities` hook (Orval-generated) SHALL support passing a `filter` query parameter to the backend. After regenerating the client, the `AuditDetailPage` SHALL pass the current filter expression to this hook.

#### Scenario: Fetching non-conformities with filter parameter
- **WHEN** the `useListAuditNonConformities` hook is called with `{ id: 5, filter: "hasWaiver = false" }`
- **THEN** the API request includes the filter as a query parameter