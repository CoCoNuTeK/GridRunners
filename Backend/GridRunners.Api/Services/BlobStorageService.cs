using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GridRunners.Api.Configuration;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace GridRunners.Api.Services;

public class BlobStorageService
{
    private const int MaxImageDimension = 1024;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        BlobServiceClient blobServiceClient,
        AzureStorageOptions options,
        ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _options = options;
        _logger = logger;
        
        _logger.LogInformation("BlobStorageService initialized with storage account {AccountName}, container {ContainerName}",
            options.AccountName, options.ContainerName);
    }

    public async Task<string> UploadUserImageAsync(int userId, IFormFile file)
    {
        try
        {
            // Get container reference
            var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            
            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                _logger.LogInformation("Creating blob container {ContainerName}", _options.ContainerName);
                await containerClient.CreateAsync();
            }

            // Validate file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                throw new InvalidOperationException("Only .jpg, .jpeg and .png files are allowed");
            }

            // Generate unique blob name
            var blobName = $"{_options.UserImagesPath}/{userId}/{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Process and upload the image
            using var image = await Image.LoadAsync(file.OpenReadStream());
            
            // Resize if needed while maintaining aspect ratio
            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                var ratio = Math.Min((double)MaxImageDimension / image.Width, (double)MaxImageDimension / image.Height);
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);
                
                image.Mutate(x => x.Resize(newWidth, newHeight));
            }

            // Convert to memory stream for upload
            using var ms = new MemoryStream();
            var format = image.Metadata.DecodedImageFormat ?? JpegFormat.Instance;
            await image.SaveAsync(ms, format);
            ms.Position = 0;

            // Upload the processed image
            await blobClient.UploadAsync(ms, new BlobHttpHeaders
            {
                ContentType = file.ContentType
            });
            
            _logger.LogInformation("Successfully uploaded image for user {UserId}: {BlobUri}", userId, blobClient.Uri);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading user image for user {UserId}", userId);
            throw;
        }
    }

    public async Task DeleteUserImageAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            var uri = new Uri(imageUrl);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            
            // Extract blob name from the full URL (remove query parameters first)
            var blobPath = uri.GetLeftPart(UriPartial.Path);
            var blobName = new Uri(blobPath).LocalPath.TrimStart('/').Split('/', 2)[1];
            var blobClient = containerClient.GetBlobClient(blobName);
            
            await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation("Successfully deleted image at URL {ImageUrl}", imageUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image at URL {ImageUrl}", imageUrl);
            throw;
        }
    }
} 