using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;

namespace GameOutside.Services;

public class UserCardService(
    BuildingGameDB dbCtx,
    IUserCardRepository userCardRepository)
{
    /// <summary>
    /// 获取玩家卡牌列表
    /// </summary>
    public ValueTask<List<UserCard>> GetReadonlyUserCardsAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => userCardRepository.GetUserCardsAsync(shardId, playerId, TrackingOptions.NoTracking));
    }

    public ValueTask<List<UserCard>> GetReadonlyUserCardsByCardIdsAsync(
        short shardId,
        long playerId,
        IEnumerable<int> cardIds)
    {
        return dbCtx.WithDefaultRetry(_ => userCardRepository.GetUserCardsByCardIdsAsync(
            shardId,
            playerId,
            cardIds,
            TrackingOptions.NoTracking));
    }

    public Task UpsertUserCardsAsync(IEnumerable<UserCard> userCards)
    {
        return userCardRepository.UpsertUserCardsAsync(userCards);
    }
}