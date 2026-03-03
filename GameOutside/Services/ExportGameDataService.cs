using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.Controllers;
using GameOutside.DBContext;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Services;

public class ExportGameDataService(
    UserAssetService userAssetService,
    UserInfoService userInfoService,
    ActivityService activityService,
    IapPackageService iapPackageService,
    GameService gameService,
    BuildingGameDB dbCtx,
    UserAchievementService userAchievementService)
{
    public async Task<GmController.UserSpec> ExportPlayerInfo(long playerId, short shardId)
    {
        return new GmController.UserSpec()
        {
            UserAssets = await userAssetService.GetUserAssetsDetailedAsync(shardId, playerId),
            UserCustomData = await dbCtx.GetUserDataAsync(shardId, playerId),
            UserDivision = await dbCtx.UserDivisions.FindAsync(playerId, shardId),
            UserDailyStoreItems = await dbCtx.GetUserDailyStoreItems(shardId, playerId),
            UserDailyStoreIndex = await dbCtx.GetUserDailyStoreIndex(shardId, playerId),
            // 这个跟赛季相关了，还要改同步redis数据，就不导出了
            // UserEndlessRank = await dbCtx.UserEndlessRanks.FindAsync(playerId, shardId),
            UserGameInfo = await gameService.GetUserGameInfoByIdAsync(shardId, playerId),
            UserInfo = await userInfoService.GetUserInfoWithHistoriesAsync(shardId, playerId, "100.0.0"),
            // 这个跟赛季相关了，还要改同步redis数据，就不导出了
            // UserRank = await dbCtx.UserRanks.FindAsync(playerId, shardId),
            UserCommodityBoughtRecords = await dbCtx.GetAllBoughtCommodityRecords(shardId, playerId),
            UserAttendance = await dbCtx.GetUserAttendanceRecord(shardId, playerId),
            UserAchievements = await userAchievementService.GetReadonlyUserAchievementsAsync(shardId, playerId),
            UserBeginnerTasks = await dbCtx.GetBeginnerTaskAsync(shardId, playerId),
            UserBattlePassInfos = await dbCtx.GetUserBattlePassInfo(shardId, playerId),
            UserCustomCardPools = await dbCtx.GetAllUserCustomCardPoolsAsync(shardId, playerId),
            UserFixedLevelMapProgresses = await dbCtx.GetAllUserFixedLevelMapProgresses(playerId, shardId),
            UserIapPurchaseRecords = await iapPackageService.GetFullPurchaseListAsync(shardId, playerId),
            ActivityLuckyStar = await activityService.GetActivityLuckStarDataAsync(playerId, shardId, TrackingOptions.NoTracking),
            PromotionStatus = await iapPackageService.GetPromotionData(playerId, shardId),
            UserStarStoreInfos = await dbCtx.GetOrAddUserStarRewardClaimStatusAsync(playerId, shardId),
            ActivityPiggyBank = await activityService.GetPiggyBankStatusAsync(playerId, shardId),
            ActivityUnrivaledGods
                = await dbCtx.ActivityUnrivaledGods.Where(t => t.PlayerId == playerId && t.ShardId == shardId)
                    .ToListAsync(),
            UserFortuneBags = await dbCtx.UserFortuneBagInfos.Where(t => t.PlayerId == playerId && t.ShardId == shardId)
                .ToListAsync(),
            MonsMonthPassInfo = await dbCtx.GetMonthPassInfo(playerId, shardId),
            ActivityCoopBossInfo = await dbCtx.ActivityCoopBossInfoSet
                .Where(t => t.PlayerId == playerId && t.ShardId == shardId)
                .ToListAsync(),
            ActivityTreasureMazeInfo = await dbCtx.ActivityTreasureMazeInfos
                .Where(t => t.PlayerId == playerId && t.ShardId == shardId)
                .ToListAsync(),
            UserDailyTask = await dbCtx.GetDailyTask(shardId, playerId),
            UserIdleReward = await dbCtx.GetUserIdleRewardInfo(shardId, playerId)
        };
    }

    public void DeleteUserRecords(long playerId)
    {
        dbCtx.UserAssets.RemoveRange(dbCtx.UserAssets.Where(u => u.PlayerId == playerId));
        dbCtx.UserCards.RemoveRange(dbCtx.UserCards.Where(u => u.PlayerId == playerId));
        dbCtx.UserCustomData.RemoveRange(dbCtx.UserCustomData.Where(u => u.PlayerId == playerId));
        dbCtx.UserDivisions.RemoveRange(dbCtx.UserDivisions.Where(u => u.PlayerId == playerId));
        dbCtx.UserDailyStoreItems.RemoveRange(dbCtx.UserDailyStoreItems.Where(u => u.PlayerId == playerId));
        dbCtx.UserDailyStoreIndices.RemoveRange(dbCtx.UserDailyStoreIndices.Where(u => u.PlayerId == playerId));
        dbCtx.UserEndlessRanks.RemoveRange(dbCtx.UserEndlessRanks.Where(u => u.PlayerId == playerId));
        dbCtx.UserGameInfos.RemoveRange(dbCtx.UserGameInfos.Where(u => u.PlayerId == playerId));
        dbCtx.UserHistories.RemoveRange(dbCtx.UserHistories.Where(u => u.PlayerId == playerId));
        dbCtx.UserInfos.RemoveRange(dbCtx.UserInfos.Where(u => u.PlayerId == playerId));
        dbCtx.UserItems.RemoveRange(dbCtx.UserItems.Where(u => u.PlayerId == playerId));
        dbCtx.UserRanks.RemoveRange(dbCtx.UserRanks.Where(u => u.PlayerId == playerId));
        dbCtx.UserTreasureBoxes.RemoveRange(dbCtx.UserTreasureBoxes.Where(u => u.PlayerId == playerId));
        dbCtx.UserCommodityBoughtRecords.RemoveRange(
            dbCtx.UserCommodityBoughtRecords.Where(u => u.PlayerId == playerId));
        dbCtx.UserAttendances.RemoveRange(dbCtx.UserAttendances.Where(u => u.PlayerId == playerId));
        dbCtx.UserAchievements.RemoveRange(dbCtx.UserAchievements.Where(u => u.PlayerId == playerId));
        dbCtx.UserBeginnerTasks.RemoveRange(dbCtx.UserBeginnerTasks.Where(u => u.PlayerId == playerId));
        dbCtx.UserBattlePassInfos.RemoveRange(dbCtx.UserBattlePassInfos.Where(u => u.PlayerId == playerId));
        dbCtx.UserMallAdvertisements.RemoveRange(dbCtx.UserMallAdvertisements.Where(u => u.PlayerId == playerId));
        dbCtx.UserGlobalInfos.RemoveRange(dbCtx.UserGlobalInfos.Where(u => u.LastLoginPlayerId == playerId));
        // dbCtx.PlatformNotifies.RemoveRange(dbCtx.PlatformNotifies.Where(u => u.PlayerId == playerId));
        // dbCtx.PaidOrders.RemoveRange(dbCtx.PaidOrders.Where(u => u.PlayerId == playerId));
        dbCtx.UserCustomCardPools.RemoveRange(dbCtx.UserCustomCardPools.Where(u => u.PlayerId == playerId));
        dbCtx.UserFixedLevelMapProgress.RemoveRange(dbCtx.UserFixedLevelMapProgress.Where(u => u.PlayerId == playerId));
        dbCtx.UserIapPurchases.RemoveRange(dbCtx.UserIapPurchases.Where(u => u.PlayerId == playerId));
        dbCtx.UserIdleRewardInfos.RemoveRange(dbCtx.UserIdleRewardInfos.Where(u => u.PlayerId == playerId));
        dbCtx.PromotionStatus.RemoveRange(dbCtx.PromotionStatus.Where(u => u.PlayerId == playerId));
        dbCtx.UserStarStoreStatus.RemoveRange(dbCtx.UserStarStoreStatus.Where(u => u.PlayerId == playerId));
        dbCtx.ActivityLuckyStars.RemoveRange(dbCtx.ActivityLuckyStars.Where(u => u.PlayerId == playerId));
        dbCtx.UserFortuneBagInfos.RemoveRange(dbCtx.UserFortuneBagInfos.Where(u => u.PlayerId == playerId));
        dbCtx.ActivityUnrivaledGods.RemoveRange(dbCtx.ActivityUnrivaledGods.Where(u => u.PlayerId == playerId));
        dbCtx.UserMonthPassInfos.RemoveRange(dbCtx.UserMonthPassInfos.Where(u => u.PlayerId == playerId));
        dbCtx.ActivityCoopBossInfoSet.RemoveRange(dbCtx.ActivityCoopBossInfoSet.Where(u => u.PlayerId == playerId));
        dbCtx.ActivityTreasureMazeInfos.RemoveRange(dbCtx.ActivityTreasureMazeInfos.Where(u => u.PlayerId == playerId));
        dbCtx.UserDailyTasks.RemoveRange(dbCtx.UserDailyTasks.Where(u => u.PlayerId == playerId));
        dbCtx.ActivityPiggyBanks.RemoveRange(dbCtx.ActivityPiggyBanks.Where(u => u.PlayerId == playerId));
        dbCtx.ActivityEndlessChallenges.RemoveRange(dbCtx.ActivityEndlessChallenges.Where(u => u.PlayerId == playerId));
        dbCtx.UserDailyTreasureBoxProgresses.RemoveRange(
            dbCtx.UserDailyTreasureBoxProgresses.Where(u => u.PlayerId == playerId));
    }

    public void AddUserSpec(GmController.UserSpec userSpec)
    {
        if (userSpec.UserAssets != null) dbCtx.UserAssets.Add(userSpec.UserAssets);
        if (userSpec.UserCustomData != null) dbCtx.UserCustomData.Add(userSpec.UserCustomData);
        if (userSpec.UserDivision != null) dbCtx.UserDivisions.Add(userSpec.UserDivision);
        dbCtx.UserDailyStoreItems.AddRange(userSpec.UserDailyStoreItems);
        if (userSpec.UserDailyStoreIndex != null)
            dbCtx.UserDailyStoreIndices.Add(userSpec.UserDailyStoreIndex);
        if (userSpec.UserGameInfo != null) dbCtx.UserGameInfos.Add(userSpec.UserGameInfo);
        if (userSpec.UserInfo != null) dbCtx.UserInfos.AddRange(userSpec.UserInfo);
        if (userSpec.UserAssets != null)
            dbCtx.UserTreasureBoxes.AddRange(userSpec.UserAssets.UserTreasureBoxes);
        dbCtx.UserCommodityBoughtRecords.AddRange(userSpec.UserCommodityBoughtRecords);
        if (userSpec.UserAttendance != null) dbCtx.UserAttendances.Add(userSpec.UserAttendance);
        dbCtx.UserAchievements.AddRange(userSpec.UserAchievements);
        if (userSpec.UserBeginnerTasks != null) dbCtx.UserBeginnerTasks.Add(userSpec.UserBeginnerTasks);
        dbCtx.UserBattlePassInfos.AddRange(userSpec.UserBattlePassInfos);
        dbCtx.UserCustomCardPools.AddRange(userSpec.UserCustomCardPools);
        dbCtx.UserFixedLevelMapProgress.AddRange(userSpec.UserFixedLevelMapProgresses);
        dbCtx.UserIapPurchases.AddRange(userSpec.UserIapPurchaseRecords);
        if (userSpec.ActivityLuckyStar != null)
            dbCtx.ActivityLuckyStars.Add(userSpec.ActivityLuckyStar);
        if (userSpec.PromotionStatus != null)
            dbCtx.PromotionStatus.Add(userSpec.PromotionStatus);
        if (userSpec.UserStarStoreInfos != null)
            dbCtx.UserStarStoreStatus.Add(userSpec.UserStarStoreInfos);
        if (userSpec.ActivityPiggyBank != null)
            dbCtx.ActivityPiggyBanks.Add(userSpec.ActivityPiggyBank);
        dbCtx.ActivityUnrivaledGods.AddRange(userSpec.ActivityUnrivaledGods);
        dbCtx.UserFortuneBagInfos.AddRange(userSpec.UserFortuneBags);
        if (userSpec.MonsMonthPassInfo != null)
            dbCtx.UserMonthPassInfos.Add(userSpec.MonsMonthPassInfo);
        dbCtx.ActivityCoopBossInfoSet.AddRange(userSpec.ActivityCoopBossInfo);
        dbCtx.ActivityTreasureMazeInfos.AddRange(userSpec.ActivityTreasureMazeInfo);
        if (userSpec.UserDailyTask != null)
            dbCtx.UserDailyTasks.Add(userSpec.UserDailyTask);
        if (userSpec.UserIdleReward != null)
            dbCtx.UserIdleRewardInfos.Add(userSpec.UserIdleReward);
    }
}