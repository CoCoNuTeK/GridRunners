using Microsoft.AspNetCore.SignalR;
using GridRunners.Core.Models;
using GridRunners.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static GridRunners.Core.Models.MazeGame;
using GridRunners.SignalR.Configuration;
using GridRunners.SignalR.Services;
using GridRunners.Core.Services;

namespace GridRunners.SignalR.Hubs;

public class MazeGameHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MazeGameHub> _logger;
    private readonly RuntimeOpenAIConfig? _openAIConfig;
    private readonly IMazeGenerationService? _mazeGenerationService;

    public MazeGameHub(
        ApplicationDbContext context, 
        ILogger<MazeGameHub> logger,
        RuntimeOpenAIConfig? openAIConfig = null,
        OpenAIService? openAIService = null)
    {
        _context = context;
        _logger = logger;
        _openAIConfig = openAIConfig;
        _mazeGenerationService = openAIService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        // Find any ACTIVE game the user is in (Lobby or InGame only, not Finished)
        var game = await _context.Games
            .Include(g => g.Players)
            .Where(g => g.State != GameState.Finished) // Exclude finished games
            .FirstOrDefaultAsync(g => g.Players.Any(p => p.Id == userId));

        if (game != null)
        {
            // Add to game group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{game.Id}");
            _logger.LogInformation("User {UserId} connected to hub and joined active game {GameId} in state {State}", 
                userId, game.Id, game.State);
        }
        else
        {
            _logger.LogInformation("User {UserId} connected to hub (not in any active game)", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        // Find any game the player is in
        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Players.Any(p => p.Id == userId));

        if (game != null)
        {
            // Handle disconnections for active games
            if (game.State == GameState.InGame)
            {
                // Remove player from the game when in-game
                var player = game.Players.FirstOrDefault(p => p.Id == userId);
                if (player != null)
                {
                    game.Players.Remove(player);
                }
                
                // Check if all players are disconnected
                if (!game.Players.Any())
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
            // Handle disconnections from lobby
            else if (game.State == GameState.Lobby)
            {
                var player = game.Players.FirstOrDefault(p => p.Id == userId);
                if (player != null)
                {
                    // Remove player from the game
                    game.Players.Remove(player);

                    // If this was the last player, delete the lobby
                    if (game.Players.Count == 0)
                    {
                        _context.Games.Remove(game);
                        _logger.LogInformation("Lobby {GameId} deleted as last player disconnected", game.Id);
                    }
                    else
                    {
                        // Notify remaining players about the disconnection
                        await Clients.Group($"game_{game.Id}").SendAsync("PlayerLeft", new
                        {
                            PlayerId = userId,
                            PlayerName = player.DisplayName,
                            RemainingPlayers = game.Players.Select(p => new { p.Id, p.DisplayName })
                        });
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User {UserId} disconnected from lobby {GameId}", userId, game.Id);
                }
            }
            // Log disconnection from finished games
            else if (game.State == GameState.Finished)
            {
                _logger.LogInformation("User {UserId} disconnected from finished game {GameId}", userId, game.Id);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Handles player movement. The frontend should ONLY update player positions when receiving the PlayerMoved event,
    /// not immediately on key press. This ensures state synchronization across all clients.
    /// </summary>
    public async Task MovePlayer(
        int gameId, 
        int x, 
        int y, 
        int currentX, 
        int currentY, 
        int[][] grid, 
        Dictionary<string, dynamic> playerPositions)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        _logger.LogInformation("Move request received - Game: {GameId}, User: {UserId}, From: ({CurrentX}, {CurrentY}), To: ({X}, {Y})", 
            gameId, userId, currentX, currentY, x, y);
        
        var game = await _context.Games
            .Include(g => g.Players)  // Include Players collection
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
        {
            _logger.LogWarning("Move request failed - Game: {GameId} not found in database", gameId);
            return;
        }

        try
        {
            // Set the grid on the game object - this is needed as the grid isn't in the DB
            game.SetGridFromClient(grid);
            
            // Set player positions from client
            game.SetPlayerPositionsFromClient(playerPositions);
            
            // Try to move - if successful, broadcast to all players
            if (game.MovePlayer(userId, x, y, currentX, currentY))
            {
                _logger.LogInformation("Move successful - Game: {GameId}, User: {UserId}, New Position: ({X}, {Y})", gameId, userId, x, y);
                
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
                    _logger.LogInformation("Game finished - Game: {GameId}, Winner: {WinnerId}", gameId, game.WinnerId);
                }
            }
            else
            {
                _logger.LogWarning("Move failed validation - Game: {GameId}, User: {UserId}, Attempted Position: ({X}, {Y})", gameId, userId, x, y);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing move - Game: {GameId}, User: {UserId}", gameId, userId);
            throw; // Re-throw to let SignalR handle the error
        }
    }

    public async Task JoinGame(int gameId)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        // Get user info from database
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            await Clients.Caller.SendAsync("GameError", "User not found");
            return;
        }

        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);
            
        if (game == null)
        {
            await Clients.Caller.SendAsync("GameError", "Game not found");
            return;
        }

        // Broadcast the join event
        await Clients.Group($"game_{gameId}").SendAsync("PlayerJoined", new
        {
            PlayerId = userId,
            DisplayName = user.DisplayName,
            ProfileImageUrl = user.ProfileImageUrl
        });
        
        _logger.LogInformation("User {UserId} joined game {GameId}", userId, gameId);
    }

    public async Task StartGame(int gameId)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null || game.State != GameState.Lobby || !game.Players.Any(p => p.Id == userId))
        {
            // Send error back to caller
            await Clients.Caller.SendAsync("GameError", "Someone else already started the game. Hold on tight!");
            return;
        }

        // Immediately set game state to InGame to prevent concurrent starts
        game.SetToInGameState();
        await _context.SaveChangesAsync();
        
        // Initialize the game (generates maze and places players) - passing OpenAI services
        await game.StartGameAsync(_openAIConfig, _mazeGenerationService);
        await _context.SaveChangesAsync();

        if (game.Grid == null)
        {
            await Clients.Caller.SendAsync("GameError", "Failed to initialize game grid");
            return;
        }

        // Convert 2D grid to a nested array structure for serialization
        var serializableGrid = new int[game.Grid.GetLength(0)][];
        for (int y = 0; y < game.Grid.GetLength(0); y++)
        {
            serializableGrid[y] = new int[game.Grid.GetLength(1)];
            for (int x = 0; x < game.Grid.GetLength(1); x++)
            {
                serializableGrid[y][x] = (int)game.Grid[y, x];
            }
        }
        
        // Log grid serialization details
        int walls = 0, free = 0, finish = 0;
        for (int y = 0; y < serializableGrid.Length; y++)
        {
            for (int x = 0; x < serializableGrid[y].Length; x++)
            {
                switch (serializableGrid[y][x])
                {
                    case (int)CellType.Wall: walls++; break;
                    case (int)CellType.Free: free++; break;
                    case (int)CellType.Finish: finish++; break;
                }
            }
        }
        _logger.LogInformation("Grid serialized for client: {Width}x{Height}, Walls: {Walls}, Free: {Free}, Finish: {Finish}",
            serializableGrid[0].Length, serializableGrid.Length, walls, free, finish);

        // Send initial game state to all players in the group
        await Clients.Group($"game_{gameId}").SendAsync("GameStarted", new
        {
            Grid = serializableGrid,
            PlayerPositions = game.PlayerPositions,
            Players = game.Players.Select(p => new { p.Id, p.DisplayName }),
            PlayerColors = game.PlayerColors,
            Width = MazeGame.Width,
            Height = MazeGame.Height
        });

        _logger.LogInformation("Game {GameId} started by user {UserId}", gameId, userId);
    }

    public async Task LeaveLobby(int gameId)
    {
        var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var game = await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null || game.State != GameState.Lobby)
        {
            await Clients.Caller.SendAsync("GameError", "Cannot leave game");
            return;
        }

        var player = game.Players.FirstOrDefault(p => p.Id == userId);
        if (player == null)
        {
            await Clients.Caller.SendAsync("GameError", "You are not in this game");
            return;
        }

        game.Players.Remove(player);

        // If this was the last player, delete the lobby
        if (game.ShouldDeleteLobby())
        {
            _context.Games.Remove(game);
            _logger.LogInformation("Lobby {GameId} deleted as last player left", gameId);
        }

        await _context.SaveChangesAsync();
        
        // Remove from SignalR group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game_{gameId}");
        
        // Notify other players about leaving
        await Clients.Group($"game_{gameId}").SendAsync("PlayerLeft", new
        {
            PlayerId = userId,
            PlayerName = player.DisplayName,
            RemainingPlayers = game.Players.Select(p => new { p.Id, p.DisplayName })
        });

        _logger.LogInformation("User {UserId} left game {GameId}", userId, gameId);
    }
}