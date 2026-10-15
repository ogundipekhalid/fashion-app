using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FashionCommerce.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FashionCommerce.Infrastructure.Storage;

public class AzureBlobStorageSettings
{
    public const string SectionName = "Storage:AzureBlob";
    public string ConnectionString { get; set; } = default!;
    public string ContainerName { get; set; } = "fashioncommerce";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>
/// The Azure Blob Storage alternative to Cloudinary — same
/// IFileStorageService contract, so switching providers is a one-line DI
/// registration change (see DependencyInjection.cs's Storage:Provider
/// switch), nothing else in the codebase needs to know which is active.
/// Unlike Cloudinary, Azure Blob does no automatic image optimization —
/// that trade-off (simpler/cheaper storage vs. built-in transformations)
/// is exactly why the spec offered both as options rather than mandating one.
/// </summary>
public class AzureBlobFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4", "video/quicktime"
    };

    private readonly BlobContainerClient _containerClient;
    private readonly AzureBlobStorageSettings _settings;

    public AzureBlobFileStorageService(IOptions<AzureBlobStorageSettings> settings)
    {
        _settings = settings.Value;
        var serviceClient = new BlobServiceClient(_settings.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(_settings.ContainerName);
    }

    public async Task<FileUploadResult> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType))
            return FileUploadResult.Failure($"File type '{contentType}' is not allowed. Allowed types: images (jpeg/png/webp/gif) and video (mp4/mov).");

        if (fileStream.Length > _settings.MaxFileSizeBytes)
            return FileUploadResult.Failure($"File exceeds the maximum allowed size of {_settings.MaxFileSizeBytes / (1024 * 1024)}MB.");

        try
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

            // Unique blob name: folder-prefixed, GUID-disambiguated so two
            // uploads of the same original filename never collide.
            var extension = Path.GetExtension(fileName);
            var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";

            var blobClient = _containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(fileStream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, cancellationToken);

            return FileUploadResult.Success(blobClient.Uri.ToString(), blobName);
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
            var blobClient = _containerClient.GetBlobClient(publicId); // publicId is the blob name (folder/guid.ext) for this provider
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return response.Value;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Azure Blob Storage is raw object storage with no built-in image AI — this capability only exists on the Cloudinary provider.</summary>
    public Task<FileUploadResult> UploadWithBackgroundRemovalAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Background removal is not available on the Azure Blob storage provider — it requires Cloudinary's AI background removal add-on. Switch Storage:Provider to \"Cloudinary\" to use this feature.");
}
