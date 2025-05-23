using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GridRunners.Core.Models;
using GridRunners.Core.Data;
using GridRunners.Core.Dtos;
using GridRunners.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using GridRunners.Api.Configuration;

namespace GridRunners.Api.Controllers;

[ApiController]
[ApiVersion(ApiVersions.Current)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]  // Require authentication for all endpoints
[EnableRateLimiting("authenticated")]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly BlobStorageService _blobStorage;
    private readonly ILogger<UserController> _logger;

    public UserController(
        ApplicationDbContext context,
        BlobStorageService blobStorage,
        ILogger<UserController> logger)
    {
        _context = context;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    /// <summary>
    /// Updates the user's display name
    /// </summary>
    /// <param name="request">New display name</param>
    /// <returns>Updated user profile</returns>
    /// <response code="200">Display name updated successfully</response>
    /// <response code="400">If the display name is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="500">If there was an internal server error</response>
    [HttpPut("display-name")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserProfileResponse>> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { message = "Display name cannot be empty" });

        if (request.DisplayName.Length > 50)
            return BadRequest(new { message = "Display name cannot be longer than 50 characters" });

        user!.UpdateDisplayName(request.DisplayName);
        await _context.SaveChangesAsync();

        return UserProfileResponse.FromUser(user);
    }

    /// <summary>
    /// Updates the user's profile image
    /// </summary>
    /// <param name="file">New profile image file (jpg or png)</param>
    /// <returns>Updated user profile</returns>
    /// <response code="200">Profile image updated successfully</response>
    /// <response code="400">If the file is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="415">If the file type is not supported</response>
    /// <response code="500">If there was an internal server error</response>
    [HttpPost("profile-image")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]  // 10 MB limit
    [RequestSizeLimit(10 * 1024 * 1024)]  // 10 MB limit
    public async Task<ActionResult<UserProfileResponse>> UpdateProfileImage([FromForm] UpdateProfileImageRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);

        if (request.ImageFile == null || request.ImageFile.Length == 0)
            return BadRequest(new { message = "No file was uploaded" });

        if (request.ImageFile.Length > 10 * 1024 * 1024)  // 10 MB
            return BadRequest(new { message = "File size exceeds 10 MB limit" });

        // Validate file extension before attempting upload
        var extension = Path.GetExtension(request.ImageFile.FileName).ToLowerInvariant();
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, 
                new { message = "Only .jpg, .jpeg and .png files are allowed" });
        }

        try
        {
            // Delete old image if exists
            if (!string.IsNullOrEmpty(user!.ProfileImageUrl))
            {
                await _blobStorage.DeleteUserImageAsync(user.ProfileImageUrl);
            }

            // Upload new image
            var imageUrl = await _blobStorage.UploadUserImageAsync(userId, request.ImageFile);
            
            // Update user profile with new image URL
            user.UpdateProfileImage(imageUrl);
            await _context.SaveChangesAsync();

            return UserProfileResponse.FromUser(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing profile image for user {UserId}", userId);
            
            if (ex is InvalidOperationException)
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { message = ex.Message });
                
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update profile image" });
        }
    }

    /// <summary>
    /// Gets the user's profile and match history
    /// </summary>
    /// <returns>User profile and list of games the user has participated in</returns>
    /// <response code="200">Returns the profile and match history</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="500">If there was an internal server error</response>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileWithHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserProfileWithHistoryResponse>> GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var games = await _context.Games
            .Include(g => g.Players)
            .Where(g => g.Players.Any(p => p.Id == userId))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        var matchHistory = games.Select(game => new GameHistoryResponse(
            GameIdentifier: $"{game.Name}-{game.CreatedAt:yyyyMMddHHmmss}",
            Name: game.Name,
            CreatedAt: game.CreatedAt,
            StartedAt: game.StartedAt,
            EndedAt: game.EndedAt,
            State: game.State,
            WonByUser: game.WinnerId == userId,
            Participants: game.Players.Select(p => p.DisplayName ?? p.Username).ToList()
        )).ToList();

        return UserProfileWithHistoryResponse.FromUser(user, matchHistory);
    }
} 