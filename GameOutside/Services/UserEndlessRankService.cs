using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;

namespace GameOutside.Services;

public class UserEndlessRankService(
    BuildingGameDB dbCtx,
    IUserEndlessRankRepository userEndlessRankRepository,
    ISeasonInfoRepository seasonInfoRepository,
    LeaderboardModule leaderboardModule)
{
    /// <summary>
    /// 根据 ShardId 和 PID 获取玩家无尽模式排名信息
    /// </summary>
    public ValueTask<UserEndlessRank?> GetCurrentSeasonUserEndlessRankAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => userEndlessRankRepository.GetUserEndlessRankAsync(shardId, playerId));
    }

    public UserEndlessRank CreateUserEndlessRank(short shardId, long playerId, int? seasonNumber = null)
    {
        return userEndlessRankRepository.CreateEndlessRank(shardId, playerId, seasonNumber);
    }

    /// <summary>
    /// 上传无尽模式分数
    /// </summary>
    public async ValueTask UploadEndlessScoreAsync(short shardId, long playerId, long score, long timestamp, string gameMode)
    {
        var userEndlessRank = await GetCurrentSeasonUserEndlessRankAsync(shardId, playerId) ??
                              CreateUserEndlessRank(shardId, playerId);
        // 检查是否需要更新分数
        bool shouldUpdate = false;
        int oldScore = 0;
        string leaderboardId = "";

        switch (gameMode.ToLower())
        {
            case "survivor":
                if (userEndlessRank.SurvivorScore < score)
                {
                    oldScore = (int)userEndlessRank.SurvivorScore;
                    userEndlessRank.SurvivorScore = score;
                    userEndlessRank.SurvivorTimestamp = timestamp;
                    leaderboardId = LeaderboardModule.SurvivorModeLeaderBoardId;
                    shouldUpdate = true;
                }
                break;
            case "towerdefence":
                if (userEndlessRank.TowerDefenceScore < score)
                {
                    oldScore = (int)userEndlessRank.TowerDefenceScore;
                    userEndlessRank.TowerDefenceScore = score;
                    userEndlessRank.TowerDefenceTimestamp = timestamp;
                    leaderboardId = LeaderboardModule.TowerDefenceModeLeaderBoardId;
                    shouldUpdate = true;
                }
                break;
            case "trueendless":
                if (userEndlessRank.TrueEndlessScore < score)
                {
                    oldScore = (int)userEndlessRank.TrueEndlessScore;
                    userEndlessRank.TrueEndlessScore = score;
                    userEndlessRank.TrueEndlessTimestamp = timestamp;
                    leaderboardId = LeaderboardModule.TrueEndlessModeLeaderBoardId;
                    shouldUpdate = true;
                }
                break;
        }

        if (shouldUpdate)
        {
            var seasonNumber = seasonInfoRepository.GetCurrentSeasonNumber();
            await leaderboardModule.UpdateScore(playerId, score, oldScore, timestamp, leaderboardId, seasonNumber);
        }
    }

    public ValueTask<bool> ClearUserEndlessRankAsync(short shardId, IEnumerable<int> seasonNumbersToBeKept)
    {
        return dbCtx.WithDefaultRetry(_ => userEndlessRankRepository.ClearUserEndlessRanksAsync(shardId, seasonNumbersToBeKept));
    }
}
