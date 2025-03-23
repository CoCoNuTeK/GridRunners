using System.ComponentModel.DataAnnotations;
using GridRunners.Api.Models;
using static GridRunners.Api.Models.MazeGame;

namespace GridRunners.Api.Dtos;

public record CreateGameRequest(
    [Required]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Game name must be between 3 and 50 characters")]
    string Name
);

public record GameResponse(
    int Id,
    string Name,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? EndedAt,
    GameState State,
    int? WinnerId,
    int BotCount,
    ICollection<PlayerInfo> Players
);

public record PlayerInfo(
    int Id,
    string Username,
    string? DisplayName
);

public record GameListResponse(
    ICollection<GameResponse> Games
);

public static class GameDtoExtensions
{
    public static GameResponse ToGameResponse(this MazeGame game)
    {
        return new GameResponse(
            Id: game.Id,
            Name: game.Name,
            CreatedAt: game.CreatedAt,
            StartedAt: game.StartedAt,
            EndedAt: game.EndedAt,
            State: game.State,
            WinnerId: game.WinnerId,
            BotCount: game.BotCount,
            Players: game.Players.Select(p => new PlayerInfo(
                Id: p.Id,
                Username: p.Username,
                DisplayName: p.DisplayName
            )).ToList()
        );
    }
} 