using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GridRunners.SignalR.Configuration;
using GridRunners.Core.Models;
using static GridRunners.Core.Models.MazeGame;
using GridRunners.Core.Services;
using Microsoft.Extensions.Logging;

namespace GridRunners.SignalR.Services;

public class OpenAIService : IMazeGenerationService
{
    private readonly RuntimeOpenAIConfig _config;
    private readonly ILogger<OpenAIService> _logger;
    private readonly HttpClient _client;

    public OpenAIService(
        RuntimeOpenAIConfig config,
        ILogger<OpenAIService> logger,
        HttpClient client)
    {
        _config = config;
        _logger = logger;
        _client = client;
        
        // Set a longer timeout (1000 seconds)
        _client.Timeout = TimeSpan.FromSeconds(1000);

        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            if (!string.IsNullOrEmpty(_config.OrganizationId))
            {
                _client.DefaultRequestHeaders.Add("OpenAI-Organization", _config.OrganizationId);
            }
            if (!string.IsNullOrEmpty(_config.ProjectId))
            {
                _client.DefaultRequestHeaders.Add("OpenAI-Project", _config.ProjectId);
            }
        }
    }

    public async Task<CellType[,]?> GenerateCompleteGridAsync(int width, int height, int playerCount)
    {
        try
        {
            _logger.LogInformation(
                "Generating maze with OpenAI API. Dimensions: {Width}x{Height}, Players: {PlayerCount}",
                width, height, playerCount);

            var endpointUrl = _config.EndpointUrl;
            if (string.IsNullOrEmpty(endpointUrl))
            {
                _logger.LogError("OpenAI API endpoint URL is not configured");
                return null;
            }

            var systemInstructions =
                $"You are a maze generating assistant. Generate a maze with dimensions {width}x{height} where:\n" +
                "- 0 represents walls\n" +
                "- 1 represents free cells (paths)\n" +
                "- 2 represents the finish point (exactly one)\n" +
                $"- 3 represents player positions (exactly {playerCount} players)\n\n" +
                "The maze MUST follow these requirements:\n" +
                "1. All outer edges must be walls (0)\n" +
                $"2. There must be exactly one finish point (2) placed exactly in the center area: between row {height/2-2} and {height/2+2}, and between column {width/2-2} and {width/2+2}\n" +
                $"3. There must be exactly {playerCount} player positions (3) placed near different corners of the maze\n" +
                "4. The maze must have valid paths from all player starting positions to the finish\n" +
                "5. Player positions should be far from each other, ideally in different corners\n" +
                "6. Create a proper maze structure with distinct corridors and walls, not just scattered wall cells\n" + 
                "7. Ensure all players have balanced paths to the finish with similar difficulty levels\n" +
                "8. Use a consistent maze generation pattern like recursive backtracking or cellular automata\n" + 
                "Return a JSON object with a 'grid' property containing a 2D array representing the complete grid.\n";

            var requestObject = new
            {
                model = _config.ModelId,
                input = $"Generate a {width}x{height} maze with {playerCount} players. You are obliged and must make sure that there are exactly {playerCount} player positions (3) placed near different corners of the maze",
                instructions = systemInstructions,
                parallel_tool_calls = false,
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "maze_grid",
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                grid = new
                                {
                                    type = "array",
                                    description = "A 2D grid representing the maze where 0=wall, 1=path, 2=finish, 3=player starting position",
                                    items = new
                                    {
                                        type = "array",
                                        description = "A row in the maze grid",
                                        items = new
                                        {
                                            type = "integer",
                                            description = "Cell type: 0=wall, 1=path, 2=finish, 3=player starting position",
                                            @enum = new[] {0, 1, 2, 3}
                                        }
                                    }
                                }
                            },
                            required = new[] {"grid"},
                            additionalProperties = false
                        },
                        strict = true
                    }
                }
            };

            var requestContent = JsonSerializer.Serialize(requestObject);
            var content = new StringContent(requestContent, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(endpointUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "OpenAI API error: {StatusCode}, {Error}",
                    response.StatusCode, errorMsg);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received response from OpenAI API");

            try
            {
                using var document = JsonDocument.Parse(jsonResponse);
                if (!document.RootElement.TryGetProperty("output", out var outputElement))
                {
                    _logger.LogError("Response doesn't contain 'output' property");
                    return null;
                }

                foreach (var messageElement in outputElement.EnumerateArray())
                {
                    if (messageElement.GetProperty("type").GetString() == "message")
                    {
                        var contentArray = messageElement.GetProperty("content");
                        foreach (var contentItem in contentArray.EnumerateArray())
                        {
                            var contentType = contentItem.GetProperty("type").GetString();
                            if (contentType == "refusal")
                            {
                                if (contentItem.TryGetProperty("refusal", out var refusalElement))
                                {
                                    _logger.LogWarning(
                                        "OpenAI refused to generate maze: {Reason}",
                                        refusalElement.GetString());
                                }
                                return null;
                            }
                            if (contentType == "output_text")
                            {
                                var outputText = contentItem.GetProperty("text").GetString();
                                if (!string.IsNullOrEmpty(outputText))
                                {
                                    try
                                    {
                                        // Parse the response as an object with a grid property
                                        using var jsonDoc = JsonDocument.Parse(outputText);
                                        var rootElement = jsonDoc.RootElement;
                                        
                                        // Check if we have a grid property
                                        if (rootElement.TryGetProperty("grid", out var gridElement))
                                        {
                                            var gridWithPlayers = JsonSerializer.Deserialize<int[][]>(gridElement.GetRawText());
                                            if (gridWithPlayers != null)
                                            {
                                                var result = new CellType[height, width];
                                                for (int y = 0; y < Math.Min(height, gridWithPlayers.Length); y++)
                                                {
                                                    var row = gridWithPlayers[y];
                                                    for (int x = 0; x < Math.Min(width, row.Length); x++)
                                                    {
                                                        result[y, x] = (CellType)row[x];
                                                    }
                                                }
                                                if (VerifyGrid(result, width, height, playerCount))
                                                {
                                                    _logger.LogInformation(
                                                        "Successfully generated maze with player positions");
                                                    return result;
                                                }
                                                else
                                                {
                                                    _logger.LogWarning("Generated grid doesn't meet requirements");
                                                    return null;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Response doesn't contain 'grid' property, falling back to direct array parsing");
                                            // Try direct array parsing as fallback
                                            var gridWithPlayers = JsonSerializer.Deserialize<int[][]>(outputText);
                                            if (gridWithPlayers != null)
                                            {
                                                var result = new CellType[height, width];
                                                for (int y = 0; y < Math.Min(height, gridWithPlayers.Length); y++)
                                                {
                                                    var row = gridWithPlayers[y];
                                                    for (int x = 0; x < Math.Min(width, row.Length); x++)
                                                    {
                                                        result[y, x] = (CellType)row[x];
                                                    }
                                                }
                                                if (VerifyGrid(result, width, height, playerCount))
                                                {
                                                    _logger.LogInformation(
                                                        "Successfully extracted maze with player positions from text");
                                                    return result;
                                                }
                                            }
                                        }
                                    }
                                    catch (JsonException ex)
                                    {
                                        _logger.LogError(ex, "Failed to parse maze grid from OpenAI response");
                                        var jsonStartIndex = outputText.IndexOf('[');
                                        var jsonEndIndex = outputText.LastIndexOf(']');
                                        if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
                                        {
                                            try
                                            {
                                                var jsonPart = outputText.Substring(
                                                    jsonStartIndex,
                                                    jsonEndIndex - jsonStartIndex + 1);
                                                
                                                // Try to parse as object with grid property first
                                                try
                                                {
                                                    using var jsonDoc = JsonDocument.Parse(jsonPart);
                                                    var rootElement = jsonDoc.RootElement;
                                                    
                                                    if (rootElement.ValueKind == JsonValueKind.Object && 
                                                        rootElement.TryGetProperty("grid", out var gridElement))
                                                    {
                                                        // We have a grid property
                                                        var extractedGrid = JsonSerializer.Deserialize<int[][]>(gridElement.GetRawText());
                                                        if (extractedGrid != null)
                                                        {
                                                            var result = new CellType[height, width];
                                                            for (int y = 0; y < Math.Min(height, extractedGrid.Length); y++)
                                                            {
                                                                var row = extractedGrid[y];
                                                                for (int x = 0; x < Math.Min(width, row.Length); x++)
                                                                {
                                                                    result[y, x] = (CellType)row[x];
                                                                }
                                                            }
                                                            if (VerifyGrid(result, width, height, playerCount))
                                                            {
                                                                _logger.LogInformation(
                                                                    "Successfully extracted maze with player positions from text");
                                                                return result;
                                                            }
                                                        }
                                                    }
                                                    else if (rootElement.ValueKind == JsonValueKind.Array)
                                                    {
                                                        // Direct array format
                                                        var extractedGrid = JsonSerializer.Deserialize<int[][]>(jsonPart);
                                                        if (extractedGrid != null)
                                                        {
                                                            var result = new CellType[height, width];
                                                            for (int y = 0; y < Math.Min(height, extractedGrid.Length); y++)
                                                            {
                                                                var row = extractedGrid[y];
                                                                for (int x = 0; x < Math.Min(width, row.Length); x++)
                                                                {
                                                                    result[y, x] = (CellType)row[x];
                                                                }
                                                            }
                                                            if (VerifyGrid(result, width, height, playerCount))
                                                            {
                                                                _logger.LogInformation(
                                                                    "Successfully extracted maze grid directly from text");
                                                                return result;
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (JsonException innerEx)
                                                {
                                                    _logger.LogWarning(innerEx, "Failed parsing as object, trying as direct array");
                                                    // Fall back to direct array parsing
                                                    var extractedGrid = JsonSerializer.Deserialize<int[][]>(jsonPart);
                                                    if (extractedGrid != null)
                                                    {
                                                        var result = new CellType[height, width];
                                                        for (int y = 0; y < Math.Min(height, extractedGrid.Length); y++)
                                                        {
                                                            var row = extractedGrid[y];
                                                            for (int x = 0; x < Math.Min(width, row.Length); x++)
                                                            {
                                                                result[y, x] = (CellType)row[x];
                                                            }
                                                        }
                                                        if (VerifyGrid(result, width, height, playerCount))
                                                        {
                                                            _logger.LogInformation(
                                                                "Successfully extracted maze with player positions from text");
                                                            return result;
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception exInner)
                                            {
                                                _logger.LogError(exInner, "Failed to extract maze grid from text");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                _logger.LogWarning("Failed to extract maze grid from OpenAI response");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OpenAI response: {Message}", ex.Message);
                _logger.LogDebug("Raw response: {Response}", jsonResponse);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maze with OpenAI API");
            return null;
        }
    }

    private bool VerifyGrid(CellType[,] grid, int width, int height, int expectedPlayerCount)
    {
        int wallCount = 0;
        int freeCount = 0;
        int finishCount = 0;
        int playerCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                switch (grid[y, x])
                {
                    case CellType.Wall: wallCount++; break;
                    case CellType.Free: freeCount++; break;
                    case CellType.Finish: finishCount++; break;
                    case (CellType)3: playerCount++; break;
                }
                if ((x == 0 || x == width - 1 || y == 0 || y == height - 1) &&
                    grid[y, x] != CellType.Wall)
                {
                    _logger.LogWarning("Grid missing wall at edge position ({X},{Y})", x, y);
                    return false;
                }
            }
        }

        if (finishCount != 1)
        {
            _logger.LogWarning("Grid has {FinishCount} finish points instead of 1", finishCount);
            return false;
        }
        if (playerCount != expectedPlayerCount)
        {
            _logger.LogWarning(
                "Grid has {PlayerCount} player positions instead of {ExpectedCount}",
                playerCount, expectedPlayerCount);
            return false;
        }
        _logger.LogInformation(
            "Grid verification passed: {Walls} walls, {Free} free cells, {Finish} finish, {Players} players",
            wallCount, freeCount, finishCount, playerCount);
        return true;
    }
}
