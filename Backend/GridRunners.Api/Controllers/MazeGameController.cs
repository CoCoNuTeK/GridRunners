using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GridRunners.Api.Models;
using GridRunners.Api.Data;
using GridRunners.Api.Dtos;
using GridRunners.Api.Hubs;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace GridRunners.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // Require authentication for all endpoints
[EnableRateLimiting("authenticated")]
public class MazeGameController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MazeGameController> _logger;
    private readonly IHubContext<GameHub> _hubContext;

    public MazeGameController(
        ApplicationDbContext context,
        ILogger<MazeGameController> logger,
        IHubContext<GameHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Creates a new maze game and adds the creator as the first player
    /// </summary>
    /// <param name="request">Game creation details</param>
    /// <returns>The created game details</returns>
    /// <response code="201">Game created successfully</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="500">If there was an internal error</response>
    [HttpPost]
    [ProducesResponseType(typeof(GameResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GameResponse>> CreateGame([FromBody] CreateGameRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return Unauthorized(new { message = "User not found" });

            var game = MazeGame.CreateNew(request.Name);
            game.Players.Add(user);

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} created game {GameId}", userId, game.Id);
            return StatusCode(StatusCodes.Status201Created, game.ToGameResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating game");
            return StatusCode(500, new { message = "An error occurred while creating the game" });
        }
    }

    /// <summary>
    /// Lists all games in lobby state that the user can join
    /// </summary>
    /// <returns>List of available games</returns>
    /// <response code="200">Returns the list of available games</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="500">If there was an internal error</response>
    [HttpGet("available")]
    [ProducesResponseType(typeof(GameListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GameListResponse>> GetAvailableGames()
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var games = await _context.Games
                .Include(g => g.Players)
                .Where(g => g.State == GameState.Lobby && !g.Players.Any(p => p.Id == userId))
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            return new GameListResponse(
                Games: games.Select(g => g.ToGameResponse()).ToList()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available games");
            return StatusCode(500, new { message = "An error occurred while retrieving available games" });
        }
    }

    /// <summary>
    /// Joins an existing game in lobby state
    /// </summary>
    /// <param name="id">Game ID to join</param>
    /// <returns>The joined game details</returns>
    /// <response code="200">Successfully joined the game</response>
    /// <response code="400">If the game cannot be joined</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="404">If the game is not found</response>
    /// <response code="500">If there was an internal error</response>
    [HttpPost("{id}/join")]
    [ProducesResponseType(typeof(GameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GameResponse>> JoinGame(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return Unauthorized(new { message = "User not found" });

            var game = await _context.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
                return NotFound(new { message = "Game not found" });

            if (game.State != GameState.Lobby)
                return BadRequest(new { message = "Game is not in lobby state" });

            if (game.Players.Any(p => p.Id == userId))
                return BadRequest(new { message = "You are already in this game" });

            game.Players.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} joined game {GameId}", userId, id);
            return game.ToGameResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining game {GameId}", id);
            return StatusCode(500, new { message = "An error occurred while joining the game" });
        }
    }

    /// <summary>
    /// Leaves a game lobby. If this was the last player, the lobby is deleted regardless of bots.
    /// </summary>
    /// <param name="id">Game ID to leave</param>
    /// <returns>Success message</returns>
    /// <response code="200">Successfully left the lobby</response>
    /// <response code="400">If the game is not in lobby state</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="404">If the game is not found or user is not in the game</response>
    /// <response code="500">If there was an internal error</response>
    [HttpPost("{id}/leave")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LeaveLobby(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var game = await _context.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
                return NotFound(new { message = "Game not found" });

            if (game.State != GameState.Lobby)
                return BadRequest(new { message = "Can only leave games in lobby state" });

            var player = game.Players.FirstOrDefault(p => p.Id == userId);
            if (player == null)
                return NotFound(new { message = "You are not in this game" });

            game.Players.Remove(player);

            // If this was the last player, delete the lobby (regardless of bots)
            if (game.ShouldDeleteLobby())
            {
                _context.Games.Remove(game);
                _logger.LogInformation("Lobby {GameId} deleted as last player left", id);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} left game {GameId}", userId, id);
            return Ok(new { message = "Successfully left the lobby" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving game {GameId}", id);
            return StatusCode(500, new { message = "An error occurred while leaving the lobby" });
        }
    }

    [HttpPost("{id}/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartGame(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var game = await _context.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
                return NotFound(new { message = "Game not found" });

            if (game.State != GameState.Lobby)
                return BadRequest(new { message = "Game is not in lobby state" });

            if (!game.Players.Any(p => p.Id == userId))
                return BadRequest(new { message = "You are not in this game" });

            game.StartGame();
            await _context.SaveChangesAsync();

            // Notify all players that the game has started
            await _hubContext.Clients.Group($"game_{id}").SendAsync("GameStarted", game.Grid, game.PlayerPositions);

            _logger.LogInformation("Game {GameId} started by user {UserId}", id, userId);
            return Ok(new { message = "Game started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting game {GameId}", id);
            return StatusCode(500, new { message = "An error occurred while starting the game" });
        }
    }
} 