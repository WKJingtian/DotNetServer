using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

public partial class ActivityController
{
    public record struct FortuneBagClaimReply(int resultDiamondCount, int diamondAddCount);

    public async Task<(bool, FortuneBagClaimReply?)> ClaimExpiredFortuneBag(int newActivityId, UserAssets asset)
    {
        // 当前福袋中只有玉璧，因此返回值只有int
        // 如果newActivityId是-1，则代表当前没有活跃的福袋事件，则不需要更新数据库的activityId
        var result = new FortuneBagClaimReply() { diamondAddCount = 0 };
        bool databaseChanged = false;
        return await dbContext.WithRCUDefaultRetry<(bool, FortuneBagClaimReply?)>(async _ =>
        {
            var fortuneBagInfo = await activityService.GetUserFortuneBagInfoAsync(PlayerId, PlayerShard, TrackingOptions.Tracking);
            if (fortuneBagInfo != null && fortuneBagInfo.ActivityId != newActivityId &&
                fortuneBagInfo.FortuneBags.Count > 0)
            {
                var bagConfig = serverConfig.GetActivityFortuneBagConfigByActivityId(fortuneBagInfo.ActivityId);
                if (bagConfig != null)
                {
                    databaseChanged = true;
                    var bagContentList = bagConfig.fortune_bag_diamond_count;
                    foreach (var bag in fortuneBagInfo.FortuneBags)
                    {
                        for (int claimTime = bag.ClaimStatus; claimTime < bagContentList.Count; claimTime++)
                            result.diamondAddCount += bagContentList[claimTime] * bag.BagCount;
                    }

                    fortuneBagInfo.FortuneBags.Clear();
                    asset.DiamondCount += result.diamondAddCount;
                    result.resultDiamondCount = asset.DiamondCount;
                    dbContext.Entry(fortuneBagInfo).Property(t => t.FortuneBags).IsModified = true;
                }
            }

            if (newActivityId != -1 && (fortuneBagInfo == null || fortuneBagInfo.ActivityId != newActivityId))
            {
                databaseChanged = true;
                if (fortuneBagInfo == null)
                    activityService.AddUserFortuneBagInfo(PlayerId, PlayerShard, newActivityId);
                else
                {
                    fortuneBagInfo.Reset(newActivityId);
                    dbContext.Entry(fortuneBagInfo).Property(t => t.FortuneBags).IsModified = true;
                }
            }

            return (databaseChanged, result);
        });
    }

    [HttpPost]
    public async Task<ActionResult<FortuneBagClaimReply>> ClaimAllFortuneBagReward(int activityId)
    {
        var result = new FortuneBagClaimReply() { diamondAddCount = 0 };
        return await dbContext.WithRCUDefaultRetry<ActionResult<FortuneBagClaimReply>>(async _ =>
        {
            var fortuneBagInfo = await activityService.GetUserFortuneBagInfoAsync(PlayerId, PlayerShard, TrackingOptions.Tracking);
            if (fortuneBagInfo == null || fortuneBagInfo.ActivityId != activityId)
                return BadRequest(ErrorKind.USER_FORTUNE_BAG_DATA_MISSING.Response());

            var bagConfig = serverConfig.GetActivityFortuneBagConfigByActivityId(fortuneBagInfo.ActivityId);
            if (bagConfig == null)
                return BadRequest(ErrorKind.NO_FORTUNE_BAG_CONFIG.Response());

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var bagContentList = bagConfig.fortune_bag_diamond_count;
            foreach (var bag in fortuneBagInfo.FortuneBags)
            {
                var dayDifference = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), bag.AcquireTime,
                    userAsset.TimeZoneOffset, 0);
                int claimTime = bag.ClaimStatus;
                for (; claimTime < bagContentList.Count && claimTime <= dayDifference; claimTime++)
                    result.diamondAddCount += bagContentList[claimTime] * bag.BagCount;
                bag.ClaimStatus = claimTime;
            }

            userAsset.DiamondCount += result.diamondAddCount;
            result.resultDiamondCount = userAsset.DiamondCount;
            dbContext.Entry(fortuneBagInfo).Property(t => t.FortuneBags).IsModified = true;

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(result);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult?>> ClaimServerFortuneLevelReward(int activityId)
    {
        var activityConfig = serverConfig.GetActivityConfigById(activityId);
        if (activityConfig == null)
            return BadRequest(ErrorKind.NO_ACTIVITY_CONFIG.Response());
        if (!activityConfig.activity_type.Equals(ActivityType.ActivityFortuneBag))
            return BadRequest(ErrorKind.WRONG_ACTIVITY_TYPE.Response());
        var fortuneBagLevels = serverConfig.GetActivityFortuneBagLevelConfigListByActivityId(activityId);
        if (fortuneBagLevels == null)
            return BadRequest(ErrorKind.NO_ACTIVITY_CONFIG.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult?>>(async _ =>
        {
            // 这里确定只会发放玉璧，所以不取Detail数据
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var serverFortuneLevel
                = await activityService.GetFortuneBagLevelAsync(activityId);
            Dictionary<int, int> fortuneLevelRewardDict = new();
            var fortuneBagInfo
                = await activityService.GetUserFortuneBagInfoAsync(PlayerId, PlayerShard, TrackingOptions.Tracking);
            if (fortuneBagInfo == null || fortuneBagInfo.ActivityId != activityId)
                return BadRequest(ErrorKind.USER_FORTUNE_BAG_DATA_MISSING.Response());

            // 服务器聚福等级带来的奖励
            for (int i = 0; i < fortuneBagLevels.Count; i++)
            {
                var levelConfig = fortuneBagLevels[i];
                if (serverFortuneLevel < levelConfig.fortune_bag_required)
                {
                    fortuneBagInfo.FortuneLevelRewardClaimStatus = i;
                    break;
                }

                if (fortuneBagInfo.FortuneLevelRewardClaimStatus > levelConfig.id)
                    continue;
                for (int ii = 0; ii < levelConfig.item_list.Count; ii++)
                {
                    fortuneLevelRewardDict.TryAdd(levelConfig.item_list[ii], 0);
                    fortuneLevelRewardDict[levelConfig.item_list[ii]] += levelConfig.count_list[ii];
                }

                fortuneBagInfo.FortuneLevelRewardClaimStatus = i + 1;
            }

            var generalReward = new GeneralReward() { ItemList = new(), CountList = new() };
            foreach (var item in fortuneLevelRewardDict)
            {
                generalReward.ItemList.Add(item.Key);
                generalReward.CountList.Add(item.Value);
            }

            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            if (newCardList.Count > 0)
            {
                var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
                if (takeRewardResult.AssetsChange is not null)
                    takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            }
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();
            return Ok(takeRewardResult);
        });
    }

    [HttpPost]
    public async Task<ActionResult<FortuneBagDataClient?>> RefreshFortuneBagActivityInfo(int activityId)
    {
        var info = await activityService.FetchFortuneBagActivityInfoAsync(activityId, PlayerId, PlayerShard);
        return Ok(info);
    }
}