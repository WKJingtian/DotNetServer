using GameExternal;

namespace GameOutside.Services;

public class AntiCheatService(ServerConfigService serverConfigService)
{
    /// 认定为作弊的上报击杀数
    private const int _cheatingKillCount = 9999999;

    /// 认定为作弊的上报无尽模式波次数
    private const int _cheatingTrueEndlessWaveCount = 999;

    /// 认定为作弊的上报建筑数量
    private const int _cheatingBuildingCount = 9999;

    /// 认定为作弊的上报科技点数
    private const float _cheatingExpScore = 99999999f;

    /// <summary>
    /// 资源检测，这个比较重要
    /// 先检测资源累计、产量
    /// 再粗略对比一下最终算的分数和实际上传的资源分有没有太大的出入
    /// </summary>
    private void CheckResource(
        long playerId,
        List<GameEndResourceRecord> resourceList,
        List<GameEndResourceScore> scoreList,
        List<string> cheatingResult)
    {
        var playerScore = scoreList.FirstOrDefault(score => score.PlayerId == playerId)?.Score;
        // 按道理不会查不到
        if (!playerScore.HasValue)
            return;
        int calculatedScore = 0;
        foreach (var resource in resourceList)
        {
            var config = serverConfigService.GetResourceConfigByKey(resource.Key);
            if (config == null)
                continue;
            calculatedScore += resource.AccuCount * config.score;
            // 检测产量
            if (config.cheating_production > 0 && resource.Production >= config.cheating_production)
            {
                cheatingResult.Add(
                    $"resource {resource.Key} production >= {config.cheating_production}, now is {resource.Production}");
            }

            // 检测累计值
            if (config.cheating_accu > 0 && resource.AccuCount >= config.cheating_accu)
            {
                cheatingResult.Add(
                    $"resource {resource.Key} accu >= {config.cheating_accu}, now is {resource.AccuCount}");
            }
        }

        // 对比一下计算值和上报值
        // 允许有一点点误差
        if (Math.Abs(calculatedScore - playerScore.Value) > 1000)
        {
            cheatingResult.Add($"calculated score: {calculatedScore} not match uploaded score: {playerScore}");
        }
    }

    private void CheckBuildingCount(long playerId, List<GameEndBuildingInfo> buildingList, List<string> cheatingResult)
    {
        foreach (var building in buildingList)
        {
            if (building.PlayerId == playerId && building.Count >= _cheatingBuildingCount)
            {
                cheatingResult.Add(
                    $"buildingId: {building.Id} count >= {_cheatingBuildingCount}, now is {building.Count}");
            }
        }
    }


    // 设定击杀数上限
    private void CheckKillCount(int killCount, List<string> cheatingResult)
    {
        if (killCount >= _cheatingKillCount)
        {
            cheatingResult.Add($"kill count > {killCount}, now is : {killCount}");
        }
    }

    // 无尽模式波次
    private void CheckTrueEndlessWave(int waveCount, List<string> cheatingResult)
    {
        if (waveCount > _cheatingTrueEndlessWaveCount)
        {
            cheatingResult.Add($"true endless wave Count >= {_cheatingTrueEndlessWaveCount}, current is {waveCount}");
        }
    }

    private void CheckExpScore(long playerId, List<GameEndExpScore> expScoreList, List<string> cheatingResult)
    {
        foreach (var expScoreInfo in expScoreList)
        {
            if (expScoreInfo.PlayerId == playerId && expScoreInfo.Exp > _cheatingExpScore)
            {
                cheatingResult.Add($"exp score >= {_cheatingExpScore}, now is {expScoreInfo.Exp}");
                break;
            }
        }
    }

    // 检查建筑、资源、击杀数、经验值
    public List<string> CheckCheating(long playerId, NormalGameEndMessage gameEndMessage)
    {
        List<string> cheatingResult = new List<string>();
        CheckBuildingCount(playerId, gameEndMessage.BuildingList, cheatingResult);
        CheckResource(playerId, gameEndMessage.ResourceList, gameEndMessage.ResourceScoreList, cheatingResult);
        CheckKillCount(gameEndMessage.KillCount, cheatingResult);
        CheckHackWin(gameEndMessage, cheatingResult);
        CheckExpScore(playerId, gameEndMessage.ExpScoreList, cheatingResult);
        return cheatingResult;
    }

    private void CheckHackWin(NormalGameEndMessage gameEndMessage, List<string> cheatingResult)
    {
        if (gameEndMessage.Win)
        {
            if (gameEndMessage.KillCount <= 500)
                cheatingResult.Add("win but kill count = 500?");
            if (gameEndMessage.GameTime < 180)
                cheatingResult.Add("win but game time < 180?");
        }
    }

    // 检查击杀数，影响最终奖励
    public List<string> CheckCheating(EndlessGameEndMessage gameEndMessage)
    {
        List<string> cheatingResult = new List<string>();
        CheckKillCount(gameEndMessage.KillCount, cheatingResult);
        return cheatingResult;
    }

    public List<string> CheckCheating(SurvivorGameEndMessage gameEndMessage)
    {
        List<string> cheatingResult = new List<string>();
        CheckKillCount(gameEndMessage.KillCount, cheatingResult);
        return cheatingResult;
    }

    public List<string> CheckCheating(TowerDefenceGameEndMessage gameEndMessage)
    {
        List<string> cheatingResult = new List<string>();
        CheckKillCount(gameEndMessage.KillCount, cheatingResult);
        return cheatingResult;
    }

    public List<string> CheckCheating(TrueEndlessGameEndMessage gameEndMessage)
    {
        List<string> cheatingResult = new List<string>();
        CheckTrueEndlessWave(gameEndMessage.MonsterWaveCount, cheatingResult);
        CheckKillCount(gameEndMessage.KillCount, cheatingResult);
        return cheatingResult;
    }

    public void CheckDifficultyCheating(GameOutside.Models.UserAssets userAssets, NormalGameEndMessage gameEndMessage, List<string> cheatingResult)
    {
        if (gameEndMessage.MapType == GameMapType.OnlineMap)
            return;
        if (gameEndMessage.Difficulty > 0)
        {
            int maxUnlockedDifficulty = 0;
            for (int i = 0; i <= userAssets.LevelData.Level; i++)
            {
                var config = serverConfigService.GetUserLevelConfig(i);
                if (config != null)
                {
                    maxUnlockedDifficulty = Math.Max(maxUnlockedDifficulty, config.unlock_difficulty);
                }
            }

            if (gameEndMessage.Difficulty > maxUnlockedDifficulty)
            {
                cheatingResult.Add($"Difficulty {gameEndMessage.Difficulty} > maxUnlockedDifficulty {maxUnlockedDifficulty}");
            }
        }

        int currentMaxLevel = 0;
        if (gameEndMessage.Difficulty >= 0 && gameEndMessage.Difficulty < userAssets.DifficultyData.Levels.Count)
        {
            currentMaxLevel = userAssets.DifficultyData.Levels[gameEndMessage.Difficulty];
        }

        if (gameEndMessage.DifficultyLevel > currentMaxLevel)
        {
            cheatingResult.Add($"DifficultyLevel {gameEndMessage.DifficultyLevel} > currentMaxLevel {currentMaxLevel} for Difficulty {gameEndMessage.Difficulty}");
        }
    }

    // 这个不上榜，也没啥检查的
    public List<string> CheckCheating(CoopBossGameEndMessage gameEndMessage)
    {
        return new List<string>();
    }
}