using GridRunners.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using GridRunners.Core.Models;
using GridRunners.Api.Services;
using GridRunners.Core.Data;
using GridRunners.Core.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GridRunners.Api.Controllers;

[ApiController]
[ApiVersion(ApiVersions.Current)]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        AuthService authService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns access and refresh tokens
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication tokens and username</returns>
    /// <response code="200">Returns the authentication tokens</response>
    /// <response code="401">If the password is incorrect</response>
    /// <response code="429">Too many requests - rate limit exceeded</response>
    /// <response code="500">If there was an internal error</response>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Username and password are required" });
            }

            // Find user by username
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            // If user doesn't exist, create a new one
            if (user == null)
            {
                try
                {
                    user = GridRunners.Core.Models.User.CreateNew(request.Username, request.Password);
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Created new user: {Username}", request.Username);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }
            }
            // If user exists, verify password
            else if (!user.VerifyPassword(request.Password))
            {
                return Unauthorized(new { message = "Invalid password" });
            }

            // Generate tokens
            var accessToken = _authService.GenerateAccessToken(user);
            var refreshToken = _authService.GenerateRefreshToken();
            var refreshTokenExpiration = _authService.GetRefreshTokenExpiration();

            // Save refresh token
            user.SetRefreshToken(refreshToken, refreshTokenExpiration);
            await _context.SaveChangesAsync();

            return new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                AccessTokenExpiresAt: DateTime.UtcNow.AddMinutes(30),
                Username: user.Username
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", request.Username);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Refreshes the access token using a valid refresh token
    /// </summary>
    /// <param name="request">The refresh token</param>
    /// <returns>New access and refresh tokens</returns>
    /// <response code="200">Returns new authentication tokens</response>
    /// <response code="401">If the refresh token is invalid or expired</response>
    /// <response code="500">If there was an internal error</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // Find user by refresh token
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

            if (user == null || !user.IsRefreshTokenValid(request.RefreshToken))
            {
                return Unauthorized(new { message = "Invalid refresh token" });
            }

            // Generate new tokens
            var accessToken = _authService.GenerateAccessToken(user);
            var refreshToken = _authService.GenerateRefreshToken();
            var refreshTokenExpiration = _authService.GetRefreshTokenExpiration();

            // Update refresh token
            user.SetRefreshToken(refreshToken, refreshTokenExpiration);
            await _context.SaveChangesAsync();

            return new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                AccessTokenExpiresAt: DateTime.UtcNow.AddMinutes(30),
                Username: user.Username
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { message = "An error occurred while refreshing the token" });
        }
    }

    /// <summary>
    /// Logs out a user by invalidating their refresh token
    /// </summary>
    /// <returns>Success message</returns>
    /// <response code="200">Successfully logged out</response>
    /// <response code="401">If not authenticated</response>
    /// <response code="500">If there was an internal error</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.ClearRefreshToken();
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Successfully logged out" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }
} 