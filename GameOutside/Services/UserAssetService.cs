using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Services;

public class UserAssetService(
    BuildingGameDB dbCtx,
    IUserAssetRepository userAssetRepository,
    ServerConfigService serverConfigService)
{
    /// <summary>
    /// 获取玩家资产信息，包含物品、卡牌、宝箱详细信息
    /// 重要：当使用该方法时，请首先思考下是否真的需要这么多信息，避免不必要的性能损耗
    /// </summary>
    public ValueTask<UserAssets?> GetUserAssetsDetailedAsync(short? shardId, long playerId)
    {
        return GetUserAssetsByIncludeOptionAsync(shardId, playerId, UserAssetIncludeOptions.IncludeAll);
    }

    /// <summary>
    /// 获取玩家资产信息，仅包含卡牌信息
    /// </summary>
    public ValueTask<UserAssets?> GetUserAssetsWithCardsAsync(short shardId, long playerId)
    {
        return GetUserAssetsByIncludeOptionAsync(shardId, playerId, UserAssetIncludeOptions.IncludeCards);
    }

    /// <summary>
    /// 获取玩家资产信息，仅包含物品信息
    /// </summary>
    public ValueTask<UserAssets?> GetUserAssetsWithItemsAsync(short shardId, long playerId)
    {
        return GetUserAssetsByIncludeOptionAsync(shardId, playerId, UserAssetIncludeOptions.IncludeItems);
    }

    /// <summary>
    /// 获取玩家资产信息，仅包含宝箱信息
    /// </summary>
    /// <returns></returns>
    public ValueTask<UserAssets?> GetUserAssetsWithTreasureBoxAsync(short shardId, long playerId)
    {
        return GetUserAssetsByIncludeOptionAsync(shardId, playerId, UserAssetIncludeOptions.IncludeTreasureBoxes);
    }

    /// <summary>
    /// 获取玩家资产信息，仅包含资产表信息，不包含物品、卡牌、宝箱详细信息
    /// </summary>
    public ValueTask<UserAssets?> GetUserAssetsSimpleAsync(short? shardId, long playerId)
    {
        return GetUserAssetsByIncludeOptionAsync(shardId, playerId, UserAssetIncludeOptions.NoInclude);
    }

    public ValueTask<UserAssets?> GetUserAssetsByIncludeOptionAsync(
        short? shardId,
        long playerId,
        UserAssetIncludeOptions includeOptions)
    {
        return dbCtx.WithDefaultRetry(_ => userAssetRepository.GetUserAssetsAsync(shardId, playerId, includeOptions));
    }

    /// <summary>
    /// 获取玩家时区偏移
    /// </summary>
    public ValueTask<int?> GetTimeZoneOffsetAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => userAssetRepository.GetTimeZoneOffsetAsync(shardId, playerId));
    }

    /// <summary>
    /// 获取玩家等级
    /// </summary>
    public ValueTask<UserLevelData?> GetLevelDataAsync(short? shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => userAssetRepository.GetLevelDataAsync(shardId, playerId));
    }

    public ValueTask<UserCard?> GetUserCardAsync(short shardId, long playerId, int cardId)
    {
        return dbCtx.WithDefaultRetry(_ => userAssetRepository.GetUserCardAsync(shardId, playerId, cardId));
    }
    
    /// <summary>
    /// 添加用户资产记录
    /// </summary>
    public void AddUserAssetAsync(UserAssets userAssets)
    {
        userAssetRepository.AddUserAsset(userAssets);
    }

    public void DetachUserAssetItems(UserAssets userAsset)
    {
        if (userAsset.UserItems.IsNullOrEmpty())
            return;
        // 需要倒序遍历，因为Detached的时候可能修改原列表
        int count = userAsset.UserItems.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            var item = userAsset.UserItems[i];
            var entry = dbCtx.Entry(item);
            if (entry.State != EntityState.Detached)
                entry.State = EntityState.Detached;
        }

        userAsset.UserItems.Clear();
    }

    public void DetachUserAssetCards(UserAssets userAsset)
    {
        if (userAsset.UserCards.IsNullOrEmpty())
            return;
        // 需要倒序遍历，因为Detached的时候可能修改原列表
        int count = userAsset.UserCards.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            var card = userAsset.UserCards[i];
            var entry = dbCtx.Entry(card);
            if (entry.State != EntityState.Detached)
                entry.State = EntityState.Detached;
        }

        userAsset.UserCards.Clear();
    }

    public Task UpsertUserCardsAsync(IEnumerable<UserCard> userCards)
    {
        return userAssetRepository.UpsertUserCardsAsync(userCards);
    }

    public Task UpsertUserItemsAsync(IEnumerable<UserItem> userItems)
    {
        return userAssetRepository.UpsertUserItemsAsync(userItems);
    }
}
