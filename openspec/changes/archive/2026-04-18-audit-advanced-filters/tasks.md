## 1. Backend: Filter Metadata & Values Endpoints

- [x] 1.1 Add `GetAuditNonConformityFieldMetadata()` private method to `AuditsController` returning field definitions for: title, album.name, artist.name, genre.name, year, explicit, isFavorite, hasWaiver, createdAt
- [x] 1.2 Add `GET /api/audits/rules/{id}/non-conformities/filter-metadata` endpoint to `AuditsController` that returns `FilterMetadataResponse` with fields and operators
- [x] 1.3 Add `GET /api/audits/rules/{id}/non-conformities/filter-values` endpoint to `AuditsController` accepting `field`, `search` (optional), `limit` (default 15) parameters, with dynamic value queries for song-related fields and static `["true", "false"]` for boolean fields
- [x] 1.4 Add private `GetAuditNonConformityFieldMappings()` method mapping DSL field names to entity paths (e.g., `title` → `Song.Title`, `hasWaiver` → `HasWaiver`, etc.)

## 2. Backend: Server-Side Filter on Non-Conformities Endpoint

- [x] 2.1 Add optional `[FromQuery] string? filter` parameter to `ListNonConformities` action in `AuditsController`
- [x] 2.2 When `filter` is provided, parse it with `FilterDslParser.Parse()`, resolve entity paths with field mappings, build expression with `DynamicFilterBuilder.BuildFilter<AuditNonConformity>()`, and apply to query
- [x] 2.3 Verify that the existing unfiltered behavior still works (filter parameter is optional)

## 3. Frontend: Extend FilterEntity and useFilterMetadata

- [x] 3.1 Add `'audits'` to the `FilterEntity` union type in `src/types/filter-entity.ts`
- [x] 3.2 Extend `useFilterMetadata` in `src/components/filters/use-filter-metadata.ts` to accept an optional `resourceId` parameter; add audits endpoint mapping `/api/audits/rules/{id}/non-conformities/filter-metadata` with `{id}` substitution; update query key to include resourceId when provided

## 4. Frontend: Wire Up Audit Schema with Filter Metadata

- [x] 4.1 Update `useAuditNonConformitiesSchema` to accept the audit rule ID, call `useFilterMetadata('audits', ruleId)`, and include `filterMetadata` and `fetchFilterValues` in the returned schema
- [x] 4.2 Add `fetchFilterValues` callback that calls `GET /api/audits/rules/{id}/non-conformities/filter-values` with field, search, and limit parameters

## 5. Frontend: Server-Side Filtering in AuditDetailPage

- [x] 5.1 Update `AuditDetailPage` to manage filter state using `useCollectionActions` and `useCollectionStateByKey`, similar to `SongsPage`
- [x] 5.2 Pass `search` and `filter` parameters to `useListAuditNonConformities` hook
- [x] 5.3 Pass `filterMode="server"`, `serverSearch`, `serverFilter`, and `onServerFilterChange` props to Collection in `AuditDefaultComponent`
- [x] 5.4 Update `AuditDefaultComponent` props interface to accept `serverSearch`, `serverFilter`, `onServerFilterChange`, and pass them through to the Collection component

## 6. API Client Regeneration

- [x] 6.1 Restart the server and run `devbox run orval` to regenerate the API client with the new filter parameter on the non-conformities endpoint
- [x] 6.2 Verify the generated `useListAuditNonConformities` hook accepts the `filter` parameter; if mutation invalidation is needed, add it to `mutationInvalidates` in `orval.config.cjs`

## 7. Testing

- [x] 7.1 Add unit tests for `GetAuditNonConformityFieldMetadata()` verifying field names, types, and supported operators
- [x] 7.2 Add integration tests for the filter-metadata and filter-values endpoints verifying correct response structure
- [x] 7.3 Add integration tests for the non-conformities endpoint with filter parameter verifying filtered results