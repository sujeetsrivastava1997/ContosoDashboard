# Quickstart Validation

## Prerequisites

- Run from the repository root on Windows.
- The project targets the framework declared in `ContosoDashboard/ContosoDashboard.csproj`.
- Development use can run with EF InMemory; no SQL Server LocalDB is required for the development branch.
- Ensure the `AppData/uploads` directory can be created and written by the application.
- Use the mock login users documented in `README.md`.

## Start

```powershell
Set-Location ContosoDashboard
dotnet run
```

Open the HTTPS URL emitted by `dotnet run` and log in with a seeded mock user.

## Upload and Browse

1. Log in as Employee `Ni Kang`.
2. Open the document upload page.
3. Select a valid PDF smaller than 25 MB.
4. Enter title and category, optionally select the seeded project.
5. Submit once.

**Expected**:
- Progress is shown.
- Success feedback appears.
- The file is in My Documents with title, category, uploader, upload date, size, MIME type, and project.
- The source filename is not used as the storage path.

## Validation Failure

Repeat the upload flow with:
- an unsupported `.exe` file;
- a file over 25 MB;
- missing title or category.

**Expected**:
- An actionable, security-neutral error appears.
- No metadata row or accessible file remains.

## Project and Task Access

1. Log in as Employee `Ni Kang`.
2. Open the seeded project and verify project documents are visible.
3. Open an authorized task and upload a PDF.

**Expected**:
- Project members can list/download authorized project documents.
- The task upload inherits the task's project.
- Unauthorized project/task documents are absent.

## Manage and Share

1. Upload a document as Ni Kang.
2. Edit metadata and replace the file.
3. Share it with another user.

**Expected**:
- The owner edits/replaces successfully.
- The recipient sees an in-app notification and Shared with Me entry.
- The previous file is removed after successful replacement.
- Revoking the share immediately removes list, search, preview, download, and Shared with Me access.

4. Log in as Team Lead `Floris Kregel` and manage a document uploaded by a team member in the same department.
5. Log in as Project Manager `Camille Nicole` and delete a project document.

**Expected**:
- Permitted management succeeds.
- Unauthorized management is rejected.
- Deletion removes metadata and file.

## Team Snapshot

1. Share a document with the Engineering department/team.
2. Add or simulate a later user in Engineering.
3. Confirm the later user does not see it.
4. Reshare by the owner.

**Expected**:
- Only snapshot recipients at the original share gain access.
- Resharing snapshots the current membership.

## Preview and Download

1. Open an authorized PDF or image.
2. Use preview and download routes.
3. Repeat as an unauthorized user.

**Expected**:
- Authorized users can preview/download.
- Unauthorized users receive forbidden/not-found behavior and no storage path.

## Dashboard, Audit, and Reports

1. Add several permitted documents.
2. Open the dashboard.
3. Log in as Administrator and open document activity/reporting.

**Expected**:
- Dashboard shows up to five accessible recent documents and a document count.
- Audit/report data includes upload, download, deletion, share/revoke actions with actor and timestamp.

## Validation Commands

After implementation adds tests:

```powershell
dotnet build ContosoDashboard/ContosoDashboard.csproj
dotnet test
```
