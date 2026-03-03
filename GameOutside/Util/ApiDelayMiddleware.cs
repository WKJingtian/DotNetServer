using Microsoft.Extensions.Options;

namespace GameOutside.Util;

public class ApiDelayMiddleware
{
    private readonly RequestDelegate next;
    private readonly IOptionsMonitor<ApiDelayMiddlewareOptions> optionsMonitor;

    public ApiDelayMiddleware(RequestDelegate next, IOptionsMonitor<ApiDelayMiddlewareOptions> optionsMonitor)
    {
        this.next = next;
        this.optionsMonitor = optionsMonitor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.Enabled &&
            options.DelayMilliseconds > 0 &&
            ShouldDelay(context, options))
        {
            await Task.Delay(options.DelayMilliseconds, context.RequestAborted);
        }

        await next(context);
    }

    private static bool ShouldDelay(HttpContext context, ApiDelayMiddlewareOptions options)
    {
        if (options.PathPrefixes.Count == 0)
            return true;

        var requestPath = context.Request.Path;
        foreach (var prefix in options.PathPrefixes)
        {
            if (requestPath.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public class ApiDelayMiddlewareOptions
{
    public bool Enabled { get; set; }
    public int DelayMilliseconds { get; set; }
    public HashSet<string> PathPrefixes { get; set; } = [];
}
