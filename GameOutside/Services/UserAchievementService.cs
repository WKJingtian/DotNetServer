using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;

namespace GameOutside.Services;

public class UserAchievementService(
    BuildingGameDB dbCtx,
    IUserAchievementRepository userAchievementRepository,
    ServerConfigService serverConfigService)
{
    public ValueTask<List<UserAchievement>> GetUserAchievementsAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => userAchievementRepository.GetUserAchievements(shardId, playerId, TrackingOptions.Tracking));
    }

    public ValueTask<List<UserAchievement>> GetReadonlyUserAchievementsAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => userAchievementRepository.GetUserAchievements(shardId, playerId, TrackingOptions.NoTracking));
    }

    public ValueTask<UserAchievement?> GetUserAchievementInfoAsync(short shardId, long playerId, int configId, string target)
    {
        return dbCtx.WithDefaultRetry(_ => userAchievementRepository.GetUserAchievementInfo(shardId, playerId, configId, target, TrackingOptions.Tracking));
    }

    public Task UpsertUserAchievementsAsync(IEnumerable<UserAchievement> userAchievements)
    {
        return userAchievementRepository.UpsertUserAchievementsAsync(userAchievements);
    }

    public async ValueTask<List<UserAchievement>> IncreaseAchievementProgressAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId)
    {
        return await userAchievementRepository.IncreaseAchievementProgressAsync(
            records, shardId, playerId);
    }

    public async ValueTask<List<UserAchievement>> UpdateAchievementProgressAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId)
    {
        return await userAchievementRepository.UpdateAchievementProgressAsync(
            records, shardId, playerId);
    }

    public async Task<List<UserAchievement>> IncreaseTreasureBoxAchievementAsync(
        List<TreasureBoxConfig> configs,
        List<int> counts,
        short shardId,
        long playerId)
    {
        if (!configs.Count.Equals(counts.Count))
            throw new Exception("Sequence count not equal");
        var records = new List<AchievementRecord>();
        for (int i = 0; i < configs.Count; i++)
        {
            var config = configs[i];
            int count = counts[i];
            var itemConfig = serverConfigService.GetItemConfigById(config.id);
            if (itemConfig == null)
                continue;
            var quality = (int)itemConfig.quality;
            records.Add(
                new AchievementRecord
                {
                    Key = AchievementKeys.OpenTreasureBox,
                    Target = quality.ToString(),
                    Count = count
                });
        }
        return await IncreaseAchievementProgressAsync(records, shardId, playerId);
    }

    public async Task<List<UserAchievement>> UpdateCardUpgradeAchievementProgressAsync(
        List<UserCard> cards,
        short shardId,
        long playerId)
    {
        if (cards.IsNullOrEmpty())
            return [];

        var records = new List<AchievementRecord>();
        foreach (var card in cards)
        {
            var (key, target) = GetAchievementKeyAndTarget(card);
            if (key == null || target == null)
                continue;
            records.Add(new AchievementRecord() { Key = key, Target = target, Count = card.CardLevel });
        }

        return await UpdateAchievementProgressAsync(records, shardId, playerId);
    }

    public (string? key, string? target) GetAchievementKeyAndTarget(UserCard card)
    {
        var itemConfig = serverConfigService.GetItemConfigById(card.CardId);
        if (itemConfig == null)
            return (null, null);
        var key = GetUpgradeCardAchievementKeyByQuality(itemConfig.quality);
        if (key == null)
            return (null, null);
        return (key, card.CardId.ToString());
    }

    private static string? GetUpgradeCardAchievementKeyByQuality(ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.Ordinary => AchievementKeys.CardUpgradeQuality0,
            ItemQuality.Rare => AchievementKeys.CardUpgradeQuality1,
            ItemQuality.Unique => AchievementKeys.CardUpgradeQuality2,
            ItemQuality.Unrivaled => AchievementKeys.CardUpgradeQuality3,
            _ => null,
        };
    }
}