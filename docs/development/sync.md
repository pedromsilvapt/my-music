# Music Sync

This document is the source-of-truth for requirements and high level design regarding the sync process.
The server can be synced to remote devices (currently supported by the CLI and Mobile projects).
The sync process followed by both the CLI and Mobile projects should be an almost identical match, like a line-by-line.
 - Only specific tech differences are allowed (for example, the CLI, written in C#, uses dependency injection. The Mobile, in TypeScript, does not, because adding a dependency for that was not worth it.)
When running a sync session, we can use dry run. When in dry-run, we do not:
 - Import the songs into the system
 - Do not change fields in the SongDevices table
 - We only are allowed to make changes to the database in SyncSession and DeviceSyncSessionRecord tables.
 - We do not make any changes whatsoever to local device files.
During a sync session, we create a list of `DeviceSyncSessionRecord`. Each record has a Action , which indicates its type, and a JSON Data field (schema varies by action type), which stores the values necessary for performing such action.
 - The list of `DeviceSyncSessionRecord` must be exactly the same when running in dry-run vs when not.
 - The server should return in each endpoint called during the sync process, return the delta counters for how many DeviceSyncSessionRecord of each action type were created by that request.
    - A record is only counted by the request that creates it. Acknowledging a record creates none, so its delta counters are empty: the records the device performs were already counted when they were created (by `check`, `resolve-conflicts`, `conflict-choices`, `upload` or `pending-actions`).
 - The device client applications should not keep track of counters on their own, they should instead just keep track of the counters sent to them by the server. The server is the authoritative source of counter values.
 - The server should create all the `DeviceSyncSessionRecord` before the commit phase: only in the commit phase are those actions really performed (songs created, files uploaded, downloaded, renamed, etc...)
    - The list of `DeviceSyncSessionRecord` is the source of truth for what operations to perform
 - During song upload, we save the uploaded song files inside a temp folder in the music repository directory. And during commit, we move those files to the correct repository.
    - Files that will not be imported at commit (dry-run uploads, and uploads recorded as `Link`, `UpdateLocal`, `Skipped` or `Error`) are deleted at the end of the upload request.
    - The upload response returns every record created for the file. When the file matches a previous Checksum of a Song, those include the `UpdateLocal` the device performs in the same session (see the rules below). The session's temp folder, with any leftovers, is deleted when the session ends.
 - During sync process, when the server needs to find a song by checksum or by device path, we should not only look in the Songs database table, but also in the `DeviceSyncSessionRecord` for records created during this sync session that match those data properties.
  - For the sync process, some important rules:
   - A file locally without any SongDevice matches:
      - If it matches the current Checksum of a Song, should result in a Link action
      - If it matches the previous Checksum of a Song, should result in a Link + UpdateLocal
      - If it matches another local file already being uploaded in this session, should result in a Link to that file's song
      - If it matches nothing, should result in a CreateRemote
      - With deduplication (`--deduplicate`), a file that would result in a CreateRemote is first matched by sound (acoustic fingerprint):
         - If it sounds like a Song, should result in a Link + UpdateLocal (the device gets the server's file)
         - If it sounds like another local file already being uploaded in this session, should result in a Link to that file's song + UpdateLocal that copies that local file over it
         - In direction `up`, the UpdateLocal is a Skipped instead
      - If it can't be imported (invalid/unreadable file), should result in an Error
   - A file locally with a SongDevice match:
      - File changes are based on the file's `ModifiedAt`, the SongDevice's `LastSyncModifiedAt` values, and the Song's `FileModifiedAt`
      - If neither side changed since the last sync, should result in a Skipped
      - If only the local file changed:
         - If its new checksum matches no other Song (current or previous), should result in an UpdateRemote of its current Song
         - If its new content matches the current Checksum of current Song, should result in a Skipped
         - If its new content matches the previous Checksum of its own Song, should result in an UpdateLocal
         - If its new content matches the current Checksum of another Song, should result in a Unlink old Song + Link to that new Song
         - If its new content matches the previous Checksum of another Song, should result in an Unlink old Song + Link to that new Song + UpdateLocal
      - If only the server file changed:
         - If the local file already has the server's current content, should result in an UpdateTimestamp
         - Otherwise, should result in an UpdateLocal (+ Rename only if the naming template now produces a different path)
      - If both sides changed:
         - If the contents are equal, should result in an UpdateTimestamp
         - If the local file matches a previous Checksum of the Song, should result in an UpdateLocal (the server version wins)
         - Otherwise, should result in a Conflict; the local file is kept and not downloaded, unless the user resolves it:
            - Choosing the server's version should result in an UpdateLocal (+ Rename only if the naming template now produces a different path)
            - Choosing the local version uploads the file, which should result in an UpdateRemote (or whatever the upload rules give for its content, such as an Unlink + Link when it matches another Song)
            - The Conflict record is kept, and the records that resolve it point to it (`ResolvesConflictRecordId`)
            - The Conflict counter only counts the conflicts left unresolved
      - If it was never synced before (the server assigned a song to the device, it hasn't been downloaded yet, and a local file already exists at exactly the path it would be downloaded to):
         - If the server changed it after it was assigned to the device, it is treated as "both sides changed"
         - Otherwise, should result in an UpdateRemote
      - If the Song was removed from the device or deleted on the server, should result in a DeleteLocal
      - If sync is forced, should result in an UpdateRemote
   - A SongDevice without a local file:
      - If the Song was assigned to the device and never downloaded, should result in a CreateLocal
      - If the Song was removed from the device, should result in a DeleteLocal
      - If the file was deleted locally, should result in an Unlink (the Song stays on the server)
   - Metadata-only edits (such as increasing `Song.PlayCount`) on the server never cause a download (because they do not change the physical file); only changes to the file content do
   - Downloaded and renamed files follow the device's naming template; a name collision gets a ` (2)`, ` (3)`, … suffix
      - A collision is only with *another* path of the device: a SongDevice never collides with its own path, so a path that already has a suffix is kept while the name without it is still taken
      - A path given to a file stays taken for the rest of the session, across all its requests: device paths only change at commit, so the paths taken and freed so far are read from the session's records (once per request)
      - A Rename frees the path the file is renamed from, and a DeleteLocal frees the path of the deleted file: a later file of the session can be given that path
      - A device never replaces an existing file when it renames: if the target is still there (its own rename or deletion failed), the Rename should result in an Error
      - The file is always put at the path the server holds for it. When that path changes before the first download, the CreateLocal carries the new path and the commit saves it in the SongDevice
   - Content present in the server library only once: duplicate local files (same Checksum) link to one Song and are never imported twice
   - Direction `up`:
      - Anything that would change the device (UpdateLocal, CreateLocal, DeleteLocal, Rename) should result in a Skipped instead
      - Songs deleted on the server keep their local file (Skipped)
      - Pending removals are ignored, and the file is compared normally
      - Files deleted locally should result in an Unlink, even if they had a pending server action
   - Direction `down`:
      - Nothing is uploaded
      - Files deleted locally are not unlinked
      - All server actions (CreateLocal, UpdateLocal, DeleteLocal, Rename) are applied on the device
   - Failures:
      - If an upload fails to import at commit, should result in an Error; the local change stays pending and is retried on the next sync
      - Links to content whose import failed should result in an Error too
      - Files that fail to scan on the device should result in an Error

---

# High Level Design

## The Problem

A user has a music server and one or more devices. Each device holds a local copy of part (or all) of the music library. Both the server and any device can change independently between sync sessions: new songs appear, existing songs get modified, songs get deleted or renamed. The goal of sync is to bring both sides back to a consistent state — the server reflecting what the device has, and the device reflecting what the server has — without losing data or creating duplicates.

## Core Concepts

### The Device-Server Relationship

Each device maintains its own independent filesystem. The server owns the canonical music library: the source of truth for what songs exist. A device's music files are a working copy — some songs may be present locally but not yet on the server, and vice versa. The `SongDevice` association records which server songs are present on which device, and at which local path.

### Sync Direction

Not every sync session is bi-directional. A user may want to:

- **Push only** (`up`) — upload local changes to the server without downloading anything. The device is the source of truth for this session.
- **Pull only** (`down`) — download server changes without uploading anything. The server is the source of truth for this session.
- **Full sync** (`both`) — push and pull in the same session.

Direction affects not just what data flows, but how deletions are interpreted. See [Orphan Detection](#orphan-detection).

In `up`, the server never creates records that require the device to act (`DeleteLocal`, `UpdateLocal`, `CreateLocal`, `Rename`): a pending server removal is ignored and the file is compared normally, and a newer server version is recorded as `Skipped`.

In `down`, the device applies every server-initiated action (`CreateLocal`, `UpdateLocal`, `DeleteLocal`, `Rename`), not only downloads: the server is the source of truth, so its removals and renames are carried out on the device as well.

## Change Detection

The fundamental question sync must answer for every file is: **has this changed since the last sync?**

### Timestamps as the Primary Signal

We use filesystem modification timestamps (`modifiedAt`) as the primary change detection mechanism. After each sync, the server records the device's last-known modification timestamp for each file (`LastSyncedModifiedAt`). On the next sync, comparing the current timestamp against the last-synced timestamp tells us whether the file has been touched.

This choice trades perfect accuracy for practicality: timestamps are essentially free to read, and in practice they reliably indicate changes. The alternative — hashing every file on every sync — would be correct but prohibitively expensive for large music libraries.

### When Timestamps Lie

Timestamps can diverge from reality: copying a file without modifying it can change the timestamp, or a metadata editor might touch the file without changing the audio content. In the worst case, both sides report a newer timestamp for the same file (a potential conflict). We resolve these situations with checksums — see [Conflict Resolution](#conflict-resolution).

### Unseen Files (No Path Match)

If a file exists on the device but there is no `SongDevice` association for that path, it is a new file — it has never been synced before. Conversely, if a `SongDevice` association exists in the database but the corresponding file was not mentioned during this sync session, the file may have been deleted from the device. See [Orphan Detection](#orphan-detection).

## Conflict Resolution

A conflict occurs when both the device and the server have modified the same file since the last sync. Timestamps alone cannot tell us whether the modifications are identical or divergent — they only tell us that both sides claim to be newer.

### The Two-Step Resolution Algorithm

1. **Detect by timestamps** — If both `deviceModifiedAt` and `serverModifiedAt` are newer than `LastSyncedModifiedAt`, we have a potential conflict.
2. **Resolve by checksum** — The device computes the checksum of its file (with the algorithm the `check` response reports for the song) and sends only that; the server compares it against the stored song. Older clients upload the whole file instead and the server computes the checksum, which is slower but gives the same result:
   - **Checksums match** → The content is identical despite the timestamp difference (e.g., the file was copied or touched without changing audio). Auto-resolve: treat as a normal update. The timestamps diverged, but the music didn't.
   - **Checksums differ** → The content genuinely changed on both sides. This is a real conflict. The conflict is recorded and surfaced to the user; the device does not download the server's version, preserving the local file.

### Resolving a Real Conflict

Only the user can settle a real conflict, so the device asks what to do with each one, right after the server reports it:

- **Download** — the server's version wins. The device sends the ids of those `Conflict` records (`conflict-choices`), and the server creates the `UpdateLocal` (and a `Rename`, when the naming template now gives the file another path) that the device performs in the server actions phase.
- **Upload** — the local version wins. The device uploads the file, naming the `Conflict` record it resolves; the upload is decided like any other (normally an `UpdateRemote`).
- **Skip** — the conflict stays, the local file is untouched, and the next sync reports it again.

The `Conflict` record is never removed: the records created by the choice point to it through `ResolvesConflictRecordId`, which is how the session history shows what was decided. A resolved conflict no longer counts as a conflict, so the conflict counter (in the session and in each response's delta, where it can be negative) only covers the unresolved ones. An upload that fails leaves the conflict unresolved.

A direction that never changes one side cannot pick it: `up` offers no download, `down` no upload. In `up`, a download chosen anyway is recorded as `Skipped`.

The choice is asked in a dry run too, and creates the same records. Resolving a conflict can affect other records (the renamed path of a downloaded file is taken for the rest of the session, an uploaded file can be linked to another song), so a dry run that did not ask would not preview the real sync. As with every other record, a dry run performs none of them: the conflict is still there on the next sync.

How the question is asked is up to each client. The CLI prompts, or takes the answer for every conflict from `--conflicts`; the mobile app shows a dialog, unless "Treat Conflicts as Errors" is on.

### Why Checksums Are Not the Primary Signal

Checksums are reliable but expensive — they require reading the entire file. We use them only when timestamps indicate a potential conflict, not as the first-line change detection mechanism. This keeps the common case (no conflict) fast while still providing correct resolution when needed.

## Deduplication

The same audio file can appear at multiple device paths (duplicates in the user's collection) or across multiple devices. The server should represent each unique song once in the library, regardless of how many device paths reference it.

When a file is uploaded, the server calculates its checksum and checks whether a song with that checksum already exists — first among records created earlier in the same session, then across the user's entire library. If a match is found, the server creates a `Link` record (associating the device path with the existing song) instead of a `CreateRemote` record (importing a new song). This keeps the library free of duplicates without requiring the user to clean up their local collection.

The same applies to a re-uploaded (updated) file. If its new content matches a song other than the one the device path is linked to, the server creates a `Link` to that song instead of an `UpdateRemote`. At commit, the device path's association moves to the matching song, and the song it was linked to before is left unchanged. Session records that count as matches include pending `UpdateRemote` records, since the commit gives their song that content. The commit never merges songs on its own: that would change the library in a way no record describes.

### Soundalike Deduplication

- When syncing with the Deduplicate option, any new files from local to remote, are compared with Soundlike against existing remote songs, and previous new uploads from the same session.
   - If a match is found, instead of CreateRemote, we emit a Link + UpdateLocal
      - If the match found was a new file from the same session, UpdateLocal will reference in the Data class the LocalSourcePath of the matched upload - the device copies its own matched file over this one instead of downloading.
- For the calculation of fingerprints, we try to use existing fingerprints if they are in the database already (keyed on checksum). But if they are not yet calculated, for:
   - **library songs** are saved on the DB as soon as they are computed, both (in dry run or not)
   - **uploaded files** are kept in process memory. They are only saved in DB at the end, during commit phase, and only for non-dry run sessions.
- Fingerprinting library songs without a stored fingerprint can take long on a big library, so clients run a **prepare deduplicate** phase after starting the session and before uploading (skipped in `down`): they call `POST /devices/{deviceId}/sync/{sessionId}/deduplicate/prepare` until it returns `done`. Each call fingerprints one batch of songs and returns `total` / `processed`, which the clients show as a progress bar.
   - The preparation is optional: the first upload's soundalike lookup fingerprints whatever library songs are still missing.

## Two-Phase Commit

Sync is split into two distinct phases: **record** and **commit**. During the record phase, the system determines what needs to happen and writes a list of action records. During the commit phase, those actions are executed.

### Why Two Phases?

1. **Dry-run is a first-class feature** — A user can preview exactly what sync will do without any side effects. The record list is the same whether or not the commit executes. This is only possible if the decisions and the execution are separate.
2. **Atomicity** — The commit applies all changes in a single transaction. If something fails mid-commit, the session can be retried without inconsistent partial state. There are no half-synced libraries.
3. **Validation before execution** — The server sees the full plan before acting on it. It can validate that records are consistent, detect orphans, and resolve duplicates before any data is written to the library.

### The Record List Is the Contract

The list of `DeviceSyncSessionRecord` entries is the single source of truth for what a sync session will do. Every action — import, download, delete, link, rename, skip — is represented as a record. The commit phase is purely mechanical: it reads the records and performs the corresponding operations. Nothing happens during commit that wasn't already decided during the record phase (except for orphan detection, which adds `Unlink` records for files that disappeared from the device, and error reporting, which may add `Error` records when staged files or other resources are unexpectedly unavailable, or when importing an uploaded file fails). A commit-time `Error` record names the record it failed in `Data.FailedRecordId`, and that record's bookkeeping is skipped: a failed `CreateRemote` links nothing to the device, and a failed `UpdateRemote` does not mark the device's change as synced, so the next sync retries it.

### Unified Record Responses

Every sync endpoint returns records as a single flat list of `DeviceSyncSessionRecord` entries, each carrying its `Action` type. The client iterates the list and dispatches per-record based on that action. This is the natural consequence of the record list being the contract: if the record list is the single source of truth, then the API response should simply *be* that list — not a partitioned view of it.

Avoid returning individual lists for each action type (e.g., separate `ToCreate`, `ToUpdate`, `PotentialConflicts` lists). Partitioning records into typed lists introduces three problems:

- **Unnecessary complexity** — The server must partition records into separate categories, and the client must reassemble or iterate them independently. Both sides carry code that exists only to redistribute records across structures that the `Action` field already distinguishes.
- **Verbosity** — Each new action type requires a new list property on the response DTO, a new field in the client type, and new iteration logic. A single list with an `Action` discriminator needs none of this.
- **Ordering loss** — Partitioning records into separate lists discards the original sequence. The server creates records in a meaningful order (e.g., imports must happen before downloads that reference them); the client must receive that sequence intact.

### Session States

A sync session progresses through a defined lifecycle:

```
InProgress ──► Committed ──► Completed
     │
     └──► Cancelled
```

- **InProgress** — The record phase is active. Records are being created but no side effects have occurred.
- **Committed** — The commit phase has executed. All record actions have been applied.
- **Completed** — The session is finalized. Metadata (e.g., `Device.LastSyncAt`) is updated.
- **Cancelled** — The session was abandoned before commit.

The commit boundary is idempotent: if commit is called on an already-committed session, the existing results are returned without reprocessing.

## Orphan Detection

Between sync sessions, a user may delete files from their device. Since the device doesn't notify the server of deletions, the server must infer them: if a `SongDevice` association exists for a path, but no record in the current session mentions that path, the file was likely removed from the device. These are orphans.

How orphans are handled depends on the sync direction:

- **`both`** — The device is the source of truth for what files exist locally. Orphaned associations (with no pending sync action) are removed. The server forgets that the device ever had that file.
- **`up`** — The device is explicitly the source of truth for this session. Orphaned associations are removed, and any pending server-initiated actions (downloads marked on `SongDevice`) for files that are present are cleared — the user chose to push, not pull.
- **`down`** — The server is the source of truth for this session. Orphan detection is not performed; the server does not assume the device has intentionally deleted files just because it isn't mentioning them. The user is only pulling changes, not declaring that their local deletions should be reflected server-side.

## Dry-Run Mode

Dry-run is not a debug flag — it is a user-facing feature that answers the question "what would happen if I synced right now?" The answer is the record list: every action that would be taken, summarized by type and count.

### The Dry-Run Invariant

The list of `DeviceSyncSessionRecord` entries must be **identical** regardless of whether the session is a dry-run or not. The same decisions are made; only the side effects differ.

### What Dry-Run Skips

- **No songs are imported** into the library. Uploaded files are stored temporarily only for the length of the upload request (to calculate a checksum and check the metadata), then discarded.
- **No `SongDevice` associations are modified or deleted.** The library state remains untouched.
- **No local files are changed.** The device does not download, delete, or rename any files.
- **No `Device.LastSyncAt` timestamp is updated.** A dry-run session is invisible to the next real sync.
- **Session and record tables are still written.** These are the output of the dry-run, not a side effect of syncing.

### Device Options in a Dry-Run

Paths are computed with the session's naming template, which is resolved once when the session starts and stored on `DeviceSyncSession.NamingTemplate`; check, resolve-conflicts and pending-actions all read it from there.

- **Real sync** — The client saves its device options (naming template, icon, import on purchase) to the server device *before* starting the session, which then uses the device's template.
- **Dry-run** — The client saves nothing. It sends its local template in `SyncStartRequest.DeviceOptions`, which overrides the device's template for that session only, so the preview shows what the real sync would do. A `DeviceOptions` with a `null` template previews the server default.

`DeviceOptions` is rejected on a real sync: files would be renamed with a template the device doesn't have, and the next sync would rename them back.

Outside of a sync, the options are saved by `my-music device save` (CLI) and by saving the device settings screen (mobile).

## Server as Authority

The server is the sole authority for several aspects of the sync process. Clients never compute these independently:

- **Counter values** — The counts of actions by type (creates, updates, deletes, etc.) are determined by the server. The server returns delta counters with every response during the record phase. At commit and completion, the server returns the authoritative totals, and the client replaces its accumulated counts with these. This prevents drift between what the client displays and what the server recorded.
- **Conflict outcomes** — The server decides whether a conflict is auto-resolved or real, by comparing checksums. The client does not make this determination. For a real conflict the client only carries the user's choice; the server creates the records that apply it.
- **Deduplication** — The server decides whether an uploaded file is a new song or a link to an existing one. The client cannot know the full library state.
- **Orphan detection** — The server determines which `SongDevice` associations are orphans based on the record list and the direction of the sync. The client has no visibility into this.

## Key Invariants

1. **Record determinism** — The list of `DeviceSyncSessionRecord` entries is identical for dry-run and non-dry-run sessions given the same inputs.
2. **Commit atomicity** — All record actions are applied in a single transaction. Either all succeed or none do.
3. **One song per unique content** — Duplicate files (same checksum) are linked to a single song in the library, never imported twice.
4. **Server-authoritative counters** — Counter values displayed to the user always reflect the server's record of what happened, not the client's accumulation.
5. **No silent data loss** — Real conflicts are recorded and preserved. The device file is never overwritten by the server file when content diverges, unless the user chose the server's version for that conflict. Orphaned associations in `down` direction are not removed.
6. **Client parity** — The CLI and Mobile implementations make the same sync decisions for the same inputs. Allowed differences are limited to technology-specific concerns (DI vs. dependency passing, file system APIs, screen wake management, conflict UX presentation).
7. **Unified record responses** — Sync endpoints return a single ordered list of `DeviceSyncSessionRecord` entries. The client dispatches by `Action` on each record. Response DTOs must not partition records into separate lists by action category.
