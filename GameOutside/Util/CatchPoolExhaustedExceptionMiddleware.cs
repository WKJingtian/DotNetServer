using System.Text.Json;
using ChillyRoom.Infra.ApiController;
using Npgsql;

namespace GameOutside.Util;

public static class PoolExhaustedResponse
{
    public static readonly string Singleton = JsonSerializer.Serialize(new ErrorResponse
    {
        ErrorCode = 34,
        Message = "Server overload, pool exhausted",
    });
}

public class CatchPoolExhaustedExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public CatchPoolExhaustedExceptionMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NpgsqlException e) when (e.Message.StartsWith("The connection pool has been exhausted"))
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(PoolExhaustedResponse.Singleton);
        }
        catch (NpgsqlException e) when (e.InnerException is TimeoutException)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(PoolExhaustedResponse.Singleton);
        }
        catch (InvalidOperationException e) when (e.InnerException is TimeoutException)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(PoolExhaustedResponse.Singleton);
        }
    }
}