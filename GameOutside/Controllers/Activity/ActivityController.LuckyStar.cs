using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

public partial class ActivityController
{
    public record struct BuyLuckyStarReply(TakeRewardResult Reward, LuckyStarDataClient LuckyStarData);

    [HttpPost]
    public async Task<ActionResult<BuyLuckyStarReply>> BuyLuckyStar(int activityId)
    {
        var activityConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityLuckyStar, GameVersion);
        if (activityConfig == null || activityConfig.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<BuyLuckyStarReply>>(async _ =>
        {
            var luckyStarData = await activityService.GetActivityLuckStarDataAsync(PlayerId, PlayerShard, TrackingOptions.Tracking);
            if (luckyStarData == null)
                luckyStarData = activityService.AddActivityLuckyStarData(PlayerId, PlayerShard, activityConfig.id);
            // 已经到下一期活动了，需要重置一下
            if (luckyStarData.ActivityId != activityConfig.id)
                luckyStarData.Reset(activityConfig.id);

            if (!serverConfig.TryGetParameterInt(Params.LuckyStarMaxCycle, out int maxCycle))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

            if (luckyStarData.Cycle >= maxCycle)
                return BadRequest(ErrorKind.LUCKY_STAR_MAX_CYCLE.Response());

            var configList = serverConfig.GetActivityLuckyStarConfigListByActivityId(activityConfig.id);
            if (configList == null)
                return BadRequest(ErrorKind.NO_LUCKY_STAR_CONFIG.Response());
            // 上限检测
            if (luckyStarData.Sequence >= configList.Count)
                luckyStarData.Sequence = 0;
            var config = configList[luckyStarData.Sequence];
            int costDiamond = luckyStarData.Free ? 0 : config.cost_diamond;

            // 福星活动只会发放宝箱
            var userAsset = await userAssetService.GetUserAssetsWithTreasureBoxAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            if (userAsset.DiamondCount < costDiamond)
                return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());
            userAsset.DiamondCount -= costDiamond;

            var reward = new GeneralReward() { ItemList = [config.box_id], CountList = [1] };
            var (newCardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            luckyStarData.Sequence++;
            if (luckyStarData.Sequence >= configList.Count)
            {
                luckyStarData.Cycle++;
                luckyStarData.Sequence = 0;
            }

            // 随机免费概率
            var rand = Random.Shared.Next(0, 1000);
            luckyStarData.Free = rand < config.free_probability;

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new BuyLuckyStarReply() { Reward = result, LuckyStarData = luckyStarData.ToClientApi() });
        });
    }
}