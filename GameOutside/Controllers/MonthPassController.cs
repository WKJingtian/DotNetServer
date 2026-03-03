using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameOutside.DBContext;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class MonthPassController(
    IConfiguration configuration,
    ILogger<MonthPassController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    UserAssetService userAssetService,
    BuildingGameDB context)
    : BaseApiController(configuration)
{
    public record struct MonthPassData(
        int nthDay, //今天是月卡第几天，-2则月卡未激活，-1则月卡已经过期
        int dayLeft, //月卡还有几天过期
        int claimStatus, // 本30天单元内有哪些天领取了奖励
        int totalDiamondCount, // 如果领取行为发生了，玩家账户内的玉璧总数
        int diamondAddCount); // 如果领取行为发生了，本次领取了多少玉璧

    [HttpPost]
    public async Task<ActionResult<MonthPassData>> ClaimDailyMonthPathReward()
    {
        // 月卡的奖励是自动发生的，因此条件不满足时不返回BadRequest
        // 月卡奖励只会发放玉璧，因此不取Detail数据
        var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
        if (userAsset == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        MonthPassData result = new();
        var monthPassInfo = await context.GetMonthPassInfo(PlayerId, PlayerShard);
        if (monthPassInfo == null)
            result.nthDay = -2;
        else
        {
            var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), monthPassInfo.PassAcquireTime, userAsset.TimeZoneOffset, 0);
            result.nthDay = dayDiff < monthPassInfo.PassDayLength ? dayDiff : -1;
            result.dayLeft = monthPassInfo.PassDayLength - dayDiff - 1;
        }
        //result.nthDay = await _context.GetNthDayOfMonthPass(PlayerId, PlayerShard);
        if (result.nthDay < 0 || monthPassInfo == null)
            return Ok(result);
        if (monthPassInfo.LastRewardClaimDay / 30 != result.nthDay / 30)
            monthPassInfo.RewardClaimStatus = 0;
        result.claimStatus = monthPassInfo.RewardClaimStatus;
        if (monthPassInfo.LastRewardClaimDay >= result.nthDay)
            return Ok(result);
        monthPassInfo.LastRewardClaimDay = result.nthDay;
        monthPassInfo.RewardClaimStatus |= 1 << (result.nthDay % 30);
        result.claimStatus = monthPassInfo.RewardClaimStatus;
        serverConfigService.TryGetParameterInt(Params.MonthPassDailyDiamond, out var diamondCount);
        GeneralReward generalReward = new()
        {
            ItemList = [0],
            CountList = [diamondCount]
        };
        // 这里确认只会有玉璧
        await userItemService.TakeReward(userAsset, generalReward, GameVersion);
        result.totalDiamondCount = userAsset.DiamondCount;
        result.diamondAddCount = diamondCount;

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }
        return Ok(result);
    }
}