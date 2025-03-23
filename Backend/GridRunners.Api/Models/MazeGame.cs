using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GridRunners.Api.Models;

public class MazeGame
{
    /// <summary>
    /// Maximum total number of players (including bots) allowed in a game
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

    // Number of bots in the game
    public int BotCount { get; private set; }
    
    // Maze properties - not stored in DB
    [NotMapped]
    public CellType[,]? Grid { get; private set; }
    
    // Player positions and colors - not stored in DB
    [NotMapped]
    public Dictionary<int, (int X, int Y)> PlayerPositions { get; private set; } = new();
    
    [NotMapped]
    public Dictionary<int, string> PlayerColors { get; private set; } = new();
    
    // Track player connection status - not stored in DB
    [NotMapped]
    public Dictionary<int, bool> PlayerConnected { get; private set; } = new();
    
    // Navigation property for N:N relationship with User
    public virtual ICollection<User> Players { get; init; } = new List<User>();

    // Helper methods for player/bot management
    public bool CanAddParticipant() => 
        Players.Count + BotCount < MaxPlayers;

    public bool AddBot()
    {
        if (!CanAddParticipant() || State != GameState.Lobby)
            return false;
            
        BotCount++;
        return true;
    }

    public int TotalParticipants => Players.Count + BotCount;

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
    public void StartGame()
    {
        if (State == GameState.Lobby)
        {
            GenerateMaze();
            AssignPlayerColors();
            PlacePlayersAtStart();
            InitPlayerConnections();
            StartedAt = DateTime.UtcNow;
            State = GameState.InGame;
        }
    }

    private void InitPlayerConnections()
    {
        PlayerConnected.Clear();
        foreach (var player in Players)
        {
            PlayerConnected[player.Id] = true;
        }
        // Initialize bot connections
        for (int i = 0; i < BotCount; i++)
        {
            PlayerConnected[1000 + i] = true;
        }
    }

    public void SetPlayerConnection(int playerId, bool isConnected)
    {
        if (PlayerConnected.ContainsKey(playerId))
        {
            PlayerConnected[playerId] = isConnected;
        }
    }

    public bool AreAllPlayersDisconnected()
    {
        return PlayerConnected.All(p => !p.Value);
    }

    private void AssignPlayerColors()
    {
        PlayerColors.Clear();
        var orderedPlayers = Players.OrderBy(p => p.Id).ToList();
        var random = new Random();
        
        // Assign colors to real players first
        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            PlayerColors[orderedPlayers[i].Id] = AvailableColors[i];
        }

        // Assign remaining colors to bots randomly
        var remainingColors = AvailableColors.Skip(orderedPlayers.Count).ToList();
        for (int i = 0; i < BotCount; i++)
        {
            var colorIndex = random.Next(remainingColors.Count);
            PlayerColors[1000 + i] = remainingColors[colorIndex]; // Use 1000+ for bot IDs
            remainingColors.RemoveAt(colorIndex);
        }
    }

    private void GenerateMaze()
    {
        Grid = new CellType[Width, Height];
        var random = new Random();

        // First fill everything with walls
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Grid[x, y] = CellType.Wall;
            }
        }

        // Create paths using a simple random walk
        var stack = new Stack<(int X, int Y)>();
        var start = (X: 1, Y: 1);
        stack.Push(start);
        Grid[start.X, start.Y] = CellType.Free;

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
                    && Grid[newX, newY] == CellType.Wall)
                {
                    neighbors.Add((newX, newY));
                }
            }

            if (neighbors.Count > 0)
            {
                var next = neighbors[random.Next(neighbors.Count)];
                // Create path by setting both the next cell and the cell between current and next
                Grid[next.X, next.Y] = CellType.Free;
                Grid[current.X + (next.X - current.X) / 2, current.Y + (next.Y - current.Y) / 2] = CellType.Free;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }

        // Set finish point in middle area
        var midX = Width / 2 + random.Next(-3, 4);
        var midY = Height / 2 + random.Next(-3, 4);
        Grid[midX, midY] = CellType.Finish;
    }

    private void PlacePlayersAtStart()
    {
        PlayerPositions.Clear();
        var corners = new List<(int X, int Y)>
        {
            (1, 1),                    // Top-left
            (Width - 2, 1),            // Top-right
            (1, Height - 2),           // Bottom-left
            (Width - 2, Height - 2)    // Bottom-right
        };

        // Place real players at opposing corners
        var playerList = Players.OrderBy(p => p.Id).ToList();
        for (int i = 0; i < playerList.Count; i++)
        {
            PlayerPositions[playerList[i].Id] = corners[i];
            Grid[corners[i].X, corners[i].Y] = CellType.Free; // Ensure corner is walkable
        }

        // Place bots at remaining corners
        for (int i = 0; i < BotCount; i++)
        {
            var cornerIndex = playerList.Count + i;
            if (cornerIndex < corners.Count)
            {
                PlayerPositions[1000 + i] = corners[cornerIndex];
                Grid[corners[cornerIndex].X, corners[cornerIndex].Y] = CellType.Free;
            }
        }
    }

    public bool MovePlayer(int playerId, int newX, int newY)
    {
        if (State != GameState.InGame || Grid == null || !PlayerPositions.ContainsKey(playerId))
            return false;

        var currentPos = PlayerPositions[playerId];

        // Don't allow moving to current position
        if (newX == currentPos.X && newY == currentPos.Y)
            return false;
        
        // Validate move is only one square (up, down, left, right)
        var dx = Math.Abs(newX - currentPos.X);
        var dy = Math.Abs(newY - currentPos.Y);
        if (dx + dy != 1) // Ensures only one square movement
            return false;

        // Validate move is within bounds and Grid is not null
        if (newX < 0 || newX >= Width || newY < 0 || newY >= Height || Grid == null)
            return false;

        // Check if target position is a wall
        if (Grid[newX, newY] == CellType.Wall)
            return false;

        // Check if another player is already at the target position
        var targetPos = (X: newX, Y: newY);
        if (PlayerPositions.Values.Any(pos => pos == targetPos))
            return false;

        // Update position
        PlayerPositions[playerId] = (newX, newY);

        // Check if player reached finish and Grid is not null
        if (Grid != null && Grid[newX, newY] == CellType.Finish)
        {
            EndGame(playerId);
            return true;
        }

        return true;
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
} 