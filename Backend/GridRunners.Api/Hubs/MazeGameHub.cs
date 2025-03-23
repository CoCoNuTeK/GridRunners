using Microsoft.AspNetCore.SignalR;
using GridRunners.Api.Models;
using GridRunners.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static GridRunners.Api.Models.MazeGame;

namespace GridRunners.Api.Hubs;

public class MazeGameHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MazeGameHub> _logger;

    public MazeGameHub(ApplicationDbContext context, ILogger<MazeGameHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        _logger.LogInformation("User {UserId} connected to hub", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        // Find any active game the player is in
        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Players.Any(p => p.Id == userId));

        if (game != null)
        {
            // Only handle disconnections for active games
            if (game.State == GameState.InGame)
            {
                // Mark player as disconnected
                game.SetPlayerConnection(userId, false);
                
                // Check if all players are disconnected
                if (game.AreAllPlayersDisconnected())
                {
                    _context.Games.Remove(game);
                    _logger.LogInformation("Game {GameId} deleted as all players disconnected", game.Id);
                }
                else
                {
                    // Notify other players about disconnection
                    await Clients.Group($"game_{game.Id}").SendAsync("PlayerDisconnected", new
                    {
                        PlayerId = userId,
                        ConnectedPlayers = game.PlayerConnected
                    });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} disconnected from active game {GameId}", userId, game.Id);
            }
            else
            {
                _logger.LogInformation("User {UserId} disconnected from non-active game {GameId} in state {State}", 
                    userId, game.Id, game.State);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Handles player movement. The frontend should ONLY update player positions when receiving the PlayerMoved event,
    /// not immediately on key press. This ensures state synchronization across all clients.
    /// </summary>
    public async Task MovePlayer(int gameId, int x, int y)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var game = await _context.Games.FindAsync(gameId);

        // Silently fail if game doesn't exist or player isn't in it
        if (game == null || !game.PlayerPositions.ContainsKey(userId))
        {
            return;
        }

        // Try to move - if successful, broadcast to all players
        if (game.MovePlayer(userId, x, y))
        {
            // First send the move to everyone
            await Clients.Group($"game_{gameId}").SendAsync("PlayerMoved", new
            {
                PlayerId = userId,
                Position = new { X = x, Y = y }
            });

            // If game finished, save and notify everyone
            if (game.State == GameState.Finished)
            {
                await _context.SaveChangesAsync();
                await Clients.Group($"game_{gameId}").SendAsync("GameOver", new
                {
                    WinnerId = game.WinnerId,
                    FinalPositions = game.PlayerPositions
                });
            }
        }
    }

    public async Task JoinGame(int gameId)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{gameId}");
        
        var game = await _context.Games.FindAsync(gameId);
        if (game != null && game.State == GameState.InGame)
        {
            game.SetPlayerConnection(userId, true);
            await _context.SaveChangesAsync();
            
            // Notify other players about reconnection
            await Clients.Group($"game_{gameId}").SendAsync("PlayerReconnected", new
            {
                PlayerId = userId
            });
        }
        
        _logger.LogInformation("User {UserId} joined game {GameId} group", userId, gameId);
    }
}