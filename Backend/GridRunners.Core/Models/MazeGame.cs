using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;
using Microsoft.CSharp.RuntimeBinder;
using System.Text.Json;
using GridRunners.Core.Services;

namespace GridRunners.Core.Models;

public class MazeGame
{
    /// <summary>
    /// Maximum total number of players allowed in a game
    /// </summary>
    public const int MaxPlayers = 4;

    /// <summary>
    /// Standard maze dimensions for all games
    /// </summary>
    public const int Width = 20;
    public const int Height = 20;

    /// <summary>
    /// Available player colors in order of assignment
    /// </summary>
    private static readonly string[] AvailableColors = { "red", "blue", "green", "purple" };

    /// <summary>
    /// Represents different types of cells in the maze
    /// </summary>
    public enum CellType
    {
        /// <summary>A wall that cannot be passed through</summary>
        Wall,
        /// <summary>A path that can be walked on</summary>
        Free,
        /// <summary>The finish line that players need to reach</summary>
        Finish
    }

    /// <summary>
    /// Represents the state of a maze game
    /// </summary>
    public enum GameState
    {
        /// <summary>Game is in lobby, waiting for players</summary>
        Lobby,
        /// <summary>Game is currently being played</summary>
        InGame,
        /// <summary>Game has finished with a winner</summary>
        Finished
    }

    [Key]
    public int Id { get; init; }
    
    [Required]
    [StringLength(50)]
    public string Name { get; init; } = null!;
    
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public GameState State { get; private set; }
    
    // Winner reference - just store the ID as we can query User table when needed
    public int? WinnerId { get; private set; }
    
    // Maze properties - not stored in DB
    [NotMapped]
    public CellType[,]? Grid { get; private set; }
    
    // Player positions and colors - not stored in DB
    [NotMapped]
    public Dictionary<int, dynamic> PlayerPositions { get; private set; } = new();
    
    [NotMapped]
    public Dictionary<int, string> PlayerColors { get; private set; } = new();
    
    // Track player connection status - not stored in DB
    [NotMapped]
    public Dictionary<int, bool> PlayerConnected { get; private set; } = new();
    
    // Navigation property for N:N relationship with User
    public virtual ICollection<User> Players { get; init; } = new List<User>();

    // Helper methods for player management
    public bool CanAddParticipant() => 
        Players.Count < MaxPlayers;

    public int TotalParticipants => Players.Count;

    // Default constructor for EF Core
    protected MazeGame() 
    {
        CreatedAt = DateTime.UtcNow;
        State = GameState.Lobby;
    }

    // Factory method for creating new games
    public static MazeGame CreateNew(string name)
    {
        return new MazeGame
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            State = GameState.Lobby
        };
    }

    // Game state management methods
    public async Task StartGameAsync(object? openAIConfig = null, IMazeGenerationService? mazeGenerationService = null)
    {
        if (Players.Count >= 2)
        {
            Console.WriteLine("Starting new game - initializing state");
            
            // Determine whether to use OpenAI for maze generation
            bool useOpenAI = openAIConfig != null && GetEnabledProperty(openAIConfig) && mazeGenerationService != null;
            
            if (useOpenAI)
            {
                // Use async method with await
                bool success = await GenerateMazeWithAIAsync(mazeGenerationService!, Players.Count);
                
                if (success) // If AI maze generation succeeded
                {
                    Console.WriteLine("Successfully generated maze using OpenAI service");
                }
                else // Fallback to local generation
                {
                    Console.WriteLine("OpenAI service failed to generate maze, falling back to local generation");
                    GenerateMaze();
                    PlacePlayersAtStart();
                }
            }
            else
            {
                // Use local generation
                Console.WriteLine("Using local maze generation");
                GenerateMaze();
                PlacePlayersAtStart();
            }
            
            AssignPlayerColors();
            InitPlayerConnections();
            StartedAt = DateTime.UtcNow;
            State = GameState.InGame;
            Console.WriteLine($"Game initialized with {Players.Count} players");
        }
    }
    
    // Method to set game state to InGame immediately to prevent multiple concurrent game starts
    public void SetToInGameState()
    {
        if (State == GameState.Lobby)
        {
            State = GameState.InGame;
            StartedAt = DateTime.UtcNow;
            Console.WriteLine($"Game state changed to InGame to prevent concurrent starts");
        }
    }
    
    // Keep the old method for backward compatibility
    public void StartGame(object? openAIConfig = null, IMazeGenerationService? mazeGenerationService = null)
    {
        // Just call the async version and wait for it
        var task = StartGameAsync(openAIConfig, mazeGenerationService);
        task.Wait();
    }
    
    // Helper method to get the Enabled property from the config object regardless of its type
    private bool GetEnabledProperty(object config)
    {
        try
        {
            // Try to get the Enabled property using reflection
            var property = config.GetType().GetProperty("Enabled");
            if (property != null)
            {
                return (bool)property.GetValue(config, null)!;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> GenerateMazeWithAIAsync(IMazeGenerationService mazeGenerationService, int playerCount)
    {
        try
        {
            // Generate maze and player positions
            var gridWithPlayers = await mazeGenerationService.GenerateCompleteGridAsync(Width, Height, playerCount);
            
            if (gridWithPlayers == null)
            {
                Console.WriteLine("OpenAI service failed to generate a valid maze");
                return false;
            }
            
            // Set the grid
            Grid = gridWithPlayers;
            
            // Extract player positions from grid
            ExtractPlayerPositionsFromGrid();
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error using OpenAI service: {ex.Message}");
            return false;
        }
    }
    
    private void ExtractPlayerPositionsFromGrid()
    {
        if (Grid == null) return;
        
        // Clear existing positions
        PlayerPositions.Clear();
        
        // Get ordered list of players
        var playerList = Players.OrderBy(p => p.Id).ToList();
        var playerPositions = new List<(int X, int Y)>();
        
        // Find all cells marked as player positions (value 3)
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if ((int)Grid[y, x] == 3) // Player position
                {
                    playerPositions.Add((x, y));
                    // Reset to Free in the grid (players shouldn't be part of the grid)
                    Grid[y, x] = CellType.Free;
                }
            }
        }
        
        // Assign positions to players
        for (int i = 0; i < Math.Min(playerList.Count, playerPositions.Count); i++)
        {
            var player = playerList[i];
            var (x, y) = playerPositions[i];
            PlayerPositions[player.Id] = new { X = x, Y = y };
        }
        
        Console.WriteLine($"Extracted {playerPositions.Count} player positions from grid");
    }

    private void InitPlayerConnections()
    {
        PlayerConnected.Clear();
        foreach (var player in Players)
        {
            PlayerConnected[player.Id] = true;
        }
    }

    private void AssignPlayerColors()
    {
        PlayerColors.Clear();
        var orderedPlayers = Players.OrderBy(p => p.Id).ToList();
        
        // Assign colors to players
        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            PlayerColors[orderedPlayers[i].Id] = AvailableColors[i];
        }
    }

    private void GenerateMaze()
    {
        Console.WriteLine("Starting maze generation");
        Grid = new CellType[Width, Height];
        var random = new Random();

        // First fill everything with walls
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Grid[y, x] = CellType.Wall;
            }
        }
        Console.WriteLine("Grid initialized with walls");

        // Create paths using a simple random walk
        var stack = new Stack<(int X, int Y)>();
        var start = (X: 1, Y: 1);
        stack.Push(start);
        Grid[start.Y, start.X] = CellType.Free;

        int pathCells = 1; // Count starting with the first free cell
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var neighbors = new List<(int X, int Y)>();

            // Check all 4 directions
            var directions = new[] { (0, 2), (2, 0), (0, -2), (-2, 0) };
            foreach (var (dx, dy) in directions)
            {
                var newX = current.X + dx;
                var newY = current.Y + dy;
                
                if (newX > 0 && newX < Width - 1 && newY > 0 && newY < Height - 1 
                    && Grid[newY, newX] == CellType.Wall)
                {
                    neighbors.Add((newX, newY));
                }
            }

            if (neighbors.Count > 0)
            {
                var next = neighbors[random.Next(neighbors.Count)];
                // Create path by setting both the next cell and the cell between current and next
                Grid[next.Y, next.X] = CellType.Free;
                Grid[current.Y + (next.Y - current.Y) / 2, current.X + (next.X - current.X) / 2] = CellType.Free;
                stack.Push(next);
                pathCells += 2; // Two more free cells added
            }
            else
            {
                stack.Pop();
            }
        }

        // Set finish point in middle area
        var midX = Width / 2 + random.Next(-3, 4);
        var midY = Height / 2 + random.Next(-3, 4);
        Grid[midY, midX] = CellType.Finish;
        
        Console.WriteLine($"Maze generation complete: {pathCells} path cells + 1 finish cell, finish at ({midX}, {midY})");
        
        // Log wall/free cell counts as verification
        int wallCount = 0;
        int freeCount = 0;
        int finishCount = 0;
        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                switch (Grid[y, x])
                {
                    case CellType.Wall: wallCount++; break;
                    case CellType.Free: freeCount++; break;
                    case CellType.Finish: finishCount++; break;
                }
            }
        }
        
        Console.WriteLine($"Grid cells: {Width}x{Height} = {Width*Height} total, {wallCount} walls, {freeCount} free, {finishCount} finish");
    }

    private void PlacePlayersAtStart()
    {
        if (Grid == null) return;

        // Define the four corners of the maze (excluding walls)
        var corners = new[]
        {
            (1, 1),                    // Top-left
            (Width - 2, 1),            // Top-right
            (1, Height - 2),           // Bottom-left
            (Width - 2, Height - 2)    // Bottom-right
        };

        // Place players at corners
        var playerList = Players.OrderBy(p => p.Id).ToList();
        for (int i = 0; i < playerList.Count && i < corners.Length; i++)
        {
            var player = playerList[i];
            if (player != null)
            {
                var (x, y) = corners[i];
                PlayerPositions[player.Id] = new { X = x, Y = y };
                Grid[y, x] = CellType.Free; // Ensure corner is walkable
            }
        }
    }

    public bool MovePlayer(int playerId, int newX, int newY, int currentX, int currentY)
    {
        if (State != GameState.InGame || Grid == null)
        {
            Console.WriteLine($"Move validation failed - Game state: {State}, Grid null: {Grid == null}");
            return false;
        }

        // Don't allow moving to current position
        if (newX == currentX && newY == currentY)
        {
            Console.WriteLine("Move rejected - Attempting to move to current position");
            return false;
        }
        
        // Validate move is only one square (up, down, left, right)
        var dx = Math.Abs(newX - currentX);
        var dy = Math.Abs(newY - currentY);
        if (dx + dy != 1) // Ensures only one square movement
        {
            Console.WriteLine($"Move rejected - Invalid distance: dx={dx}, dy={dy}");
            return false;
        }

        // Validate move is within bounds
        if (newX < 0 || newX >= Width || newY < 0 || newY >= Height)
        {
            Console.WriteLine($"Move rejected - Out of bounds: x={newX}, y={newY}, width={Width}, height={Height}");
            return false;
        }

        // Check if target position is a wall
        var cellType = Grid[newY, newX];
        if (cellType == CellType.Wall)
        {
            Console.WriteLine($"Move rejected - Target position is a wall at ({newX}, {newY})");
            return false;
        }

        // Check if another player is already at the target position
        if (PlayerPositions.Values.Any(pos => (int)pos.X == newX && (int)pos.Y == newY))
        {
            Console.WriteLine($"Move rejected - Position ({newX}, {newY}) is occupied by another player");
            return false;
        }

        try
        {
            // Update position
            PlayerPositions[playerId] = new { X = newX, Y = newY };
            Console.WriteLine($"Move successful - Player {playerId} moved to ({newX}, {newY})");

            // Check if player reached finish
            if (Grid != null && Grid[newY, newX] == CellType.Finish)
            {
                Console.WriteLine($"Player {playerId} reached the finish line!");
                EndGame(playerId);
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating player position: {ex.Message}");
            return false;
        }
    }

    public void EndGame(int winnerId)
    {
        if (State == GameState.InGame)
        {
            EndedAt = DateTime.UtcNow;
            State = GameState.Finished;
            WinnerId = winnerId;
        }
    }

    // Helper method to check if lobby should be deleted
    public bool ShouldDeleteLobby() => 
        State == GameState.Lobby && Players.Count == 0;

    // Helper methods to set state from client
    public void SetGridFromClient(int[][] clientGrid)
    {
        if (clientGrid == null || clientGrid.Length == 0) return;
        
        // Initialize the Grid if it's null
        if (Grid == null)
        {
            Grid = new CellType[Height, Width];
        }
        
        // Log grid dimensions
        Console.WriteLine($"Setting grid from client data: Length={clientGrid.Length}, NestedLength={clientGrid[0].Length}");
        
        // Count cell types before update
        int wallsBefore = 0, freeBefore = 0, finishBefore = 0;
        if (Grid != null)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    switch (Grid[y, x])
                    {
                        case CellType.Wall: wallsBefore++; break;
                        case CellType.Free: freeBefore++; break;
                        case CellType.Finish: finishBefore++; break;
                    }
                }
            }
        }
        
        // Convert the jagged array from client to 2D array for server
        for (int y = 0; y < clientGrid.Length && y < Height; y++)
        {
            if (clientGrid[y] == null) continue; // Skip null rows
            for (int x = 0; x < clientGrid[y].Length && x < Width; x++)
            {
                Grid![y, x] = (CellType)clientGrid[y][x];
            }
        }
        
        // Count cell types after update
        int wallsAfter = 0, freeAfter = 0, finishAfter = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                switch (Grid![y, x])
                {
                    case CellType.Wall: wallsAfter++; break;
                    case CellType.Free: freeAfter++; break;
                    case CellType.Finish: finishAfter++; break;
                }
            }
        }
        
        Console.WriteLine($"Grid updated from client - Before: {wallsBefore} walls, {freeBefore} free, {finishBefore} finish");
        Console.WriteLine($"Grid updated from client - After: {wallsAfter} walls, {freeAfter} free, {finishAfter} finish");
        
        // Check for significant changes in cell counts as an anomaly indicator
        if (Math.Abs(wallsBefore - wallsAfter) > 10 || Math.Abs(freeBefore - freeAfter) > 10)
        {
            Console.WriteLine("WARNING: Significant change in grid cell counts detected - potential grid mismatch");
        }
    }
    
    public void SetPlayerPositionsFromClient(Dictionary<string, dynamic> clientPositions)
    {
        if (clientPositions == null) return;
        
        // Clear the existing positions and repopulate
        PlayerPositions.Clear();
        
        foreach (var kvp in clientPositions)
        {
            if (int.TryParse(kvp.Key, out int playerId))
            {
                try 
                {
                    // Handle different types of position data
                    var position = kvp.Value;
                    if (position == null) continue;
                    
                    // Handle JsonElement case (most common when coming from JSON)
                    if (position is System.Text.Json.JsonElement jsonElement)
                    {
                        if (jsonElement.TryGetProperty("x", out var xElement) && 
                            jsonElement.TryGetProperty("y", out var yElement))
                        {
                            if (xElement.TryGetInt32(out int x) && yElement.TryGetInt32(out int y))
                            {
                                PlayerPositions[playerId] = new { X = x, Y = y };
                            }
                        }
                    }
                    // Handle dynamic object case
                    else
                    {
                        // Try to access x and y as dynamic properties
                        try
                        {
                            int x = Convert.ToInt32(position.x);
                            int y = Convert.ToInt32(position.y);
                            PlayerPositions[playerId] = new { X = x, Y = y };
                        }
                        catch
                        {
                            Console.WriteLine($"Could not extract x,y from position object for player {playerId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing position for player {playerId}: {ex.Message}");
                }
            }
        }
        
        Console.WriteLine($"Player positions have been set from client data: {PlayerPositions.Count} players");
    }
} 
