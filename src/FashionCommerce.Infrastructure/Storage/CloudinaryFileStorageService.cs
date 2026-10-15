using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FashionCommerce.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.Storage;

public class CloudinarySettings
{
    public const string SectionName = "Storage:Cloudinary";
    public string CloudName { get; set; } = default!;
    public string ApiKey { get; set; } = default!;
    public string ApiSecret { get; set; } = default!;

    /// <summary>Server-side max upload size — matches the "File size validation" requirement.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
}

public class CloudinaryFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4", "video/quicktime"
    };

    private readonly Cloudinary _cloudinary;
    private readonly CloudinarySettings _settings;

    public CloudinaryFileStorageService(IOptions<CloudinarySettings> settings)
    {
        _settings = settings.Value;
        _cloudinary = new Cloudinary(new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret))
        {
            Api = { Secure = true }
        };
    }

    public async Task<FileUploadResult> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType))
            return FileUploadResult.Failure($"File type '{contentType}' is not allowed. Allowed types: images (jpeg/png/webp/gif) and video (mp4/mov).");

        if (fileStream.Length > _settings.MaxFileSizeBytes)
            return FileUploadResult.Failure($"File exceeds the maximum allowed size of {_settings.MaxFileSizeBytes / (1024 * 1024)}MB.");

        try
        {
            var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

            RawUploadResult uploadResult = isVideo
                ? await _cloudinary.UploadAsync(new VideoUploadParams
                {
                    File = new FileDescription(fileName, fileStream),
                    Folder = $"fashioncommerce/{folder}"
                }, cancellationToken)
                : await _cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(fileName, fileStream),
                    Folder = $"fashioncommerce/{folder}",
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto") // automatic compression/format optimization
                }, cancellationToken);

            if (uploadResult.Error is not null)
                return FileUploadResult.Failure(uploadResult.Error.Message);

            return FileUploadResult.Success(uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
        }
        catch (Exception ex)
        {
            return FileUploadResult.Failure($"Upload failed: {ex.Message}");
        }
    }

    public async Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
            return result.Result == "ok";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Uses Cloudinary's cloudinary_ai_background_removal add-on. This is a
    /// PAID feature — Cloudinary returns a clear error (surfaced via
    /// uploadResult.Error below) if the add-on isn't enabled on the
    /// account, rather than silently applying no effect. Unlike every
    /// other file upload in this codebase, this specific call has a real
    /// per-image cost once enabled.
    /// </summary>
    public async Task<FileUploadResult> UploadWithBackgroundRemovalAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType) || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return FileUploadResult.Failure("Background removal only applies to images (jpeg/png/webp/gif).");

        if (fileStream.Length > _settings.MaxFileSizeBytes)
            return FileUploadResult.Failure($"File exceeds the maximum allowed size of {_settings.MaxFileSizeBytes / (1024 * 1024)}MB.");

        try
        {
            var uploadResult = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"fashioncommerce/{folder}",
                BackgroundRemoval = "cloudinary_ai",
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            }, cancellationToken);

            if (uploadResult.Error is not null)
            {
                // The most common failure here is the add-on not being enabled —
                // surfaced as-is rather than a generic message, since the fix
                // (enable the add-on in the Cloudinary console) is specific.
                return FileUploadResult.Failure(uploadResult.Error.Message);
            }

            return FileUploadResult.Success(uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
        }
        catch (Exception ex)
        {
            return FileUploadResult.Failure($"Background removal upload failed: {ex.Message}");
        }
    }
}
