using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using GridRunners.Api.Configuration;
using Microsoft.AspNetCore.Http;

namespace GridRunners.Api.Services;

public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        AzureStorageOptions options,
        ILogger<BlobStorageService> logger)
    {
        _options = options;
        _logger = logger;
        
        // Use DefaultAzureCredential for authentication
        _blobServiceClient = new BlobServiceClient(
            new Uri($"https://{options.AccountName}.blob.core.windows.net"),
            new DefaultAzureCredential());
    }

    private async Task<(string sasToken, DateTime expiration)> GenerateSasTokenInternalAsync(BlobClient blobClient)
    {
        var expiration = DateTime.UtcNow.AddHours(24);
        var startsOn = DateTime.UtcNow.AddMinutes(-5);
        
        // Create a SAS token that's valid for 24 hours
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b", // b for blob
            StartsOn = startsOn, // Implicit conversion to DateTimeOffset
            ExpiresOn = expiration // Implicit conversion to DateTimeOffset
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read); // Only allow reading

        // Get the SAS token
        var userDelegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(
            startsOn, // Implicit conversion to DateTimeOffset
            expiration); // Implicit conversion to DateTimeOffset

        var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, _blobServiceClient.AccountName).ToString();
        return (sasToken, expiration);
    }

    public async Task<(string sasToken, DateTime expiration)> GenerateSasTokenAsync(string imageUrl)
    {
        var uri = new Uri(imageUrl);
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        
        // Extract blob name from the full URL (remove query parameters first)
        var blobPath = uri.GetLeftPart(UriPartial.Path);
        var blobName = new Uri(blobPath).LocalPath.TrimStart('/').Split('/', 2)[1];
        var blobClient = containerClient.GetBlobClient(blobName);

        return await GenerateSasTokenInternalAsync(blobClient);
    }

    public async Task<(string imageUrl, string sasToken, DateTime expiration)> UploadUserImageAsync(int userId, IFormFile file)
    {
        try
        {
            // Get container reference
            var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            await containerClient.CreateIfNotExistsAsync();

            // Generate unique blob name
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                throw new InvalidOperationException("Only .jpg, .jpeg and .png files are allowed");
            }

            var blobName = $"{_options.UserImagesPath}/{userId}/{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Upload the file
            await using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = file.ContentType
            });

            // Generate a SAS token
            var (sasToken, expiration) = await GenerateSasTokenInternalAsync(blobClient);
            
            return (blobClient.Uri.ToString(), sasToken, expiration);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image at URL {ImageUrl}", imageUrl);
            throw;
        }
    }
} 