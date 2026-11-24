# Phase 1 Data Model

## Document

Stores metadata and the infrastructure-independent storage key for an uploaded file.

| Field | Type | Rules |
|---|---|---|
| DocumentId | int | Primary key |
| Title | string(255) | Required |
| Description | string(2000)? | Optional |
| Category | enum/string(50) | Required; predefined category |
| ProjectId | int? | Optional; must exist and be accessible |
| TaskId | int? | Optional; must belong to a project equal to `ProjectId` when both are present |
| UploaderUserId | int | Required; existing User |
| UploadedUtc | DateTime | Required; set by service |
| FileSizeBytes | long | 1..26,214,400 inclusive |
| MimeType | string(255) | Required; allowlisted |
| StorageKey | string(512) | Required, unique, generated GUID-based key |
| OriginalFileName | string(255)? | Display/export metadata only; never used as a path |
| UpdatedUtc | DateTime | Updated on metadata or file replacement |

Relationships:
- many `Document` to one `UploaderUser`
- optional many to one `Project`
- optional many to one `TaskItem`
- one to many `DocumentTag`, `DocumentShare`, `DocumentActivity`

## DocumentTag

| Field | Type | Rules |
|---|---|---|
| DocumentTagId | int | Primary key |
| DocumentId | int | Required |
| Tag | string(50) | Required; normalized trim/lowercase comparison |

A unique index is used for `(DocumentId, Tag)`; a tag list is limited to a documented small count to keep UI validation simple.

## DocumentShare

Represents an owner share action.

| Field | Type | Rules |
|---|---|---|
| DocumentShareId | int | Primary key |
| DocumentId | int | Required |
| SharedByUserId | int | Required; must be document owner |
| ShareType | enum(User, Team) | Required |
| TeamIdentifier | string(100)? | Department name for team shares |
| SharedUtc | DateTime | Required |

Relationships:
- many to one `Document`
- many to one `SharedByUser`
- one to many `DocumentShareRecipient`

## DocumentShareRecipient

Stores the user snapshot for direct and team shares and tracks revocation.

| Field | Type | Rules |
|---|---|---|
| DocumentShareRecipientId | int | Primary key |
| DocumentShareId | int | Required |
| RecipientUserId | int | Required |
| RevokedUtc | DateTime? | Null until revoked |
| RevokedByUserId | int? | Required if revoked |

Rules:
- Direct user shares create one recipient.
- Team shares snapshot every currently matching department user ID.
- Revoking a recipient sets `RevokedUtc` immediately.
- Revoking a team removes access for the recipient rows belonging to that share/team; later joiners remain excluded.
- Active access requires `RevokedUtc == null`.

## DocumentActivity

Audit event that remains after document deletion or share revocation.

| Field | Type | Rules |
|---|---|---|
| DocumentActivityId | int | Primary key |
| DocumentId | int? | Nullable only if deletion cleanup requires the audit row to outlive the document |
| DocumentTitleSnapshot | string(255) | Title at event time |
| ActorUserId | int | Required |
| ActivityType | enum | Upload, Download, Preview, MetadataUpdated, Replaced, Deleted, Shared, Revoked |
| ActivityUtc | DateTime | Required |

Indexes: `(ActivityUtc)`, `(DocumentId, ActivityUtc)`, `(ActorUserId, ActivityUtc)`.

## Existing Entities Used

- `User`: uploader, owner, actor, department team identifier, role.
- `Project`: optional document project; `ProjectManagerId` and `ProjectMembers` define project access.
- `TaskItem`: optional task association; project is inherited from the task.
- `ProjectMember`: project team membership.
- `Notification`: existing in-app notification storage; add document-related notification types or document IDs in message payload where needed.

## Validation and State Rules

- Allowed MIME/extension pairs:
  - PDF: `application/pdf`, `.pdf`
  - Word: `.doc`, `.docx` and matching Office MIME values
  - Excel: `.xls`, `.xlsx`
  - PowerPoint: `.ppt`, `.pptx`
  - Text: `text/plain`, `.txt`
  - JPEG: `image/jpeg`, `.jpg`/`.jpeg`
  - PNG: `image/png`, `.png`
- Title and category are required; category must be one of the six predefined values.
- Project association requires an existing project and project membership or Project Manager access.
- Task upload requires a task the user may participate in; `ProjectId` is copied from the task.
- Upload and replacement cannot exceed 25 MB.
- Delete is permanent and removes metadata/tags/shares first or in the same compensated operation, then removes storage after successful metadata persistence.
- A file can only become accessible after upload, size/MIME validation, malware scanning, and database persistence all succeed.
