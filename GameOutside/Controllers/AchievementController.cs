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
public class AchievementController(
    IConfiguration configuration,
    ILogger<AchievementController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    UserAssetService userAssetService,
    UserAchievementService userAchievementService)
    : BaseApiController(configuration)
{
    [HttpPost]
    public async Task<ActionResult<List<UserAchievement>>> GetUserAchievements()
    {
        var achievements = await userAchievementService.GetReadonlyUserAchievementsAsync(PlayerShard, PlayerId);
        return Ok(achievements);
    }

    public record struct GetAchievementRewardReply(UserAchievement Achievement, TakeRewardResult Result);

    [HttpPost]
    public async Task<ActionResult<GetAchievementRewardReply>> GetAchievementReward(int configId, string target)
    {
        return await context.WithRCUDefaultRetry<ActionResult<GetAchievementRewardReply>>(async _ =>
        {
            UserAchievement? achievement
                = await userAchievementService.GetUserAchievementInfoAsync(PlayerShard, PlayerId, configId, target);
            if (achievement == null)
                return BadRequest(ErrorKind.NO_ACHIEVEMENT_RECORD.Response());
            // 这里确定只会发放货币和经验，所以不取Detail数据
            var userAssets = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var rewards = new GeneralReward();
            var error = GetAchievementReward(achievement, rewards);
            if (error != ErrorKind.SUCCESS)
                return BadRequest(error.Response());
            rewards.DistinctAndMerge();
            var (newCardList, result) = await userItemService.TakeReward(userAssets, rewards, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new GetAchievementRewardReply() { Achievement = achievement, Result = result });
        });
    }

    public record struct GetAllAchievementRewardReply(List<UserAchievement> Achievements, TakeRewardResult Result);

    [HttpPost]
    public async Task<ActionResult<GetAllAchievementRewardReply>> GetAllAchievementReward()
    {
        return await context.WithRCUDefaultRetry<ActionResult<GetAllAchievementRewardReply>>(async _ =>
        {
            var achievements = await userAchievementService.GetReadonlyUserAchievementsAsync(PlayerShard, PlayerId);
            // 这里确定只会发放货币和经验，所以不取Detail数据
            var userAssets = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var rewards = new GeneralReward();

            List<UserAchievement> changedAchievements = new();
            foreach (var achievement in achievements)
            {
                var error = GetAchievementReward(achievement, rewards);
                if (error == ErrorKind.SUCCESS)
                    changedAchievements.Add(achievement);
            }

            rewards.DistinctAndMerge();
            var (newCardList, result) = await userItemService.TakeReward(userAssets, rewards, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            await userAchievementService.UpsertUserAchievementsAsync(changedAchievements);
            var finalAchievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(finalAchievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new GetAllAchievementRewardReply() { Achievements = changedAchievements, Result = result });
        });
    }

    private ErrorKind GetAchievementReward(UserAchievement achievement, GeneralReward rewardAccu)
    {
        var achievementConfig = serverConfigService.GetAchievementConfigById(achievement.ConfigId);
        if (achievementConfig == null)
            return ErrorKind.NO_ACHIEVEMENT_CONFIG;
        if (achievement.Received)
            return ErrorKind.ACHIEVEMENT_REWARD_CLAIMED;
        var currentIndex = achievement.CurrentIndex;
        if (currentIndex >= achievementConfig.progress_list.Length)
            return ErrorKind.ACHIEVEMENT_FINISHED;
        var targetProgress = achievementConfig.progress_list[currentIndex];
        if (achievement.Progress < targetProgress)
            return ErrorKind.ACHIEVEMENT_NOT_COMPLETED;

        bool isCardUpgradeAchievement = achievementConfig.key.StartsWith("card_upgrade");
        // 领奖判定，需要循环领取一下
        while (currentIndex < achievementConfig.progress_list.Length)
        {
            targetProgress = achievementConfig.progress_list[currentIndex];
            if (achievement.Progress < targetProgress)
                break;
            int itemId = achievementConfig.reward_list[currentIndex];
            int itemCount = achievementConfig.count_list[currentIndex];
            rewardAccu.AddReward(itemId, itemCount);
            currentIndex++;
            if (achievementConfig.cycling)
                currentIndex %= achievementConfig.progress_list.Length;
            if (!isCardUpgradeAchievement)
                achievement.Progress -= targetProgress;
        }

        achievement.Received = currentIndex >= achievementConfig.progress_list.Length;
        // 限制一下currentIndex不能超出progress_list的长度
        achievement.CurrentIndex = Math.Min(achievementConfig.progress_list.Length - 1, currentIndex);
        return ErrorKind.SUCCESS;
    }
}