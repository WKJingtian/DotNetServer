using GameOutside.DBContext;
using GameOutside.Models;

namespace GameOutside.Repositories;

public interface IGameRepository
{
    public ValueTask<UserGameInfo?> GetUserGameInfoByIdAsync(short shardId, long playerId);

    public void AddUserGameInfoById(short shardId, long playerId);

    public ValueTask<UserDailyTreasureBoxProgress?> GetDailyTreasureBoxProgressAsync(short shardId, long playerId);
    public UserDailyTreasureBoxProgress AddUserDailyTreasureBoxProgress(short shardId, long playerId);
}

public class GameRepository(BuildingGameDB dbCtx) : IGameRepository
{
    public void AddUserGameInfoById(short shardId, long playerId)
    {
        var userGameInfo = new UserGameInfo()
        {
            ShardId = shardId,
            PlayerId = playerId,
            LastGameEndTime = 0,
            TicketBoxSequence = 0,
            DelayBoxSequence = 0,
            CheatAccumulate = 0,
            DrawCardCountList = new List<int>(),
            DrawNewCardCountList = new List<int>(),
            GeneralCardCountList = new List<int>()
        };
        dbCtx.UserGameInfos.Add(userGameInfo);
    }

    public ValueTask<UserGameInfo?> GetUserGameInfoByIdAsync(short shardId, long playerId)
    {
        return dbCtx.UserGameInfos.FindAsync(playerId, shardId);
    }

    public ValueTask<UserDailyTreasureBoxProgress?> GetDailyTreasureBoxProgressAsync(short shardId, long playerId)
    {
        return dbCtx.UserDailyTreasureBoxProgresses.FindAsync(playerId, shardId);
    }

    public UserDailyTreasureBoxProgress AddUserDailyTreasureBoxProgress(short shardId, long playerId)
    {
        var data = new UserDailyTreasureBoxProgress
        {
            PlayerId = playerId,
            ShardId = shardId,
            Progress = 0,
            RewardClaimStatus = 0,
            Timestamp = TimeUtils.GetCurrentTime()
        };
        dbCtx.UserDailyTreasureBoxProgresses.Add(data);
        return data;
    }
}