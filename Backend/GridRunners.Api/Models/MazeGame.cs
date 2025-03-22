using System.ComponentModel.DataAnnotations;

namespace GridRunners.Api.Models;

public class MazeGame
{
    /// <summary>
    /// Represents different types of cells in the maze
    /// </summary>
    public enum CellType
    {
        /// <summary>A wall that cannot be passed through</summary>
        Wall,
        /// <summary>A path that can be walked on</summary>
        Path,
        /// <summary>The starting position for players</summary>
        Start,
        /// <summary>The finish line that players need to reach</summary>
        Finish,
        /// <summary>A cell occupied by a player</summary>
        PlayerPosition
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
        Finished,
        /// <summary>Game was cancelled</summary>
        Cancelled
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
    
    // Maze properties
    public int Width { get; private set; }
    public int Height { get; private set; }
    public CellType[,]? Grid { get; private set; }
    
    // Player positions
    public Dictionary<int, (int X, int Y)> PlayerPositions { get; private set; } = new();
    
    // Navigation property for N:N relationship with User
    public virtual ICollection<User> Players { get; init; } = new List<User>();

    // Default constructor for EF Core
    protected MazeGame() 
    {
        CreatedAt = DateTime.UtcNow;
        State = GameState.Lobby;
    }

    // Factory method for creating new games
    public static MazeGame CreateNew(string name, int width = 20, int height = 20)
    {
        return new MazeGame
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            State = GameState.Lobby,
            Width = width,
            Height = height
        };
    }

    // Game state management methods
    public void StartGame()
    {
        if (State == GameState.Lobby)
        {
            StartedAt = DateTime.UtcNow;
            State = GameState.InGame;
            GenerateMaze();
            PlacePlayersAtStart();
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

    public void CancelGame()
    {
        if (State != GameState.Finished)
        {
            EndedAt = DateTime.UtcNow;
            State = GameState.Cancelled;
        }
    }

    // Helper method to check if lobby should be deleted
    public bool ShouldDeleteLobby() => 
        State == GameState.Lobby && Players.Count == 0;

    private void GenerateMaze()
    {
        Grid = new CellType[Width, Height];
        // TODO: Implement maze generation algorithm
        // For now, create a simple maze with walls around the edges
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1)
                {
                    Grid[x, y] = CellType.Wall;
                }
                else
                {
                    Grid[x, y] = CellType.Path;
                }
            }
        }
        
        // Set start and finish
        Grid[1, 1] = CellType.Start;
        Grid[Width - 2, Height - 2] = CellType.Finish;
    }

    private void PlacePlayersAtStart()
    {
        PlayerPositions.Clear();
        foreach (var player in Players)
        {
            PlayerPositions[player.Id] = (1, 1); // Start position
        }
    }

    public bool MovePlayer(int playerId, int newX, int newY)
    {
        if (State != GameState.InGame || Grid == null || !PlayerPositions.ContainsKey(playerId))
            return false;

        // Validate move
        if (newX < 0 || newX >= Width || newY < 0 || newY >= Height)
            return false;

        if (Grid[newX, newY] == CellType.Wall)
            return false;

        // Update position
        PlayerPositions[playerId] = (newX, newY);

        // Check if player reached finish
        if (Grid[newX, newY] == CellType.Finish)
        {
            EndGame(playerId);
            return true;
        }

        return true;
    }
} 