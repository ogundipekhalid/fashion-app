using Asp.Versioning;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

public record FileUploadResponse(string Url, string PublicId);

/// <summary>
/// The upload half of the "client uploads first, then references the URL"
/// pattern used throughout Catalog (product images), Styling (look photos),
/// Support (ticket attachments), and Vendor KYC documents. One endpoint,
/// parameterized by folder, rather than a separate upload action per feature.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
public class FilesController : ApiControllerBase
{
    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "products", "style-posts", "reviews", "support-attachments", "kyc-documents", "categories", "brands", "avatars"
    };

    private readonly IFileStorageService _fileStorageService;

    public FilesController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(15_000_000)] // 15MB hard cap at the ASP.NET Core request-body level, ahead of IFileStorageService's own check
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder)
    {
        if (file.Length == 0)
            return BadRequest(ApiResponse.FailureResponse("File is empty."));

        if (!AllowedFolders.Contains(folder))
            return BadRequest(ApiResponse.FailureResponse($"Invalid folder. Allowed: {string.Join(", ", AllowedFolders)}"));

        await using var stream = file.OpenReadStream();
        var result = await _fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, folder.ToLowerInvariant());

        if (!result.Succeeded)
            return BadRequest(ApiResponse.FailureResponse(result.ErrorMessage ?? "Upload failed."));

        return Ok(ApiResponse<FileUploadResponse>.SuccessResponse(new FileUploadResponse(result.Url!, result.PublicId!)));
    }

    /// <summary>
    /// AI background removal for product photos. Vendor-only, and
    /// restricted to the "products" folder — this isn't a general-purpose
    /// upload variant. IMPORTANT: unlike every other AI feature in this
    /// codebase, this is NOT free — it requires Cloudinary's paid
    /// cloudinary_ai_background_removal add-on. On an account without it
    /// enabled, or when the active provider is Azure Blob, this returns a
    /// clear error rather than silently uploading the image unmodified.
    /// </summary>
    [HttpPost("upload-with-background-removal")]
    [RequestSizeLimit(15_000_000)]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> UploadWithBackgroundRemoval(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(ApiResponse.FailureResponse("File is empty."));

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _fileStorageService.UploadWithBackgroundRemovalAsync(stream, file.FileName, file.ContentType, "products");

            if (!result.Succeeded)
                return BadRequest(ApiResponse.FailureResponse(result.ErrorMessage ?? "Background removal failed."));

            return Ok(ApiResponse<FileUploadResponse>.SuccessResponse(new FileUploadResponse(result.Url!, result.PublicId!)));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ApiResponse.FailureResponse(ex.Message));
        }
    }
}
