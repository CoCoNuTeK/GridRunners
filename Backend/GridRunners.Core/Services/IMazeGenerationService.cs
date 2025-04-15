using GridRunners.Core.Models;
using static GridRunners.Core.Models.MazeGame;

namespace GridRunners.Core.Services;

/// <summary>
/// Interface for maze generation services
/// </summary>
public interface IMazeGenerationService
{
    /// <summary>
    /// Generates a complete maze grid with player positions
    /// </summary>
    /// <param name="width">Width of the maze</param>
    /// <param name="height">Height of the maze</param>
    /// <param name="playerCount">Number of players to place</param>
    /// <returns>A 2D array representing the maze grid with player positions marked as integer value 3</returns>
    Task<CellType[,]?> GenerateCompleteGridAsync(int width, int height, int playerCount);
} 