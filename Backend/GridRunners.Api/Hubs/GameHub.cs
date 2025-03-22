using Microsoft.AspNetCore.SignalR;
using GridRunners.Api.Models;
using GridRunners.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GridRunners.Api.Hubs;

public class GameHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GameHub> _logger;

    public GameHub(ApplicationDbContext context, ILogger<GameHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var gameId = await GetUserGameId(userId);
        
        if (gameId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{gameId}");
            _logger.LogInformation("User {UserId} connected to game {GameId}", userId, gameId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var gameId = await GetUserGameId(userId);
        
        if (gameId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game_{gameId}");
            _logger.LogInformation("User {UserId} disconnected from game {GameId}", userId, gameId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    public async Task MovePlayer(int x, int y)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var gameId = await GetUserGameId(userId);
        
        if (!gameId.HasValue)
        {
            throw new HubException("User is not in a game");
        }

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null || game.State != GameState.InGame)
        {
            throw new HubException("Game not found or not in progress");
        }

        if (game.MovePlayer(userId, x, y))
        {
            // Notify all players in the game about the move
            await Clients.Group($"game_{gameId}").SendAsync("PlayerMoved", userId, x, y);

            // If the game ended (player reached finish), notify everyone
            if (game.State == GameState.Finished)
            {
                await Clients.Group($"game_{gameId}").SendAsync("GameOver", userId);
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task<int?> GetUserGameId(int userId)
    {
        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Players.Any(p => p.Id == userId));
        
        return game?.Id;
    }
} 