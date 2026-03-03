using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class UserAttendanceController(
    IConfiguration configuration,
    ILogger<DivisionController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    UserAssetService userAssetService,
    UserAchievementService userAchievementService)
    : BaseApiController(configuration)
{

    [HttpPost]
    public async Task<ActionResult<UserAttendance>> GetUserAttendanceRecord()
    {
        return await context.WithRCUDefaultRetry<ActionResult<UserAttendance>>(async _ =>
        {
            bool changed = false;
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var attendanceRecord = await context.GetUserAttendanceRecord(PlayerShard, PlayerId);
            if (attendanceRecord == null)
            {
                attendanceRecord = context.CreateUserAttendanceRecord(PlayerShard, PlayerId, currentTime);
                changed = true;
            }

            if (!serverConfigService.TryGetParameterInt(Params.AttendanceTimeOffset, out int timeOffset))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });

            var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
            if (timeZoneOffset is null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var localMidnightEpoch = TimeUtils.GetLocalMidnightEpoch(timeZoneOffset.Value, timeOffset);
            if (localMidnightEpoch > attendanceRecord.LastLoginDate)
            {
                attendanceRecord.TotalLoginDays++;
                attendanceRecord.LastLoginDate = localMidnightEpoch;
                changed = true;
            }

            if (changed)
                await context.SaveChangesWithDefaultRetryAsync();

            return Ok(attendanceRecord);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> TakeAttendance(int dayIndex)
    {
        if (dayIndex < 0 || dayIndex >= serverConfigService.AttendanceRewardCount)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.ARG_OUT_OF_RANGE });

        return await context.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            var attendanceRecord = await context.GetUserAttendanceRecord(PlayerShard, PlayerId);
            if (attendanceRecord == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            // 已经领取过了
            if (attendanceRecord.RewardIndex.GetNthBits(dayIndex))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.ALREADY_TOOK_ATTENDANCE });

            // 还不能领取
            if (dayIndex >= attendanceRecord.TotalLoginDays)
                return BadRequest(new ErrorResponse { ErrorCode = (int)ErrorKind.NOT_READY_FOR_CLAIM });

            // 可以发放奖励了
            var attendanceReward = serverConfigService.GetAttendanceRewardConfig(dayIndex);
            if (attendanceReward == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.CACHE_UNKNOWN_ERROR });

            var generalReward = new GeneralReward()
            {
                ItemList = attendanceReward.item_list.ToList(),
                CountList = attendanceReward.count_list.ToList(),
            };

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (result == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
            attendanceRecord.RewardIndex = attendanceRecord.RewardIndex.SetNthBits(dayIndex, true);

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange is not null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(result);
        });
    }
}