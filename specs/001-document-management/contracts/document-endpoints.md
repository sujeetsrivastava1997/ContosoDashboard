# Document HTTP Endpoint Contract

The feature uses Blazor Server for UI and metadata operations. It adds two authorized HTTP GET endpoints for file bytes. Both require authentication and re-check service authorization for direct-object access.

## POST `/api/documents`

**Purpose**: Upload a supported document with metadata.

**Multipart fields**
- `file`: non-empty file, maximum 25 MB
- `title`: required, maximum 255 characters
- `description`: optional, maximum 2000 characters
- `category`: required predefined document category
- `projectId`: optional existing project ID
- `taskId`: optional accessible task ID

**Response**
- `201 Created`: document metadata and the resource Location header
- `400 Bad Request`: missing or invalid metadata/file
- `401 Unauthorized`: no valid authenticated user
- `403 Forbidden`: the project or uploader context is unauthorized
- `413 Payload Too Large`: file exceeds 25 MB
- `415 Unsupported Media Type`: MIME/extension pair is not allowlisted
- `500 Internal Server Error`: storage or database persistence failure

## GET `/documents/{documentId}/preview`

**Purpose**: Inline preview for PDF and image documents only.

**Response**
- `200 OK`: supported PDF/image stream with `Content-Type` and `X-Content-Type-Options: nosniff`
- `400 Bad Request`: preview not supported for this MIME type
- `403 Forbidden`: authenticated user has no authorized access
- `404 Not Found`: document or storage object does not exist

**Rules**
- No filesystem path or original name is disclosed.
- Download/preview activity is recorded after a successful stream preparation.

## GET `/documents/{documentId}/download`

**Purpose**: Download any authorized document.

**Response**
- `200 OK`: file stream with a safe `attachment` filename derived from validated metadata
- `403 Forbidden`: authenticated user has no authorized access
- `404 Not Found`: document or storage object does not exist

**Rules**
- The endpoint uses `Content-Disposition: attachment`.
- The endpoint records a download activity only after the response is authorized.

## Document Service Contract

The UI and endpoint handlers call `DocumentService`; storage-specific details remain behind `IFileStorageService`.

Representative operations:

- `UploadAsync(userId, UploadRequest)` -> document or validation/storage failure
- `ReplaceAsync(documentId, requestingUserId, ReplaceRequest)` -> document or permission/failure
- `UpdateMetadataAsync(documentId, requestingUserId, MetadataRequest)`
- `DeleteAsync(documentId, requestingUserId)`
- `GetAccessibleAsync(requestingUserId, DocumentFilter, search, sort)`
- `GetSharedWithMeAsync(requestingUserId)`
- `ShareWithUserAsync(documentId, ownerId, recipientId)`
- `ShareWithTeamAsync(documentId, ownerId, teamIdentifier)`
- `RevokeUserShareAsync(documentId, ownerId, recipientId)`
- `RevokeTeamShareAsync(documentId, ownerId, teamIdentifier)`
- `OpenPreviewAsync(documentId, requestingUserId)`
- `OpenDownloadAsync(documentId, requestingUserId)`
- `GetAuditReportAsync(adminId, reportFilter)`

Every mutating or object-reading operation receives the authenticated user ID and returns a security-safe result. The service performs validation and authorization rather than relying on Razor page checks.
