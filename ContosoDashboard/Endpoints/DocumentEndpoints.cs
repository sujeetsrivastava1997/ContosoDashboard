using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContosoDashboard.Services;

namespace ContosoDashboard.Endpoints;

[ApiController]
[Authorize]
[Route("documents")]
public sealed class DocumentEndpointsController(IDocumentService documents) : ControllerBase
{
    [HttpGet("{documentId:int}/preview")]
    public async Task<IActionResult> Preview(int documentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        var result = await documents.OpenPreviewAsync(documentId, userId, cancellationToken);
        if (!result.Succeeded)
            return StatusFor(result.ErrorCode);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = BuildDisposition(result.Document!, inline: true);
        return new FileStreamResult(result.Stream!, result.Document!.MimeType) { EnableRangeProcessing = true };
    }

    [HttpGet("{documentId:int}/download")]
    public async Task<IActionResult> Download(int documentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        var result = await documents.OpenDownloadAsync(documentId, userId, cancellationToken);
        if (!result.Succeeded)
            return StatusFor(result.ErrorCode);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = BuildDisposition(result.Document!, inline: false);
        return new FileStreamResult(result.Stream!, result.Document!.MimeType) { EnableRangeProcessing = true };
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private IActionResult StatusFor(string? errorCode) => errorCode switch
    {
        "forbidden" => Forbid(),
        "preview_unsupported" => BadRequest(new ProblemDetails { Status = 400, Title = "Preview unavailable", Detail = "This file type cannot be previewed." }),
        _ => NotFound()
    };

    private static string BuildDisposition(ContosoDashboard.Models.Document document, bool inline)
    {
        var extension = document.MimeType switch
        {
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "text/plain" => ".txt",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => ".bin"
        };
        var baseName = Path.GetFileNameWithoutExtension(document.OriginalFileName ?? string.Empty);
        baseName = Regex.Replace(baseName, @"[^A-Za-z0-9 _.-]", "_").Trim();
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "document";
        var filename = $"{baseName}{extension}";
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
        {
            FileName = filename
        };
        return disposition.ToString();
    }
}
