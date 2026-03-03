using Serilog.Context;

namespace GameOutside.Util;

/// <summary>
/// 用于跟踪每个请求的生命周期中的日志输出
/// </summary>
public class LogPlayerMiddleware
{
    private readonly ILogger<LogPlayerMiddleware> _logger;

    private readonly RequestDelegate _next;

    public LogPlayerMiddleware(
        RequestDelegate next,
        ILogger<LogPlayerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        //登陆态需要跟踪玩家信息
        try
        {
            var headers = context.Request.Headers;
            if (headers.Count > 0 && headers.TryGetValue("x-user-id", out var userId))
            {
                LogContext.PushProperty("PlayerId", headers["x-player-id"].ToString());
                LogContext.PushProperty("ShardId", headers["x-player-shard"].ToString());
                LogContext.PushProperty("UserId", userId.ToString());
                LogContext.PushProperty("GameVersion", headers["x-game-version"].ToString());
                var distroId = headers["x-distro-id"].ToString();
                if (Guid.TryParse(distroId, out var d) && d != Guid.Empty)
                {
                    LogContext.PushProperty("DistroId", distroId);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Internal Exception");
            throw;
        }

        await _next(context);
    }
}