using System.Collections.Immutable;
using System.Diagnostics;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.NotifyHub.Client;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

public class ActivityDataRefreshController(
    IConfiguration configuration,
    ILogger<ActivityDataRefreshController> logger,
    ServerConfigService serverConfigService,
    ActivityService activityService)
    : BaseApiController(configuration)
{
    [HttpGet]
    public async Task<ActionResult> OneShotKillActivityTenMinuteRefresh()
    {
        var configList = activityService.GetActivitiesByType(ActivityType.ActivityOneShotKill);
        var currentTime = TimeUtils.GetCurrentTime();
        foreach (var config in configList)
        {
            var startTime = TimeUtils.ParseDateTimeStrToUnixSecond(config.start_time);
            var endTime = TimeUtils.ParseDateTimeStrToUnixSecond(config.end_time);
            // 如果活动尚未开始，只走初始化的逻辑
            if (currentTime < startTime)
                await activityService.OneShotKillDataInit(config);
            // 活动已经开始的情况下，如果活动尚未结束，走正常结算的逻辑
            else if (currentTime < endTime)
                await activityService.OneShotKillTenMinuteUpdateAsync(config);
        }
        return Ok();
    }
}