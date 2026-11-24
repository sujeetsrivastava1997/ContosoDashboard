# Phase 0 Research

## Decision 1: File storage implementation

**Decision**: Use `IFileStorageService` with a `LocalFileStorageService` implementation that stores under `AppData/uploads`, outside `wwwroot`. The storage key is a GUID plus a validated extension and is never derived from the user-supplied filename.

**Rationale**: This matches the current offline training architecture and directly prevents path traversal and web-root exposure. Infrastructure abstraction provides a future Azure Blob migration path.

**Alternatives considered**:
- Store files in `wwwroot`: rejected because downloads would bypass authorization.
- Store files as database blobs: rejected because it complicates 25 MB files and the existing infrastructure target.
- Azure Blob Storage now: rejected because it violates the offline-first requirement.

## Decision 2: Upload and replacement consistency

**Decision**: Generate the storage key before the database write, save the file first, then save document metadata in an EF transaction. On metadata or storage failure, compensate by deleting the newly written file and rolling back/omitting the metadata. Replacement saves and validates the new file, updates metadata in one database operation, and deletes the old file only after the database update succeeds.

**Rationale**: This is the repository's documented local-storage pattern and prevents the most likely orphaned-file and partial-record cases.

**Alternatives considered**:
- Database record first: rejected because file-save failure leaves a record.
- Two independent EF and filesystem transactions: rejected because cross-provider atomicity is unavailable.

## Decision 3: Malware scanning

**Decision**: Define `IMalwareScanService` and implement `LocalMalwareScanService` for the training environment. The implementation validates file size and MIME/extension consistency, rejects blocked executable signatures/known unsafe patterns, and is explicitly marked in the plan as a training scanner rather than a production anti-malware engine. The service contract remains replaceable by a real scanning implementation later.

**Rationale**: It satisfies offline operation and prevents unsafe file types while documenting that the repository is a training implementation.

**Alternatives considered**:
- External malware subscription: rejected for offline constraints.
- Placeholder that always approves: rejected because the feature and constitution require meaningful misuse controls.
- Client-only validation: rejected because it is not authoritative.

## Decision 4: Team identity and team-share snapshots

**Decision**: Use the existing `User.Department` value as the training team identity, because no separate Team entity exists. When an owner shares with a department/team, resolve and snapshot the IDs of users in that department at share time into `DocumentShareRecipient` rows. Later department changes or additions do not alter existing recipients.

**Rationale**: It uses existing claims and seeded data while enforcing the exact snapshot requirement. A Team Lead manages a team member when both users are Team Leads/Employees in the same department and the current user has the TeamLead role.

**Alternatives considered**:
- Dynamic Department-based access: rejected because later members would incorrectly gain access.
- Project membership as team: useful for project authorization, but it is not the same as a separately selected sharing team.
- Add a full Team entity: rejected as unnecessary scope because requirements do not require team management CRUD.

## Decision 5: Authorization model

**Decision**: Centralize document authorization in `DocumentService`. Document access is granted if the user is the owner, an administrator, a project member/manager for a project document, a snapshot recipient from an active share, or a task participant for a task document as applicable. Owners can share; revocation removes snapshot recipients immediately. Team Leads may edit/replace/delete documents uploaded by users in the same department; Project Managers may delete documents associated with projects they manage.

**Rationale**: This enforces both page and service boundaries and keeps Razor components from querying or authorizing files directly.

**Alternatives considered**:
- UI and route-only authorization: rejected by constitution.
- Separate authorization methods in every page: rejected because behavior would diverge.

## Decision 6: External file-serving interface

**Decision**: Add minimal ASP.NET Core endpoints for authorized preview/download operations. Razor pages handle metadata and management; authenticated endpoint handlers resolve the current user and call `DocumentService` before streaming.

**Rationale**: Files remain outside `wwwroot`, while HTTP ranges and content disposition can be controlled without exposing filesystem paths.

**Alternatives considered**:
- Static files: rejected because it bypasses authorization.
- Separate REST project: rejected as unnecessary for existing Blazor architecture.

## Security and Misuse Review

- **Authentication**: every protected page and endpoint requires the existing cookie authentication.
- **Authorization**: all object lookups use the requesting user ID; direct object ID guessing must not reveal metadata or bytes.
- **Input**: title/category/tag lengths and project/task existence are server validated; MIME and extension are allowlisted; storage names are generated.
- **Direct object references**: download, preview, edit, share, revoke, delete, and replace methods reject unauthorized IDs.
- **Accidental exposure**: no storage path or absolute file name is returned to UI; CSP and `nosniff` remain in effect.
- **Failure handling**: security-neutral upload rejection; compensation deletes newly created files; no partial accessible file is left after failed metadata persistence.
- **Audit/privacy**: retain audit metadata after revoked/deleted shares and avoid logging file contents or full system paths.

## Open Items Resolved

- No unresolved technical clarification remains for the plan.
