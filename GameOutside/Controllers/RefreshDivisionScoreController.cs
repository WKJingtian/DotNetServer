using System.Collections.Immutable;
using System.Diagnostics;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.NotifyHub.Client;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

public class RefreshDivisionScoreController(
    IConfiguration configuration,
    ILogger<RefreshDivisionScoreController> logger,
    DivisionService divisionService,
    SeasonService seasonService,
    NotifyHubClient notifyHubClient)
    : BaseApiController(configuration)
{
    private static readonly Guid RefreshOkTemplateId = Guid.Parse(Environment.GetEnvironmentVariable("REFRESH_OK_TEMPLATE_ID")!);
    private static readonly Guid RefreshErrorTemplateId = Guid.Parse(Environment.GetEnvironmentVariable("REFRESH_ERROR_TEMPLATE_ID")!);
    private static readonly Guid RefreshWorldRankOkTemplateId = Guid.Parse(Environment.GetEnvironmentVariable("REFRESH_WORLD_RANK_OK_TEMPLATE_ID")!);
    private static readonly Guid RefreshWorldRankErrorTemplateId = Guid.Parse(Environment.GetEnvironmentVariable("REFRESH_WORLD_RANK_ERROR_TEMPLATE_ID")!);

    [HttpGet]
    public async Task<IActionResult> RefreshWorldRank(RefreshDivisionRequest request)
    {
        int? seasonToBeRefreshed = request.SeasonToBeRefreshed;
        int currentSeason = seasonService.GetCurrentSeasonNumber();
        seasonToBeRefreshed ??= currentSeason - 1;

        var refreshWorldRankResult = await ExecuteWithTimingAsync(
            async () => await divisionService.RefreshWorldRankAsync(seasonToBeRefreshed.Value),
            "RefreshWorldRank",
            seasonToBeRefreshed.Value);

        if (!refreshWorldRankResult.Success)
        {
            await NotifyRefreshWorldRankAsync(seasonToBeRefreshed, currentSeason, refreshWorldRankResult.ElapsedMilliseconds, 0, refreshWorldRankResult.Exception);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        await NotifyRefreshWorldRankAsync(seasonToBeRefreshed, currentSeason, refreshWorldRankResult.ElapsedMilliseconds, 0, null);
        return Ok(seasonToBeRefreshed.ToString());
    }

    [HttpGet]
    public async Task<IActionResult> RefreshDivision(RefreshDivisionRequest request)
    {
        int? seasonToBeRefreshed = request.SeasonToBeRefreshed;
        int currentSeason = seasonService.GetCurrentSeasonNumber();
        seasonToBeRefreshed ??= currentSeason - 1;

        var refreshResult = await ExecuteWithTimingAsync(
            async () => await divisionService.RefreshDivisionAsync(seasonToBeRefreshed.Value),
            "RefreshDivision",
            seasonToBeRefreshed.Value
        );
        if (!refreshResult.Success)
        {
            await NotifyRefreshDivisionResultAsync(seasonToBeRefreshed, currentSeason, refreshResult.ElapsedMilliseconds, 0, refreshResult.Exception);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        var cleanUpResult = await ExecuteWithTimingAsync(
            async () => await divisionService.CleanUpExpiredDataAsync(seasonToBeRefreshed.Value),
            "CleanUpExpiredData",
            seasonToBeRefreshed.Value
        );
        if (!cleanUpResult.Success)
        {
            await NotifyRefreshDivisionResultAsync(seasonToBeRefreshed, currentSeason, refreshResult.ElapsedMilliseconds, cleanUpResult.ElapsedMilliseconds, cleanUpResult.Exception);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        await NotifyRefreshDivisionResultAsync(seasonToBeRefreshed, currentSeason, refreshResult.ElapsedMilliseconds, cleanUpResult.ElapsedMilliseconds, null);
        return Ok(seasonToBeRefreshed.ToString());
    }

    private async Task<ExecutionResult> ExecuteWithTimingAsync(Func<Task> action, string operationName, int seasonNumber)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
            sw.Stop();
            logger.LogInformation("{OperationName} completed for season {SeasonNumber} in {ElapsedMilliseconds} ms",
                operationName, seasonNumber, sw.ElapsedMilliseconds);
            return new ExecutionResult(true, sw.ElapsedMilliseconds, null);
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogError(e, "Error in {OperationName} for season {SeasonNumber}, elapsed time: {ElapsedMilliseconds} ms",
                operationName, seasonNumber, sw.ElapsedMilliseconds);
            return new ExecutionResult(false, sw.ElapsedMilliseconds, e);
        }
    }

    private async ValueTask NotifyRefreshWorldRankAsync(int? seasonToBeRefreshed,
        int currentSeason, long refreshElapsedMilliseconds, long cleanUpElapsedMilliseconds, Exception? exception)
    {
        try
        {
            if (exception is null)
            {
                await notifyHubClient.SendTemplateMsgAsync(new SendTemplateMsgRequest(
                    TemplateId: RefreshWorldRankOkTemplateId,
                    GameIds: [14],
                    Context: new MsgFilterContext(Consts.Region),
                    Args: new Dictionary<string, string>
                    {
                        { "clusterName", Consts.Region },
                        { "seasonToBeRefreshed", seasonToBeRefreshed?.ToString() ?? (currentSeason - 1).ToString() },
                        { "currentSeason", currentSeason.ToString() },
                        { "refreshElapsedMilliseconds", refreshElapsedMilliseconds.ToString() },
                        { "cleanUpElapsedMilliseconds", cleanUpElapsedMilliseconds.ToString() }
                    }.ToImmutableDictionary()
                ));
            }
            else
            {
                await notifyHubClient.SendTemplateMsgAsync(new SendTemplateMsgRequest(
                    TemplateId: RefreshWorldRankErrorTemplateId,
                    GameIds: [14],
                    Context: new MsgFilterContext(Consts.Region),
                    Args: new Dictionary<string, string>
                    {
                        { "clusterName", Consts.Region },
                        { "seasonToBeRefreshed", seasonToBeRefreshed?.ToString() ?? (currentSeason - 1).ToString() },
                        { "currentSeason", currentSeason.ToString() },
                        { "exceptionMessage", exception.ToString() },
                        { "refreshElapsedMilliseconds", refreshElapsedMilliseconds.ToString() },
                        { "cleanUpElapsedMilliseconds", cleanUpElapsedMilliseconds.ToString() }
                    }.ToImmutableDictionary()
                ));
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send NotifyHub message for division refresh result");
        }
    }

    private async ValueTask NotifyRefreshDivisionResultAsync(int? seasonToBeRefreshed,
        int currentSeason, long refreshElapsedMilliseconds, long cleanUpElapsedMilliseconds, Exception? exception)
    {
        try
        {
            if (exception is null)
            {
                await notifyHubClient.SendTemplateMsgAsync(new SendTemplateMsgRequest(
                    TemplateId: RefreshOkTemplateId,
                    GameIds: [14],
                    Context: new MsgFilterContext(Consts.Region),
                    Args: new Dictionary<string, string>
                    {
                        { "clusterName", Consts.Region },
                        { "seasonToBeRefreshed", seasonToBeRefreshed?.ToString() ?? (currentSeason - 1).ToString() },
                        { "currentSeason", currentSeason.ToString() },
                        { "refreshElapsedMilliseconds", refreshElapsedMilliseconds.ToString() },
                        { "cleanUpElapsedMilliseconds", cleanUpElapsedMilliseconds.ToString() }
                    }.ToImmutableDictionary()
                ));
            }
            else
            {
                await notifyHubClient.SendTemplateMsgAsync(new SendTemplateMsgRequest(
                    TemplateId: RefreshErrorTemplateId,
                    GameIds: [14],
                    Context: new MsgFilterContext(Consts.Region),
                    Args: new Dictionary<string, string>
                    {
                        { "clusterName", Consts.Region },
                        { "seasonToBeRefreshed", seasonToBeRefreshed?.ToString() ?? (currentSeason - 1).ToString() },
                        { "currentSeason", currentSeason.ToString() },
                        { "exceptionMessage", exception.ToString() },
                        { "refreshElapsedMilliseconds", refreshElapsedMilliseconds.ToString() },
                        { "cleanUpElapsedMilliseconds", cleanUpElapsedMilliseconds.ToString() }
                    }.ToImmutableDictionary()
                ));
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send NotifyHub message for division refresh result");
        }
    }
}

public struct RefreshDivisionRequest
{
    /// <summary>
    /// 赛季号，为 null 时刷新上赛季
    /// </summary>
    public int? SeasonToBeRefreshed { get; set; }
}

internal record ExecutionResult(bool Success, long ElapsedMilliseconds, Exception? Exception);
