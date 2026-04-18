## Why

The audit detail pages currently support only basic client-side text search via `filterMode="client"` on the Collection component. Users cannot construct structured queries (e.g., `album.name contains "Greatest" and year >= 2020`) to narrow down non-conformities. The songs page already has a mature server-side advanced filter system with a Monaco-based DSL editor and dynamic autocomplete—bringing the same capability to audits enables powerful, consistent filtering across the app.

## What Changes

- Add `filterMode="server"` support to the audit default component's Collection, replacing the current `filterMode="client"` with server-side filtering
- Add backend `GET /api/audits/rules/{id}/non-conformities/filter-metadata` endpoint returning field metadata for non-conformity songs
- Add backend `GET /api/audits/rules/{id}/non-conformities/filter-values` endpoint returning dynamic autocomplete values for filter fields
- Add `audits` to the `FilterEntity` type on the frontend
- Create `useAuditFilterMetadata` hook (or extend `useFilterMetadata`) to fetch audit-specific filter metadata
- Wire the existing `FilterCodeEditor` IntelliSense system to the new audit filter-metadata/filter-values endpoints
- Update `AuditsController` to accept and apply a `filter` DSL parameter on the non-conformities endpoint

## Capabilities

### New Capabilities
- `audit-server-filters`: Server-side advanced filtering for audit non-conformities, including filter-metadata and filter-values endpoints, DSL filter parameter support on the non-conformities query, and frontend integration with the existing FilterCodeEditor and autocomplete system

### Modified Capabilities

## Impact

- **Backend**: `AuditsController` (new endpoints + filter parameter on existing non-conformities endpoint), `MusicDbContext` (query modifications for filter support)
- **Frontend**: `AuditDefaultComponent`, `useAuditNonConformitiesSchema`, `filter-entity.ts` type, `use-filter-metadata.ts` hook
- **API**: Two new endpoints, one modified endpoint (filter parameter added)
- **Shared**: `FilterMetadataHelper` and `FilterMetadataResponse` DTOs reused