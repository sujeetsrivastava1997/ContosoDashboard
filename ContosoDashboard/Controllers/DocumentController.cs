using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentController(IDocumentService documentService) : ControllerBase
{
    /// <summary>Uploads a supported document for the authenticated user.</summary>
    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] DocumentCategory category,
        [FromForm] int? projectId,
        [FromForm] int? taskId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid file",
                detail: "A non-empty file is required.");
        }

        if (file.Length > 25 * 1024 * 1024)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "File too large",
                detail: "The file must be no larger than 25 MB.");
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "A valid authenticated user is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await documentService.UploadAsync(
            userId,
            new DocumentUploadRequest
            {
                Title = title,
                Description = description,
                Category = category,
                ProjectId = projectId,
                TaskId = taskId,
                OriginalFileName = file.FileName,
                MimeType = file.ContentType,
                FileSizeBytes = file.Length,
                FileStream = stream
            },
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.ErrorCode switch
            {
                "unsupported_file_type" => StatusCodes.Status415UnsupportedMediaType,
                "invalid_file_size" => StatusCodes.Status400BadRequest,
                "unsafe_file" => StatusCodes.Status400BadRequest,
                "unauthorized" => StatusCodes.Status403Forbidden,
                "invalid_project" or "invalid_task" => StatusCodes.Status400BadRequest,
                "invalid_title" or "invalid_description" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            return Problem(
                statusCode: statusCode,
                title: statusCode == StatusCodes.Status500InternalServerError ? "Upload failed" : "Invalid upload",
                detail: result.ErrorMessage);
        }

        var document = result.Document!;
        var response = new DocumentUploadResponse(
            document.DocumentId,
            document.Title,
            document.Category,
            document.UploadedUtc,
            document.FileSizeBytes,
            document.MimeType);

        return Created($"/api/documents/{document.DocumentId}", response);
    }
}

/// <summary>Response returned after a successful document upload.</summary>
public sealed record DocumentUploadResponse(
    int DocumentId,
    string Title,
    DocumentCategory Category,
    DateTimeOffset UploadedUtc,
    long FileSizeBytes,
    string MimeType);
