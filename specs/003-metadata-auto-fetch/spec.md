# Feature Specification: Auto-Fetch Metadata for Audit Issues

**Feature Branch**: `003-metadata-auto-fetch`  
**Created**: 2026-03-17  
**Status**: Draft  
**Input**: User description: "Hello. I wanna develop a new system for auto-fetching metadata diff patches. Those auto-fetched patches should be saved in a new entity. There should be a button in the audits page to auto-fetch metadata. The button should call an endpoint on the server. The endpoint should get all songs that have at least one issue not-waived, and that do not have yet any auto-fetched patch info from the last 30 days, and queue background tasks, one per song. These background tasks should then auto-fetch metadata for that song and store it in the entity. On the audit page, when editing songs, we should pre-load the auto-fetched metadata for songs that have them. The checkboxes that are pre-selected on the edit song modal should depend, in these cases, on the specific audit rule type we are editing from."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trigger Metadata Auto-Fetch (Priority: P1)

As a music curator, I want to click a button on the audits page to automatically fetch metadata suggestions for all songs with unresolved audit issues, so I can quickly see potential corrections without manually searching for each song.

**Why this priority**: This is the core entry point of the feature. Without this capability, users cannot initiate the metadata fetching process, making the entire feature unusable.

**Independent Test**: Can be fully tested by clicking the "Auto-fetch Metadata" button on the audits page and verifying that background tasks are queued for eligible songs (those with non-waived issues and no recent auto-fetched patches).

**Acceptance Scenarios**:

1. **Given** the user is on the audits page and there are songs with non-waived audit issues that haven't had metadata fetched in the last 30 days, **When** the user clicks the "Auto-fetch Metadata" button, **Then** the system queues background tasks for those songs and shows a confirmation message.

2. **Given** the user clicks the button but there are no eligible songs (all issues waived or recently processed), **When** the action completes, **Then** the system displays a message indicating no songs need processing.

3. **Given** the background tasks are queued, **When** the tasks execute, **Then** each song gets its metadata fetched from external sources and stored in the system.

---

### User Story 2 - View Auto-Fetched Metadata While Editing (Priority: P2)

As a music curator resolving audit issues, I want to see pre-populated metadata suggestions when editing a song from the audit page, so I can quickly apply corrections suggested by the auto-fetch system.

**Why this priority**: This delivers the value of the fetched metadata by making it actionable during the audit resolution workflow. Without this, users would need to manually look up metadata elsewhere.

**Independent Test**: Can be fully tested by opening the edit song modal from the audit page for a song that has auto-fetched metadata and verifying the suggestions are displayed and relevant fields are pre-selected based on the audit rule type.

**Acceptance Scenarios**:

1. **Given** a song has auto-fetched metadata stored in the system, **When** the user opens the edit modal for that song from the audit page, **Then** the auto-fetched metadata suggestions are displayed alongside the current values.

2. **Given** the user is editing a song from a specific audit rule type (e.g., "Missing Year"), **When** the edit modal opens, **Then** the checkboxes for the relevant metadata fields are pre-selected to indicate which values from the auto-fetched data should be applied.

3. **Given** a song has no auto-fetched metadata, **When** the user opens the edit modal, **Then** the modal displays the standard edit form without pre-populated suggestions.

---

### User Story 3 - Apply Auto-Fetched Metadata Corrections (Priority: P3)

As a music curator, I want to selectively apply auto-fetched metadata corrections by checking/unchecking suggested changes, so I can review and approve only the accurate suggestions.

**Why this priority**: This ensures quality control by requiring user confirmation before changes are applied. It prevents incorrect metadata from being automatically applied without review.

**Independent Test**: Can be fully tested by opening the edit modal for a song with auto-fetched metadata, selecting specific checkboxes, and saving to verify only the checked fields are updated.

**Acceptance Scenarios**:

1. **Given** the edit modal shows auto-fetched metadata suggestions with pre-selected checkboxes based on the audit rule type, **When** the user reviews and modifies the selections (checking/unchecking boxes), **Then** the system respects the user's choices.

2. **Given** the user has made their selections, **When** they save the changes, **Then** only the metadata fields corresponding to checked boxes are updated with the auto-fetched values.

3. **Given** the user saves the changes, **When** the update completes, **Then** the auto-fetched patch data is marked as applied or cleared to prevent it from being suggested again.

---

### User Story 4 - Monitor Background Task Progress and Failures (Priority: P2)

As a music curator, I want to view the progress and see any failures of the background tasks that are auto-fetching metadata, so I can understand when the process is complete and which songs failed to fetch metadata.

**Why this priority**: This provides transparency into the background processing, allowing users to understand the status of their metadata fetch requests and take action on failed tasks without waiting indefinitely.

**Independent Test**: Can be fully tested by triggering the auto-fetch action and clicking a monitoring link that opens a modal showing real-time progress and failure details.

**Acceptance Scenarios**:

1. **Given** the user has triggered the metadata auto-fetch, **When** background tasks are processing, **Then** the user can click a link on the audits page that opens a modal showing a real-time progress indicator with the number of completed tasks vs. total queued tasks.

2. **Given** the user is viewing the task progress modal, **When** some tasks fail to fetch metadata, **Then** the modal displays a failure count and detailed error information for each failed task, including: song identifier, specific failure reason (Service Unavailable, No Metadata Found, Network Error, or System Error), and timestamp.

3. **Given** all tasks have completed (successfully or with failures), **When** the processing finishes, **Then** the modal provides a summary showing: total processed, successful fetches, failed fetches categorized by type, and an option to retry failed tasks.

---

### Edge Cases

- What happens when the external metadata service is unavailable or returns errors?
- How does the system handle songs where multiple conflicting metadata sources return different values?
- What happens if a user clicks the auto-fetch button multiple times in quick succession?
- How should the system handle songs that have been recently processed but the metadata fetch failed?
- What if the audit rule type doesn't map clearly to specific metadata fields (ambiguous mapping)?
- What happens if the user closes the browser or navigates away while tasks are still running?
- How should the system handle very long-running background tasks that exceed 10 minutes?
- What happens when the user retries a failed task but the external service is still unavailable?
- How should the system handle real-time updates if the WebSocket or polling mechanism fails?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an "Auto-fetch Metadata" button on the audits page that initiates the metadata fetching process.

- **FR-002**: The system MUST identify songs eligible for auto-fetch processing based on two criteria: (1) having at least one non-waived audit issue, AND (2) not having any auto-fetched metadata from the last 30 days. The system MUST enforce this 30-day deduplication window preventing re-fetch unless manually requested.

- **FR-003**: The system MUST queue background tasks for each eligible song when the auto-fetch button is clicked.

- **FR-011**: The system MUST handle background task failures gracefully, marking failed fetches and allowing retry after a cooldown period.

- **FR-012**: The background task MUST use the existing sources API that is currently used by the "Auto-fetch" button in the edit song modal dialog to fetch metadata.

- **FR-013**: The system MUST implement a rule-based mapping between audit rule types and metadata fields, where some audit rules pre-select multiple fields (e.g., "Incomplete Metadata" pre-selects all empty fields, "Missing Year" pre-selects only the year field).

- **FR-014**: The system MUST provide a link on the audits page that opens a modal for monitoring background task progress.

- **FR-015**: The monitoring modal MUST display real-time progress showing the count of completed tasks versus total queued tasks, with updates every 5 seconds.

- **FR-016**: The system MUST display detailed failure information for each failed task in the monitoring modal, including: song identifier, failure reason categorized as Service Unavailable, No Metadata Found, Network Error, or System Error, and failure timestamp.

- **FR-017**: The monitoring modal MUST provide a summary when all tasks complete, showing: total number of songs processed, count of successful metadata fetches, count of failed fetches categorized by failure type, and a retry option for failed tasks.

- **FR-018**: The system MUST allow users to retry all failed tasks as a batch operation from the completion summary in the monitoring modal.

- **FR-019**: The system MUST provide an endpoint to retrieve current queue status for monitoring purposes, returning: total queued tasks, completed count, failed count with categorized reasons, and overall progress percentage.

- **FR-020**: The system MUST persist task progress state and allow users to reopen the monitoring modal at any time to see current status, including: tasks completed since last view, new failures, and option to retry failed tasks.

- **FR-021**: The system MUST handle browser closure or navigation during active processing by persisting task state server-side and resuming monitoring seamlessly when the user returns.

- **FR-022**: The system MUST implement a timeout mechanism for background tasks that exceed 10 minutes, marking them as failed with "Timeout" reason and making them eligible for retry.

- **FR-023**: The system MUST implement exponential backoff for retry attempts when the external metadata service is unavailable, with maximum retry limit of 3 attempts per task.

- **FR-024**: The system MUST gracefully handle real-time update mechanism failures by falling back to manual refresh capability with clear user indication.

### Key Entities *(include if feature involves data)*

- **AutoFetchedMetadata**: Represents metadata fetched from external sources for a song. Contains: song reference, fetched values (title, artist, album, year, etc.), source identifier, fetch timestamp, status (pending/applied/failed), and failure reason if applicable.

- **AuditIssue**: Represents a quality issue identified during auditing. Has attributes like: song reference, rule type (e.g., "Missing Year", "Invalid Artist"), severity, status (open/waived/resolved), and timestamp.

- **Song**: The core music entity. Has attributes including: title, artists, album, year, duration, and relationships to audit issues and auto-fetched metadata.

- **BackgroundTask**: Represents a queued task for async processing. Contains: task type (metadata fetch), target song reference, status (queued/processing/completed/failed), creation timestamp, processing timestamps, failure reason categorized by type (Service Unavailable, No Metadata Found, Network Error, System Error), retry count, and progress percentage (0-100) for real-time monitoring.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can trigger metadata auto-fetch for all eligible songs with a single button click, completing the queuing process in under 5 seconds regardless of the number of songs.

- **SC-002**: The system successfully fetches metadata for at least 85% of processed songs within 10 minutes of task initiation.

- **SC-003**: When editing a song with auto-fetched metadata, users see the suggestions within 2 seconds of opening the edit modal.

- **SC-004**: Users can resolve audit issues 60% faster when using auto-fetched metadata suggestions compared to manual research and entry.

- **SC-005**: The pre-selected checkboxes based on audit rule type correctly identify the relevant field in 95% of cases, reducing user selection errors.

- **SC-006**: The system prevents duplicate fetches, ensuring no song is processed more than once per 30-day period unless manually requested.

- **SC-007**: Failed metadata fetches are identified and logged, with a clear indication to users when data is unavailable for specific songs.

- **SC-008**: Users can monitor task progress with real-time updates every 5 seconds, providing visibility into the completion status of all queued tasks.

- **SC-009**: Failed tasks are identifiable with specific failure reasons within 30 seconds of the failure occurring, allowing users to understand why metadata could not be fetched.

- **SC-010**: 90% of users can understand failure reasons without requiring additional documentation or support, as indicated by categorized error messages (Service Unavailable, No Metadata Found, Network Error, System Error).

- **SC-011**: Users can retry failed tasks as a batch operation from the monitoring modal, with the retry process completing within 2 minutes for all failed tasks.

## Assumptions

- External metadata services (MusicBrainz, Discogs, or similar) provide APIs that can be queried by song identifiers or by artist/title combinations.
- The audit system already has a defined set of rule types that can be mapped to specific metadata fields.
- Background task infrastructure exists or will be provided for queueing and processing tasks asynchronously.
- The edit song modal already supports checkbox-based selection for applying metadata changes.
- Users have appropriate permissions to trigger auto-fetch operations and edit song metadata.
- Network connectivity to external metadata services is generally available, with graceful handling of temporary outages.
