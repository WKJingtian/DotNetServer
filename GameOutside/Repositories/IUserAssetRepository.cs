using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

/// <summary>
/// 用户资产查询联表选项
/// </summary>
[Flags]
public enum UserAssetIncludeOptions
{
    /// <summary>
    /// 不需要联表查询
    /// </summary>
    NoInclude = 1,

    /// <summary>
    /// 仅联表查询卡牌
    /// </summary>
    IncludeCards = 1 << 1,

    /// <summary>
    /// 仅联表查询 Items
    /// </summary>
    IncludeItems = 1 << 2,

    /// <summary>
    /// 联表查询 TreasureBoxes
    /// </summary>
    IncludeTreasureBoxes = 1 << 3,

    /// <summary>
    /// 联表查询所有关联数据
    /// </summary>
    IncludeAll = IncludeCards | IncludeItems | IncludeTreasureBoxes,
}

public interface IUserAssetRepository
{
    public Task<UserAssets?> GetUserAssetsAsync(short? shardId, long playerId, UserAssetIncludeOptions includeOptions);
    public Task<int?> GetTimeZoneOffsetAsync(short shardId, long playerId);
    public Task<UserLevelData?> GetLevelDataAsync(short? shardId, long playerId);
    public Task<bool> IsUserAssetExistsAsync(short shardId, long playerId);
    public void AddUserAsset(UserAssets userAssets);
    public Task<UserCard?> GetUserCardAsync(short shardId, long playerId, int cardId);
    public Task UpsertUserCardsAsync(IEnumerable<UserCard> userCards);
    public Task UpsertUserItemsAsync(IEnumerable<UserItem> userItems);
}

public class UserAssetRepository(BuildingGameDB dbCtx) : IUserAssetRepository
{
    public Task<UserAssets?> GetUserAssetsAsync(short? shardId, long playerId, UserAssetIncludeOptions includeOptions)
    {
        var query = dbCtx.UserAssets.AsQueryable();

        bool includeItems = (includeOptions & UserAssetIncludeOptions.IncludeItems) != 0;
        bool includeCards = (includeOptions & UserAssetIncludeOptions.IncludeCards) != 0;
        bool includeTreasureBoxes = (includeOptions & UserAssetIncludeOptions.IncludeTreasureBoxes) != 0;

        if (!includeItems && !includeCards && !includeTreasureBoxes)
        {
            query = query.IgnoreAutoIncludes();
        }

        int includeCount = (includeItems ? 1 : 0) + (includeCards ? 1 : 0) + (includeTreasureBoxes ? 1 : 0);
        if (includeCount > 1)
        {
            query = query.AsSplitQuery();
        }

        if (includeItems)
        {
            query = query.Include(u => u.UserItems);
        }

        if (includeCards)
        {
            query = query.Include(u => u.UserCards);
        }

        if (includeTreasureBoxes)
        {
            query = query.Include(u => u.UserTreasureBoxes);
        }

        return query.FirstOrDefaultAsync(u =>
            shardId.HasValue ? u.ShardId == shardId && u.PlayerId == playerId : u.PlayerId == playerId);
    }

    public Task<int?> GetTimeZoneOffsetAsync(short shardId, long playerId)
    {
        return dbCtx.UserAssets
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(u => u.ShardId == shardId && u.PlayerId == playerId)
            .Select(u => (int?)u.TimeZoneOffset)
            .FirstOrDefaultAsync();
    }

    public Task<UserLevelData?> GetLevelDataAsync(short? shardId, long playerId)
    {
        return dbCtx.UserAssets
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(u => shardId.HasValue ? u.ShardId == shardId && u.PlayerId == playerId : u.PlayerId == playerId)
            .Select(u => u.LevelData)
            .FirstOrDefaultAsync();
    }

    public Task<bool> IsUserAssetExistsAsync(short shardId, long playerId)
    {
        return dbCtx.UserAssets.AsNoTracking().AnyAsync(u => u.PlayerId == playerId && u.ShardId == shardId);
    }

    public void AddUserAsset(UserAssets userAssets)
    {
        dbCtx.UserAssets.Add(userAssets);
    }

    public Task<UserCard?> GetUserCardAsync(short shardId, long playerId, int cardId)
    {
        return dbCtx.UserCards.Where(u => u.ShardId == shardId && u.PlayerId == playerId && u.CardId == cardId)
            .FirstOrDefaultAsync();
    }

    public Task UpsertUserCardsAsync(IEnumerable<UserCard> userCards)
    {
        return dbCtx.UserCards.UpsertRange(userCards).RunAsync();
    }

    public Task UpsertUserItemsAsync(IEnumerable<UserItem> userItems)
    {
        return dbCtx.UserItems.UpsertRange(userItems).RunAsync();
    }
}