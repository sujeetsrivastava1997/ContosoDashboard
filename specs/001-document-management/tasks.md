# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-management/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Verify the existing .NET 10 project configuration and restore dependencies in ContosoDashboard/ContosoDashboard.csproj
- [ ] T002 Create document feature directories for Models, Services, Pages, Endpoints, and AppData/uploads under ContosoDashboard/
- [ ] T003 [P] Add an xUnit test project for ContosoDashboard in tests/ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
- [ ] T004 [P] Add EF Core and ASP.NET test package references to tests/ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
- [ ] T005 Configure `AppData/uploads` as an ignored runtime data directory in .gitignore and create the directory placeholder under ContosoDashboard/AppData/uploads/

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before any user story is implemented.

- [ ] T006 Extend ContosoDashboard/Data/ApplicationDbContext.cs with Document, DocumentTag, DocumentShare, DocumentShareRecipient, and DocumentActivity DbSets
- [ ] T007 Add EF Core mappings, relationships, delete behavior, and indexes for document entities in ContosoDashboard/Data/ApplicationDbContext.cs
- [ ] T008 [P] Implement Document, DocumentTag, DocumentShare, DocumentShareRecipient, and DocumentActivity models with constrained fields from data-model.md, including Document.DocumentId/Title/Description/Category/ProjectId/TaskId/UploaderUserId/UploadedUtc/FileSizeBytes/MimeType/StorageKey/OriginalFileName/UpdatedUtc; DocumentTag.DocumentTagId/DocumentId/Tag; DocumentShare.DocumentShareId/DocumentId/SharedByUserId/ShareType/TeamIdentifier/SharedUtc; DocumentShareRecipient.DocumentShareRecipientId/DocumentShareId/RecipientUserId/RevokedUtc/RevokedByUserId; and DocumentActivity.DocumentActivityId/DocumentId/DocumentTitleSnapshot/ActorUserId/ActivityType/ActivityUtc in ContosoDashboard/Models/
- [ ] T009 [P] Add DocumentCategory and DocumentActivityType enums plus request/filter/result types for document operations in ContosoDashboard/Models/ or ContosoDashboard/Services/
- [ ] T010 Define IFileStorageService and IMalwareScanService contracts with local implementation-ready methods in ContosoDashboard/Services/IFileStorageService.cs and ContosoDashboard/Services/IMalwareScanService.cs
- [ ] T011 Implement GUID-based local storage outside web root and compensating file deletion in ContosoDashboard/Services/LocalFileStorageService.cs
- [x] T012 Implement the offline training malware scanner with allowlisted MIME/extension and unsafe signature validation in ContosoDashboard/Services/LocalMalwareScanService.cs
- [x] T013 [P] Register document dependencies, HttpContextAccessor, storage, scanner, and DocumentService in ContosoDashboard/Program.cs
- [ ] T014 [P] Add document notification types and document links to the existing notification model and notification service as needed in ContosoDashboard/Models/Notification.cs and ContosoDashboard/Services/NotificationService.cs
- [x] T015 Define the central IDocumentService contract and authorization/result primitives in ContosoDashboard/Services/DocumentService.cs
- [ ] T016 Implement shared document authorization for owners, active share recipients, project members/managers, task participants, Team Leads for same-department uploaders, and administrators in ContosoDashboard/Services/DocumentService.cs
- [ ] T017 [P] Add document routes to ContosoDashboard/Shared/NavMenu.razor and a document feature stylesheet hook in ContosoDashboard/wwwroot/css/site.css
- [ ] T018 [P] Add an in-memory EF test fixture and identity helpers to tests/ContosoDashboard.Tests/

**Checkpoint**: Foundation ready; no user-story work can begin until storage, entities, authorization, and notification infrastructure are complete.

## Phase 3: User Story 1 - Upload and organize documents (Priority: P1) 🎯 MVP

**Goal**: Authenticated employees can securely upload one or more supported files with required metadata and find the result in My Documents.

**Independent Test**: An authenticated employee uploads a valid PDF of at most 25 MB with a title and category and sees it in My Documents with uploader, date, size, MIME type, category, and project.

### Tests for User Story 1

- [ ] T019 [P] [US1] Test upload rejects missing title/category, unsupported type, and files over 25 MB in tests/ContosoDashboard.Tests/DocumentUploadTests.cs
- [ ] T020 [P] [US1] Test upload compensation removes a newly stored file when metadata persistence fails in tests/ContosoDashboard.Tests/DocumentUploadTests.cs
- [ ] T021 [P] [US1] Test task uploads inherit the task project and project-invalid uploads are rejected in tests/ContosoDashboard.Tests/DocumentUploadTests.cs

### Implementation for User Story 1

- [ ] T022 [P] [US1] Implement upload request validation for Document.Title/Description/Category/ProjectId/TaskId/FileSizeBytes/MimeType/StorageKey/OriginalFileName from data-model.md and allowed 1..26,214,400 byte files in ContosoDashboard/Services/DocumentService.cs
- [x] T023 [US1] Implement `UploadAsync` in ContosoDashboard/Services/DocumentService.cs, including GUID storage, malware scan, database persistence, activity creation, and compensation
- [ ] T024 [P] [US1] Implement My Documents queries with owner and permitted project/share access, category/project/date filtering, and sort options in ContosoDashboard/Services/DocumentService.cs
- [ ] T025 [P] [US1] Create the multi-file upload and progress UI in ContosoDashboard/Pages/DocumentUpload.razor
- [ ] T026 [P] [US1] Create the My Documents list UI with title, category, upload date, file size, and project fields in ContosoDashboard/Pages/Documents.razor
- [ ] T027 [US1] Add validation feedback and actionable security-neutral upload errors to ContosoDashboard/Pages/DocumentUpload.razor
- [ ] T028 [US1] Record upload DocumentActivity activity and notify project members when a project document is uploaded in ContosoDashboard/Services/DocumentService.cs

**Checkpoint**: US1 is complete and independently testable for valid upload, rejected upload, My Documents visibility, and project/task association.

## Phase 4: User Story 2 - Browse, find, and access documents (Priority: P1)

**Goal**: Authorized employees can browse, filter, sort, search, preview, and download permitted documents without exposing unauthorized data.

**Independent Test**: An authorized employee can filter/search project documents, preview an authorized PDF or image, and download an authorized file while unauthorized documents remain absent.

### Tests for User Story 2

- [ ] T029 [P] [US2] Test list, sort, filter, and search exclude unauthorized documents in tests/ContosoDashboard.Tests/DocumentBrowseTests.cs
- [ ] T030 [P] [US2] Test preview/download authorization and preview MIME restrictions in tests/ContosoDashboard.Tests/DocumentAccessTests.cs

### Implementation for User Story 2

- [ ] T031 [P] [US2] Implement permitted list/sort/filter/search operations across Document.Title/Description/DocumentTag.Tag/UploaderUserId and project name in ContosoDashboard/Services/DocumentService.cs
- [ ] T032 [P] [US2] Add project document loading and project-member authorization to ContosoDashboard/Pages/ProjectDetails.razor and ContosoDashboard/Services/DocumentService.cs
- [ ] T033 [US2] Implement the authorized preview and download endpoint handlers in ContosoDashboard/Endpoints/DocumentEndpoints.cs for GET `/documents/{documentId}/preview` and `/documents/{documentId}/download`
- [ ] T034 [US2] Implement safe stream preparation, nosniff headers, attachment filename, and 403/404 responses in ContosoDashboard/Endpoints/DocumentEndpoints.cs
- [ ] T035 [US2] Map document endpoints after authentication and authorization middleware in ContosoDashboard/Program.cs
- [ ] T036 [P] [US2] Add authorized preview/download actions and search/filter controls to ContosoDashboard/Pages/Documents.razor
- [ ] T037 [US2] Record successful preview and download DocumentActivity events in ContosoDashboard/Services/DocumentService.cs

**Checkpoint**: US2 is complete and independently testable for browse/search/filter/preview/download and unauthorized access denial.

## Phase 5: User Story 3 - Manage and share documents (Priority: P2)

**Goal**: Owners and applicable managers can update, replace, delete, and share documents, with immediate snapshot-based access control and revocation.

**Independent Test**: An owner edits metadata and replaces/deletes a file or shares/revokes access; recipients receive notifications and lose access immediately on revoke.

### Tests for User Story 3

- [ ] T038 [P] [US3] Test owner, Team Lead same-department, and Project Manager deletion permissions in tests/ContosoDashboard.Tests/DocumentManagementTests.cs
- [ ] T039 [P] [US3] Test replacement compensation and removal of the old file after successful metadata/storage update in tests/ContosoDashboard.Tests/DocumentManagementTests.cs
- [ ] T040 [P] [US3] Test direct/team shares snapshot recipients and revoke immediately removes access in tests/ContosoDashboard.Tests/DocumentShareTests.cs

### Implementation for User Story 3

- [ ] T041 [P] [US3] Implement metadata update for Document.Title/Description/Category/DocumentTag.Tag values with server validation in ContosoDashboard/Services/DocumentService.cs
- [ ] T042 [US3] Implement file replacement for Document.FileSizeBytes/MimeType/StorageKey/OriginalFileName/UpdatedUtc with compensation and old-file cleanup in ContosoDashboard/Services/DocumentService.cs
- [ ] T043 [US3] Implement confirmed permanent delete for owner, applicable Project Manager, and Team Lead permissions in ContosoDashboard/Services/DocumentService.cs
- [ ] T044 [P] [US3] Implement direct and department team share snapshot creation using DocumentShare and DocumentShareRecipient in ContosoDashboard/Services/DocumentService.cs
- [ ] T045 [P] [US3] Implement owner-only individual/team revocation and active-recipient filtering in ContosoDashboard/Services/DocumentService.cs
- [ ] T046 [P] [US3] Add in-app share/revoke and project notifications through ContosoDashboard/Services/NotificationService.cs
- [ ] T047 [P] [US3] Create the document management and Shared with Me UI in ContosoDashboard/Pages/SharedDocuments.razor
- [ ] T048 [US3] Add edit, replace, delete confirmation, share, and revoke controls to ContosoDashboard/Pages/Documents.razor
- [ ] T049 [US3] Record MetadataUpdated, Replaced, Deleted, Shared, and Revoked DocumentActivity activity while preserving audit snapshots in ContosoDashboard/Services/DocumentService.cs

**Checkpoint**: US3 is complete and independently testable for metadata, replacement, deletion, sharing, revocation, and role restrictions.

## Phase 6: User Story 4 - Integrate documents with tasks and dashboard (Priority: P2)

**Goal**: Task users can attach/view/upload documents, and dashboard users can see recent accessible documents and counts.

**Independent Test**: A task participant uploads or attaches a permitted document and sees it from the task and dashboard with the task's project association.

### Tests for User Story 4

- [ ] T050 [P] [US4] Test task document attachment/upload authorization and task project inheritance in tests/ContosoDashboard.Tests/TaskDocumentTests.cs
- [ ] T051 [P] [US4] Test dashboard returns at most five authorized recent documents and a correct accessible count in tests/ContosoDashboard.Tests/DashboardDocumentTests.cs

### Implementation for User Story 4

- [ ] T052 [P] [US4] Add task document list/upload/attach operations to ContosoDashboard/Services/DocumentService.cs
- [ ] T053 [P] [US4] Integrate authorized task document controls and Document.ProjectId inheritance into ContosoDashboard/Pages/Tasks.razor and ContosoDashboard/Pages/TaskDetails.razor
- [ ] T054 [US4] Extend the dashboard document query and summary model to expose up to five permitted recent documents and document count in ContosoDashboard/Services/DashboardService.cs
- [ ] T055 [P] [US4] Add recent document and document count presentation to ContosoDashboard/Pages/Index.razor
- [ ] T056 [US4] Add document navigation and cross-links in ContosoDashboard/Shared/NavMenu.razor and task/project pages

**Checkpoint**: US4 is complete and independently testable for task association, task document visibility, and dashboard document summaries.

## Phase 7: User Story 5 - Audit document activity (Priority: P3)

**Goal**: Administrators can review retained document activity and generate document type, uploader, and access reports.

**Independent Test**: An administrator sees uploads, downloads, deletions, and shares with actor/timestamp and generates report summaries.

### Tests for User Story 5

- [ ] T057 [P] [US5] Test report generation and administrator-only access in tests/ContosoDashboard.Tests/DocumentReportTests.cs
- [ ] T058 [P] [US5] Test audit rows survive document deletion and share revocation in tests/ContosoDashboard.Tests/DocumentAuditTests.cs

### Implementation for User Story 5

- [ ] T059 [P] [US5] Implement administrator audit report queries and aggregate document type/uploader/access metrics in ContosoDashboard/Services/DocumentService.cs
- [ ] T060 [P] [US5] Create the administrator report UI in ContosoDashboard/Pages/DocumentReports.razor
- [ ] T061 [US5] Preserve DocumentActivity.DocumentId/DocumentTitleSnapshot/ActorUserId/ActivityType/ActivityUtc audit data after delete/revoke operations in ContosoDashboard/Services/DocumentService.cs
- [ ] T062 [US5] Add reporting route authorization for the Administrator role in ContosoDashboard/Program.cs and ContosoDashboard/Pages/DocumentReports.razor

**Checkpoint**: US5 is complete and independently testable for administrator reports and retained audit records.

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T063 [P] Run `dotnet build ContosoDashboard/ContosoDashboard.csproj` and resolve compilation warnings/errors
- [ ] T064 [P] Run `dotnet test` and resolve document feature test failures
- [ ] T065 Review and harden all service direct-object operations against unauthorized IDs in ContosoDashboard/Services/DocumentService.cs
- [ ] T066 Verify storage, search, preview, and list performance targets against quickstart.md scenarios
- [ ] T067 [P] Update README.md and specs/001-document-management/quickstart.md with document routes, test users, and validation commands
- [ ] T068 Execute the quickstart.md validation scenarios for upload, browse, manage/share, preview/download, dashboard, and reports
- [ ] T069 Review security headers and endpoint output for path disclosure, nosniff, attachment filenames, and security-neutral errors
- [ ] T070 Remove temporary diagnostics and confirm no partial metadata or orphaned storage remains after failure scenarios

## Dependencies & Execution Order

- **Phase 1 (Setup)**: No dependencies; establishes test and runtime structure.
- **Phase 2 (Foundational)**: Depends on Setup; blocks all user-story phases.
- **User stories**: Depend on Foundational completion. US1 and US2 are P1 and are the recommended first implementation pair; US3 and US4 are P2 and may proceed after P1 or in parallel once foundation is stable; US5 is P3.
- **Polish**: Depends on all selected user stories being complete.

### User Story Dependencies

- **US1 - Upload and organize**: Independent after Foundation.
- **US2 - Browse and access**: Uses documents created by US1 but remains independently testable once the foundation and document model exist.
- **US3 - Manage and share**: Uses US1 document records and US2 authorization behavior; independent ownership/share tests are in its phase.
- **US4 - Task and dashboard**: Uses US1 upload and US2 authorization, but is an independent task/dashboard presentation increment.
- **US5 - Audit**: Consumes document activities emitted by US1-US4 and is otherwise independently reportable.

## Parallel Execution Examples

### User Story 1

- T019-T021 run together for upload, compensation, and task tests.
- T022, T024, T025, and T026 can proceed in separate files after T008-T016 establish models/services.

### User Story 2

- T029-T030 run together for browse and access tests.
- T032, T033-T034, and T036 can proceed in separate project, endpoint, and Razor files.

### User Story 3

- T038-T040 run together for management/share tests.
- T041, T044, T045, T047, and T048 can proceed in separate service/UI files after foundation.

### User Story 4

- T050-T051 run together for task/dashboard tests.
- T052-T053 and T054-T055 can proceed separately in service/task and dashboard files.

### User Story 5

- T057-T058 run together for report/audit tests.
- T059-T060 can proceed separately in service and Razor files.

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Phases 1-2.
2. Complete Phase 3 (US1).
3. Validate T019-T028 independently using the upload quickstart.
4. Stop for a working secure upload/My Documents demo before later stories.

### Incremental Delivery

1. Foundation and US1 for the MVP.
2. Add US2 browse/search/preview/download.
3. Add US3 management/sharing.
4. Add US4 task/dashboard integration.
5. Add US5 audit reporting.

### Parallel Team Strategy

1. Team completes Setup and Foundational together.
2. Developer A implements US1; Developer B implements US2; Developer C prepares US3 service/tests.
3. Merge story increments at phase checkpoints and run the full build/test suite before polish.

## Format Validation

- Every task uses `- [ ]`, a sequential task ID, and exact implementation or test file paths.
- Story tasks in Phases 3-7 include `[US1]` through `[US5]`; setup, foundation, and polish tasks omit story labels.
- Parallel tasks include `[P]` only when they operate on different files or independent test areas.
