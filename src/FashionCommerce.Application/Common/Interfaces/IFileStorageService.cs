namespace FashionCommerce.Application.Common.Interfaces;

public record FileUploadResult(bool Succeeded, string? Url, string? PublicId, string? ErrorMessage)
{
    public static FileUploadResult Success(string url, string publicId) => new(true, url, publicId, null);
    public static FileUploadResult Failure(string errorMessage) => new(false, null, null, errorMessage);
}

/// <summary>
/// Abstracts file storage (Cloudinary today; Azure Blob Storage would be a
/// second implementation of this same interface, per the spec's "either"
/// framing). Every caller in Application deals only in streams and URLs —
/// no provider-specific type ever crosses this boundary.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file into a logical folder (e.g. "products", "style-posts",
    /// "kyc-documents", "support-attachments") so files stay organized and
    /// folder-level access rules can be applied provider-side later.
    /// </summary>
    Task<FileUploadResult> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an image with the background automatically removed —
    /// the "AI Background Removal" feature from the spec.
    /// IMPORTANT: unlike every other AI feature in this codebase, this is
    /// NOT free. Cloudinary's background removal is a paid add-on
    /// (cloudinary_ai_background_removal) that must be enabled on the
    /// account — it will fail with a clear error on a free-tier account
    /// rather than silently doing nothing. Azure Blob Storage has no
    /// equivalent capability at all (it's raw object storage, no AI), so
    /// AzureBlobFileStorageService throws NotSupportedException if selected
    /// as the active provider.
    /// </summary>
    Task<FileUploadResult> UploadWithBackgroundRemovalAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
}
