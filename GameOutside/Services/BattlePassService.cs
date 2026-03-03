using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;

namespace GameOutside.Services;

public class BattlePassService(
    IBattlePassRepository battlePassRepository,
    ServerConfigService serverConfigService,
    BuildingGameDB dbCtx)
{
    public ValueTask<List<UserBattlePassInfo>> GetAllUserBattlePassInfoAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ => battlePassRepository.GetAllUserBattlePassInfoAsync(shardId, playerId));
    }

    public ValueTask<UserBattlePassInfo?> GetUserBattlePassInfoByPassIdAsync(short? shardId, long playerId, int passId)
    {
        return dbCtx.WithDefaultRetry(_ =>
            battlePassRepository.GetUserBattlePassInfoByPassIdAsync(shardId, playerId, passId));
    }

    public UserBattlePassInfo AddUserBattlePassInfo(short shardId, long playerId, int passId)
    {
        if (!serverConfigService.TryGetParameterInt(Params.MaxSuperPassLevel, out var maxLevel))
            throw new Exception("Param.MaxSuperPassLevel Not Found");
        var newBattlePassInfo = battlePassRepository.CreateNewUserBattlePassInfo(shardId, playerId, passId, maxLevel);
        battlePassRepository.AddUserBattlePassInfo(newBattlePassInfo);
        return newBattlePassInfo;
    }

    public int CalculateGameBattleExp(List<ScoreUnit> scoreList, bool useExpRule, int currentSeason)
    {
        var battleExpTotal = 0;
        foreach (var scoreUnit in scoreList)
        {
            var battleExpRulesConfig = serverConfigService.GetBattleRulesConfigByKey(scoreUnit.Key, currentSeason);
            if (battleExpRulesConfig == null)
                continue;
            var battleExp = (int)(scoreUnit.Score / battleExpRulesConfig.ratio);
            if (useExpRule && battleExp > battleExpRulesConfig.max)
            {
                battleExp = battleExpRulesConfig.max;
            }

            battleExpTotal += battleExp;
        }

        return battleExpTotal;
    }

    public int CalculateGameBattleExpByActivityGame(int killCount)
    {
        if (!serverConfigService.TryGetParameterFloat(Params.ActivityGameBattlePassExpGainBase,
                out var battlePassExpGainBase) ||
            !serverConfigService.TryGetParameterFloat(Params.ActivityGameBattlePassExpGainMult,
                out var battlePassExpGainMult) ||
            !serverConfigService.TryGetParameterInt(Params.ActivityGameBattlePassExpGainMax,
                out var battlePassExpGainMax))
            return 0;
        return Math.Min(battlePassExpGainMax,
            (int)((1.0f - 1.0f / (Math.Pow(Math.E, killCount / battlePassExpGainBase))) * battlePassExpGainMult));
    }

    public async Task<int> AddBattlePassExp(long playerId, short shardId, int expAdd)
    {
        if (expAdd <= 0)
            return (int)ErrorKind.SUCCESS;
        var firstPassInfo = await GetUserBattlePassInfoByPassIdAsync(shardId, playerId, -1);
        firstPassInfo ??= AddUserBattlePassInfo(shardId, playerId, -1);
        firstPassInfo.Exp += expAdd;

        var currentBattlePassId = serverConfigService.GetActiveBattlePassId();
        if (currentBattlePassId == -1)
            return (int)ErrorKind.NO_BATTLE_PASS_CONFIG;
        var battlePassInfo =
            await GetUserBattlePassInfoByPassIdAsync(shardId, playerId, currentBattlePassId);
        battlePassInfo ??= AddUserBattlePassInfo(shardId, playerId, currentBattlePassId);
        battlePassInfo.Exp += expAdd;

        return (int)ErrorKind.SUCCESS;
    }
}