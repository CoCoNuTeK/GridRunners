using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Claims;

namespace GridRunners.Api.Services;

public static class RateLimitingService
{
    public static void ConfigureRateLimiting(RateLimiterOptions options)
    {
        // Authenticated endpoints rate limit (more permissive)
        options.AddPolicy("authenticated", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetPartitionKeyForUser(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Specific login endpoint limit (most restrictive)
        options.AddPolicy("login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString(),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(5)
                }));

        // Configure rejection response
        options.OnRejected = async (context, cancellationToken) =>
        {
            TimeSpan? retryAfter = null;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var timeSpan))
            {
                retryAfter = timeSpan;
                context.HttpContext.Response.Headers.RetryAfter = 
                    ((int)timeSpan.TotalSeconds).ToString();
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                message = "Too many requests. Please try again later.",
                retryAfter = retryAfter.HasValue ? (int)retryAfter.Value.TotalSeconds : 0
            }, cancellationToken);
        };
    }

    private static string GetPartitionKeyForUser(HttpContext context)
    {
        // For authenticated users, use their ID as part of the key
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = context.Connection.RemoteIpAddress?.ToString();
        
        return userId != null ? $"{ip}_{userId}" : ip ?? "unknown";
    }
} 