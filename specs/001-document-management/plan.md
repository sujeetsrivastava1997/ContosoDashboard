# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-11-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-document-management/spec.md`

## Summary

Implement secure document upload, browsing, search, preview/download, metadata replacement, sharing, task/dashboard integration, notifications, audit reporting, and role-based management within the existing ASP.NET Core Blazor Server application.

The design follows the repository's layered architecture: EF Core entities and mappings in the data layer, reusable authorization and document behavior in services, Razor pages/components for presentation, and infrastructure abstractions for local file storage and offline malware scanning. Existing project membership remains the project authorization source, while department membership represents a team share snapshot because the starter code has no separate Team entity.

## Technical Context

**Language/Version**: C# with .NET 10.0 (project target; repository documentation also mentions .NET 8, so the implementation follows the current `.csproj`)
**Primary Dependencies**: ASP.NET Core Blazor Server, EF Core 10, SQL Server/LocalDB in production configuration, EF InMemory in development, Bootstrap 5.3
**Storage**: EF Core metadata plus local filesystem under `AppData/uploads` outside `wwwroot`; no external subscriptions or network services
**Testing**: No test project currently exists; add narrow xUnit or MSTest coverage appropriate to the repository SDK, or document no test coverage only for behavior where automation is impractical
**Target Platform**: Web application on Windows with SQL Server LocalDB or the existing SQL Server production configuration
**Project Type**: Web application (Blazor Server) with a small authorized file-serving surface
**Performance Goals**: 25 MB uploads within 30 seconds; list and permitted search within 2 seconds for 500 documents; preview start within 3 seconds
**Constraints**: 25 MB per file; allowed PDF, Word, Excel, PowerPoint, text, JPEG, and PNG types; secure outside-web-root storage; deterministic compensation when file/database operations fail; no orphaned metadata or files
**Scale/Scope**: Training-scale user/project/task data; document lists, search, sharing, audit, task and dashboard integrations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security and Authorization**: PASS. Every document query and download/preview operation will require service-level authorization. Role checks cover Employee, Team Lead, Project Manager, and Administrator. Page attributes and route authorization are defense in depth, not the sole control.
- **II. Offline-First and Abstraction**: PASS. File storage and malware scanning use interfaces with local implementations. EF Core remains the metadata/data abstraction.
- **III. Layered Architecture**: PASS. Models define domain data, `ApplicationDbContext` owns EF mappings, `DocumentService` owns reusable behavior, and Razor components coordinate UI only.
- **IV. Testability and Validation**: PASS, with explicit planned tests for authorization, upload validation/compensation, sharing snapshots/revocation, and file operations. A test project must be added before implementation is considered complete.
- **V. Training-Context Simplicity**: PASS. The design uses existing Blazor/EF patterns and only the abstractions needed for storage and offline scanning.

**Technical Constraints**:
- Storage outside `wwwroot`, GUID names, MIME/extension validation, and file-size limits.
- Sensitive routes require authorization-aware endpoints.
- Local development remains runnable with mock authentication and in-memory database.
- Security-sensitive design includes a threat/misuse review in `research.md`.

**Gate Result**: PASS with no Constitution violations requiring `Complexity Tracking`.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── document-endpoints.md
├── spec.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs
│   ├── DocumentShare.cs
│   ├── DocumentShareRecipient.cs
│   ├── DocumentTag.cs
│   └── DocumentActivity.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Services/
│   ├── DocumentService.cs
│   ├── IFileStorageService.cs
│   ├── LocalFileStorageService.cs
│   ├── IMalwareScanService.cs
│   └── LocalMalwareScanService.cs
├── Pages/
│   ├── Documents.razor
│   ├── SharedDocuments.razor
│   ├── DocumentUpload.razor
│   └── DocumentReports.razor
├── Endpoints/
│   └── DocumentEndpoints.cs
└── AppData/
    └── uploads/
```

**Structure Decision**: Extend the existing single-project Blazor Server application. Add document-specific models, service and storage abstractions, Razor pages, and MapGet endpoint methods; do not introduce a separate API project or repository layer for this training feature.

## Complexity Tracking

No constitution violations require justification.

## Post-Design Constitution Re-check

- **I. Security and Authorization**: PASS. The data model snapshots share recipients, revocation is explicit, and the endpoint/service contract requires authorization for every direct-object operation.
- **II. Offline-First and Abstraction**: PASS. Storage and scanning remain interface-backed; no external runtime dependency is introduced.
- **III. Layered Architecture**: PASS. Data-model, service, endpoint, and Razor responsibilities remain separated.
- **IV. Testability and Validation**: PASS. `quickstart.md` defines scenarios and the plan identifies the authorization, compensation, sharing, and reporting test areas.
- **V. Training-Context Simplicity**: PASS. Department-based snapshotting avoids a new team subsystem; project/task/document operations extend existing patterns.

**Post-Design Gate Result**: PASS.
