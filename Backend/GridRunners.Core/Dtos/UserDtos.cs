using GridRunners.Core.Models;
using Microsoft.AspNetCore.Http;
using static GridRunners.Core.Models.MazeGame;

namespace GridRunners.Core.Dtos;

public record UpdateDisplayNameRequest(string DisplayName);

public record UpdateProfileImageRequest(IFormFile ImageFile);

public record UserProfileResponse(
    string Username,
    string? DisplayName,
    string? ProfileImageUrl
)
{
    public static UserProfileResponse FromUser(User user) => new(
        Username: user.Username,
        DisplayName: user.DisplayName,
        ProfileImageUrl: user.ProfileImageUrl
    );
}

public record UserProfileWithHistoryResponse(
    string Username,
    string? DisplayName,
    string? ProfileImageUrl,
    List<GameHistoryResponse> MatchHistory
)
{
    public static UserProfileWithHistoryResponse FromUser(User user, List<GameHistoryResponse> history) => new(
        Username: user.Username,
        DisplayName: user.DisplayName,
        ProfileImageUrl: user.ProfileImageUrl,
        MatchHistory: history
    );
}

public record GameHistoryResponse(
    string GameIdentifier,
    string Name,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? EndedAt,
    GameState State,
    bool WonByUser,
    List<string> Participants
); 