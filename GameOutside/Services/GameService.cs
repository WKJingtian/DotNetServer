using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Util;
using ChillyRoom.BuildingGame.Models;
using GameExternal;
using MessagePack;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GenericPlayerManagementService.Client;
using ChillyRoom.GenericPlayerService.v1.Management;
using System.Text.Json;
using GameOutside.Services.KafkaConsumers;

namespace GameOutside.Services;

public record struct ScoreUnit(string Key, float original, float Score, float multipler);

public record struct PlayerScore(long playerId, float score);

public record FixedMapGameEndCustomArgs(List<int> StarTaskIds, List<bool> StartStatus);

public record struct OnGameEndReply(
    List<ScoreUnit> Scores,
    List<PlayerScore> PlayerScoreList,
    GeneralReward GeneralReward,
    List<UserAchievement> AchievementChange,
    UserBeginnerTask? TaskChange,
    long ScoreTotal,
    List<DifficultyChange> DifficultyChanges,
    string CustomArgs);

public record struct MatchedPlayerInfo(
    long PlayerId,
    int DivisionRank,
    int Level,
    List<UserCard> CardList);

public record struct MatchResult(List<MatchedPlayerInfo> PlayerList);

public class GameService(
    ILogger<GameService> logger,
    BuildingGameDB dbCtx,
    ServerConfigService serverConfigService,
    DivisionService divisionService,
    SeasonService seasonService,
    UserRankService userRankService,
    UserItemService userItemService,
    IGameRepository gameRepository,
    BattlePassService battlePassService,
    UserAssetService userAssetService,
    LeaderboardModule leaderboardModule,
    ActivityService activityService,
    PlayerBanner playerBanner)
{
    public T? CheckGameEndMessageValid<T>(string message, string hash, string desKey) where T : class
    {
        string content;
        try
        {
            content = EncryptHelper.EncryptHelper.DesDecrypt(message, desKey);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }

        var contentHash = EncryptHelper.EncryptHelper.CustomHash(content);
        if (contentHash != hash)
            return null;
        var bytes = Convert.FromBase64String(content);
        var messageObj = MessagePackSerializer.Deserialize<GameEndMessageBase>(bytes);
        return messageObj as T;
    }

    public bool IsMessageObjValid(UserGameInfo userGameInfo, GameEndMessageBase gameEndMessage)
    {
        // 重发验证
        var currentTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long lastGameEndTime = userGameInfo.LastGameEndTime;
        userGameInfo.LastGameEndTime = currentTimeStamp;

        if (currentTimeStamp - lastGameEndTime < 5)
            return false;
        // 过期验证
        if (currentTimeStamp - gameEndMessage.TimeStamp > 5)
            return false;
        return true;
    }

    public (List<ScoreUnit>, List<PlayerScore>, long scoreLong, int error) CalculateNormalAndFixGameScore(
        NormalGameEndMessage gameEndMessage, bool fixedMap, long playerId, int currentSeasonNo)
    {
        var difficulty = gameEndMessage.Difficulty;
        var difficultyLevel = gameEndMessage.DifficultyLevel;
        
        // 难度
        var difficultyFactor = serverConfigService.GetGameMultiByDifficulty(difficulty, difficultyLevel);
        if (difficultyFactor < 0)
            return (null, null, 0, (int)ErrorKind.NO_DIFFICULTY_CONFIG)!;
        
        // 分数计算, 临时的，瞎写凑个数
        var scoreList = new List<ScoreUnit>();
        // 杀敌
        var killScore = CalculateScoreWithRule("kill", gameEndMessage.KillCount, difficultyFactor, currentSeasonNo);
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, killScore, 1));

        // 玩家贡献排名
        // 时长
        var timeScore = CalculateScoreWithRule("time", gameEndMessage.GameTime, difficultyFactor, currentSeasonNo);
        scoreList.Add(new ScoreUnit("time", gameEndMessage.GameTime, timeScore, 1));
        // 开图率
        var mapScore = CalculateScoreWithRule("map", gameEndMessage.MapUnlockRatio, difficultyFactor, currentSeasonNo);
        scoreList.Add(new ScoreUnit("map", gameEndMessage.MapUnlockRatio, mapScore, 1));

        // 玩家自己的分数，用来计算贡献度排名
        Dictionary<long, float> playerScoreDictionary = new();
        int playerCount = gameEndMessage.ResourceScoreList.Count;
        float totalBuildingScore = 0f;
        // 建筑分数
        foreach (var building in gameEndMessage.BuildingList)
        {
            int buildingId = building.Id;
            var config = serverConfigService.GetBuildingScoreConfig(buildingId, currentSeasonNo);
            if (config == null)
            {
                logger.LogCritical("no building score config : id : {BuildingId}", buildingId);
                continue;
            }

            int score = config.damage_score * building.DamageCount + config.score * building.Count;
            totalBuildingScore += score;
            // 跳过共有的建筑
            if (building.PlayerId != 0)
            {
                playerScoreDictionary.TryAdd(building.PlayerId, 0);
                playerScoreDictionary[building.PlayerId] += score;
            }
        }

        totalBuildingScore /= playerCount;

        float totalResourceScore = 0f;
        // 资源分数
        foreach (var resource in gameEndMessage.ResourceScoreList)
        {
            playerScoreDictionary.TryAdd(resource.PlayerId, 0);
            playerScoreDictionary[resource.PlayerId] += resource.Score * 0.5f;
            totalResourceScore += resource.Score * 0.5f;
        }

        float totalExpScore = 0f;
        // 经验分数
        foreach (var expScore in gameEndMessage.ExpScoreList)
        {
            playerScoreDictionary.TryAdd(expScore.PlayerId, 0);
            playerScoreDictionary[expScore.PlayerId] += expScore.Exp * 1.2f;
            totalExpScore += expScore.Exp * 1.2f;
        }

        totalResourceScore /= playerCount;
        totalExpScore /= playerCount;

        var flatList = playerScoreDictionary.ToList();
        flatList.Sort((l, r) => r.Value.CompareTo(l.Value));
        List<long> rankList = flatList.Select(element => element.Key).ToList();

        totalBuildingScore = CalculateScoreWithRule("building", totalBuildingScore, difficultyFactor, currentSeasonNo);
        totalResourceScore = CalculateScoreWithRule("resource", totalResourceScore, difficultyFactor, currentSeasonNo);
        totalExpScore = CalculateScoreWithRule("exp", totalExpScore, difficultyFactor, currentSeasonNo);
        scoreList.Add(new ScoreUnit("building", -1, totalBuildingScore, 1));
        // 资源分数
        scoreList.Add(new ScoreUnit("resource", -1, totalResourceScore, 1));
        // 经验分数
        scoreList.Add(new ScoreUnit("exp", -1, totalExpScore, 1));

        // 跳过时间
        if (gameEndMessage.SkippedTimes > 0)
        {
            var exponent = Math.Min(0.5f, gameEndMessage.SkippedTimes / (gameEndMessage.LastWaveTime + 45));
            var skipTimeFactor = (float)Math.Pow(50, exponent);
            scoreList.Add(new ScoreUnit("skip_time", -1, 0, skipTimeFactor));
        }

        // 难度自定义选项
        var selectionList = serverConfigService.GetDifficultySelectionConfigListByDifficulty(difficulty);
        foreach (var selectionConfig in selectionList)
        {
            scoreList.Add(new ScoreUnit($"selection_{selectionConfig.id}", -1, 0, selectionConfig.score_mult));
        }

        // 战役模式的话直接关卡算加成
        if (fixedMap)
        {
            var fixedMapConfig = serverConfigService.GetFixedMapConfig(gameEndMessage.MapId);
            if (fixedMapConfig == null)
                return (null, null, 0, (int)ErrorKind.NO_MAP_CONFIG)!;
            scoreList.Add(new ScoreUnit("difficulty", -1, 0, fixedMapConfig.score_mult));
        }
        else
            scoreList.Add(new ScoreUnit("difficulty", -1, 0, difficultyFactor));

        // 胜利
        scoreList.Add(new ScoreUnit("win", -1, 0, gameEndMessage.Win ? 3 : 1));

        // 复活
        scoreList.Add(new ScoreUnit("reborn", -1, 0, gameEndMessage.Reborn ? 0.7f : 1));

        // 总分
        var totalScore = CalculateTotalScore(scoreList);

        var selfTotalScore = totalScore;
        // 贡献度
        if (rankList.Count > 1)
        {
            var rank = rankList.IndexOf(playerId);
            var contribute = serverConfigService.GetContributeFactorByRank(rank);
            scoreList.Add(new ScoreUnit("contribute", -1, 0, contribute));
            selfTotalScore *= contribute;
        }

        var playerScoreList = new List<PlayerScore>();
        for (int i = 0; i < rankList.Count; i++)
        {
            var contribute = serverConfigService.GetContributeFactorByRank(i);
            playerScoreList.Add(new PlayerScore(rankList[i], totalScore * contribute));
        }

        var scoreLong = (long)Math.Ceiling(selfTotalScore);
        return (scoreList, playerScoreList, scoreLong, (int)ErrorKind.SUCCESS);
    }

    /// <summary>
    /// 通用的游戏结束处理逻辑 - 战令经验、存钱罐、boss活动、分数上传等
    /// </summary>
    public async Task<(int, int)> ProcessCommonGameEndLogicWithScore(int battleExpTotal, bool isWin,
        long scoreLong, long timestamp, long playerId, short playerShard, string gameVersion, string? leaderboardId = null)
    {
        // 发放战令经验
        var error = await battlePassService.AddBattlePassExp(playerId, playerShard, battleExpTotal);
        if (error != (int)ErrorKind.SUCCESS)
            return (0, error);

        // 发放存钱罐经验
        if (isWin)
        {
            await activityService.AddPiggyBankExpAsync(playerId, playerShard, 1);
        }

        // 检查共斗boss隐藏关卡
        var coopBossTimeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityCoopBoss, gameVersion);
        error = await activityService.TryOpenBossActivityAsync(playerId, playerShard, coopBossTimeConfig);
        if (error != (int)ErrorKind.SUCCESS)
            return (0, error);

        // 上传分数（如果需要）
        if (!string.IsNullOrEmpty(leaderboardId))
        {
            var (division, uploadError) = await ProcessScoreUpload(scoreLong, timestamp, isWin, playerId, playerShard, leaderboardId);
            if (uploadError != (int)ErrorKind.SUCCESS)
                return (0, uploadError);
            return (division, (int)ErrorKind.SUCCESS);
        }

        return (0, (int)ErrorKind.SUCCESS);
    }

    /// <summary>
    /// 通用的游戏结束处理逻辑 - 战令经验、存钱罐、boss活动等
    /// </summary>
    public async Task<int> ProcessCommonGameEndLogic(long playerId, short playerShard, string gameVersion, int battleExpTotal, bool isWin)
    {
        // 发放战令经验
        var error = await battlePassService.AddBattlePassExp(playerId, playerShard, battleExpTotal);
        if (error != (int)ErrorKind.SUCCESS)
            return error;

        // 发放存钱罐经验
        if (isWin)
        {
            await activityService.AddPiggyBankExpAsync(playerId, playerShard, 1);
        }

        // 检查共斗boss隐藏关卡
        var coopBossTimeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityCoopBoss, gameVersion);
        error = await activityService.TryOpenBossActivityAsync(playerId, playerShard, coopBossTimeConfig);
        if (error != (int)ErrorKind.SUCCESS)
            return error;

        return (int)ErrorKind.SUCCESS;
    }

    /// <summary>
    /// 分数上传
    /// </summary>
    public async Task<(int, int)> ProcessScoreUpload(long scoreLong, long timestamp, bool isWin,
        long playerId, short playerShard, string? leaderboardId = null)
    {
        var division
            = await divisionService.GetDivisionNumberAsync(playerShard, playerId, CreateOptions.CreateWhenNotExists);
        // 需要兼容青铜段位的情况
        int seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
        var userRank = await userRankService.GetCurrentSeasonUserRankByDivisionAsync(playerShard, playerId, division) ??
                       await userRankService.CreateUserRankAsync(playerShard, playerId, division, 0, timestamp, isWin,
                           seasonNumber);

        if (userRank.HighestScore < scoreLong)
        {
            var oldScore = userRank.HighestScore;
            userRank.HighestScore = scoreLong;
            userRank.Win = isWin;
            userRank.Timestamp = timestamp;
            // 上传到排行榜
            if (!string.IsNullOrEmpty(leaderboardId))
            {
                await leaderboardModule.UpdateScore(playerId, scoreLong, oldScore, timestamp, leaderboardId, seasonNumber);
            }
        }

        return (division, (int)ErrorKind.SUCCESS);
    }

    /// <summary>
    /// 根据规则计算分数，并应用最大值限制
    /// </summary>
    public float CalculateScoreWithRule(string ruleKey, double baseCount, double difficultyMultiplier, int currentSeason)
    {
        var rule = serverConfigService.GetBattleRulesConfigByKey(ruleKey, currentSeason);
        return rule != null ? MathF.Min(rule.max_score, (float)rule.eval_lambda(baseCount, difficultyMultiplier)) : (float)baseCount;
    }

    /// <summary>
    /// 计算基础游戏分数 - 包含杀敌、难度、胜利、复活等通用要素
    /// </summary>
    public (List<ScoreUnit>, int) CalculateBasicGameScore(int killCount, bool win, bool reborn,
        string difficultyParamKey, int difficultyLevel)
    {
        var scoreList = new List<ScoreUnit>();
        scoreList.Add(new ScoreUnit("kill", killCount, killCount, 1));

        var currentSeason = seasonService.GetCurrentSeasonNumber();
        var difficultyFactor = MathF.Pow((difficultyLevel + 1) * 0.4f + 0.7f, 2.5f);

        scoreList.Add(new ScoreUnit("difficulty", -1, 0, difficultyFactor));
        scoreList.Add(new ScoreUnit("win", -1, 0, win ? 3 : 1));
        scoreList.Add(new ScoreUnit("reborn", -1, 0, reborn ? 0.7f : 1));

        return (scoreList, (int)ErrorKind.SUCCESS);
    }

    /// <summary>
    /// 计算分数总和
    /// </summary>
    public float CalculateTotalScore(List<ScoreUnit> scoreList)
    {
        var totalScore = 0f;
        foreach (var score in scoreList)
        {
            totalScore += score.Score;
            totalScore *= score.multipler;
        }
        return totalScore;
    }

    public (List<ScoreUnit>, List<PlayerScore>, long) CalculateEndlessGameScore(
        EndlessGameEndMessage gameEndMessage, long playerId)
    {
        var scoreList = new List<ScoreUnit>();
        var config = serverConfigService.GetEndlessDifficultyConfig(gameEndMessage.Difficulty);
        float scoreMult = config?.score_mult ?? 1;
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, gameEndMessage.KillCount, scoreMult));

        var totalScore = CalculateTotalScore(scoreList);
        long scoreLong = (long)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new() { playerId = playerId, score = scoreLong } };
        return (scoreList, playerScoreList, scoreLong);
    }

    public (List<ScoreUnit>, List<PlayerScore>, long, int) CalculateSurvivorGameScore(
        SurvivorGameEndMessage gameEndMessage, long playerId)
    {
        var (scoreList, error) = CalculateBasicGameScore(gameEndMessage.KillCount, gameEndMessage.Win,
            gameEndMessage.Reborn, Params.SurvivorGameScoreMultiplier, gameEndMessage.DifficultyLevel);
        if (error != (int)ErrorKind.SUCCESS)
            return (null, null, 0, error)!;

        var totalScore = CalculateTotalScore(scoreList);
        long scoreLong = (long)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = scoreLong } };
        return (scoreList, playerScoreList, scoreLong, (int)ErrorKind.SUCCESS);
    }

    public (List<ScoreUnit>, List<PlayerScore>, long, int) CalculateTowerDefenceGameScore(
        TowerDefenceGameEndMessage gameEndMessage, long playerId)
    {
        var (scoreList, error) = CalculateBasicGameScore(gameEndMessage.KillCount, gameEndMessage.Win,
            gameEndMessage.Reborn, Params.TowerDefenceGameScoreMultiplier, gameEndMessage.DifficultyLevel);
        if (error != (int)ErrorKind.SUCCESS)
            return (null, null, 0, error)!;

        var totalScore = CalculateTotalScore(scoreList);
        var scoreLong = (long)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = scoreLong } };
        return (scoreList, playerScoreList, scoreLong, (int)ErrorKind.SUCCESS);
    }

    public (List<ScoreUnit>, List<PlayerScore>, long, int) CalculateTrueEndlessGameScore(
        TrueEndlessGameEndMessage gameEndMessage, long playerId)
    {
        var scoreList = new List<ScoreUnit>();
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, gameEndMessage.KillCount, 1));
        // MonsterWaveCount是从-1开始的
        var waveCntFactor = Math.Max(1, gameEndMessage.MonsterWaveCount) * 0.05f;
        scoreList.Add(new ScoreUnit("wave", -1, 0, waveCntFactor));
        // 跳过时间
        if (gameEndMessage.SkippedTimes > 0)
        {
            var exponent = Math.Min(0.5f,
                gameEndMessage.SkippedTimes / (120 * (gameEndMessage.MonsterWaveCount + 1) + 45));
            var skipTimeFactor = (float)Math.Pow(50, exponent);
            if (skipTimeFactor < 1)
                skipTimeFactor = 1;
            scoreList.Add(new ScoreUnit("skip_time", -1, 0, skipTimeFactor));
        }
        // 复活
        scoreList.Add(new ScoreUnit("reborn", -1, 0, gameEndMessage.Reborn ? 0.7f : 1));

        var totalScore = CalculateTotalScore(scoreList);
        var scoreLong = (long)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = scoreLong } };
        return (scoreList, playerScoreList, scoreLong, (int)ErrorKind.SUCCESS);
    }

    public (List<ScoreUnit>, List<PlayerScore>, int, int) CalculateCoopBossGameScore(
        CoopBossGameEndMessage gameEndMessage, long playerId)
    {
        var scoreList = new List<ScoreUnit>();
        var totalDamage = 0L;
        var selfDamage = 0L;
        foreach (var fightUnit in gameEndMessage.FightUnitList)
        {
            totalDamage += fightUnit.TotalDamage;
            if (fightUnit.PlayerId == playerId)
                selfDamage += fightUnit.TotalDamage;
        }

        var ratio = 0f;
        if (totalDamage > 0)
            ratio = selfDamage / (float)totalDamage;
        scoreList.Add(new ScoreUnit("enemy_hurt_percentage", ratio, selfDamage, 1));
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = selfDamage } };
        return (scoreList, playerScoreList, (int)MathF.Round(ratio * 1000), 0);
    }

    public (List<ScoreUnit>, List<PlayerScore>, int, int) CalculateRpgGameScore(
        RpgGameEndMessage gameEndMessage, long playerId, float scoreMultiplierByLevel)
    {
        var scoreList = new List<ScoreUnit>();
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, gameEndMessage.KillCount, 1));
        scoreList.Add(new ScoreUnit("difficulty", -1, 0, scoreMultiplierByLevel));
        scoreList.Add(new ScoreUnit("win", -1, 0, gameEndMessage.Win ? 3 : 1));

        var totalScore = CalculateTotalScore(scoreList);
        var score = (int)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = score } };
        return (scoreList, playerScoreList, score, (int)ErrorKind.SUCCESS);
    }

    public (List<ScoreUnit>, List<PlayerScore>, int, int) CalculateLoogGameScore(
        LoogGameEndMessage gameEndMessage, long playerId, float scoreMultiplierByLevel)
    {
        var scoreList = new List<ScoreUnit>();
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, gameEndMessage.KillCount, 1));
        scoreList.Add(new ScoreUnit("difficulty", -1, 0, scoreMultiplierByLevel));
        scoreList.Add(new ScoreUnit("win", -1, 0, gameEndMessage.Win ? 3 : 1));

        var totalScore = CalculateTotalScore(scoreList);
        var score = (int)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = score } };
        return (scoreList, playerScoreList, score, (int)ErrorKind.SUCCESS);
    }

    public (List<ScoreUnit>, List<PlayerScore>, long, int) CalculateTreasureMazeGameScore(
        TreasureMazeGameEndMessage gameEndMessage, long playerId)
    {
        var scoreList = new List<ScoreUnit>();
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, gameEndMessage.KillCount, 1));
        var difficultyConfig = serverConfigService.GetTreasureMazeDifficultyConfig(gameEndMessage.ActivityId, gameEndMessage.DifficultyLevel);
        scoreList.Add(new ScoreUnit("difficulty", -1, 0, difficultyConfig!.score_multiplier));
        scoreList.Add(new ScoreUnit("win", -1, 0, gameEndMessage.Win ? 3 : 1));

        var totalScore = CalculateTotalScore(scoreList);
        var scoreLong = (long)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = scoreLong } };
        return (scoreList, playerScoreList, scoreLong, (int)ErrorKind.SUCCESS);
    }

    public (List<ScoreUnit>, List<PlayerScore>, long, int) CalculateOneShotKillGameScore(
        OneShotKillGameEndMessage gameEndMessage, long playerId)
    {
        var scoreList = new List<ScoreUnit>();
        scoreList.Add(new ScoreUnit("kill", gameEndMessage.KillCount, gameEndMessage.KillCount, 1));
        var difficultyConfigList = serverConfigService.GetOneShotKillMapConfig(gameEndMessage.ActivityId, gameEndMessage.DifficultyLevel);
        scoreList.Add(new ScoreUnit("difficulty", -1, 0, difficultyConfigList == null ? 1 : difficultyConfigList.score_multiplier));
        scoreList.Add(new ScoreUnit("win", -1, 0, gameEndMessage.Win ? 3 : 1));

        var totalScore = CalculateTotalScore(scoreList);
        long scoreLong = (long)Math.Ceiling(totalScore);
        var playerScoreList = new List<PlayerScore>() { new PlayerScore() { playerId = playerId, score = scoreLong } };
        return (scoreList, playerScoreList, scoreLong, (int)ErrorKind.SUCCESS);
    }

    // 1.8/10:00 GMT+8 赛季竞技场增加每日宝箱奖励上限
    private static readonly DateTime s_minDateForDailyChestLimitUpdate
        = new DateTime(2026, 1, 8, 2, 0, 0, DateTimeKind.Utc);

    public async Task<(GeneralReward, int)> CalculateNormalGameRewards(
        NormalGameEndMessage gameEndMessage,
        long playerId,
        short shardId,
        int division,
        string gameVersion,
        long timeZoneOffset)
    {
        var userGameInfo = await GetUserGameInfoByIdAsync(shardId, playerId);
        if (userGameInfo == null)
            return (null, (int)ErrorKind.NO_USER_ASSET)!;
        var difficulty = gameEndMessage.Difficulty;
        var difficultyLevel = gameEndMessage.DifficultyLevel;
        var mapType = gameEndMessage.MapType;

        GeneralReward finalReward = new GeneralReward();
        if (mapType == GameMapType.FreeMap || mapType == GameMapType.OnlineMap)
        {
            float gameTime = gameEndMessage.GameTime;
            // 根据成绩发放战斗奖励
            var rewardConfig = serverConfigService.GetScoreRewardConfigByTime((int)MathF.Ceiling(gameTime), gameVersion);
            if (rewardConfig == null)
                return (null, (int)ErrorKind.WRONG_GAME_SCORE)!;

            finalReward.AddRewards(rewardConfig.item_list, rewardConfig.count_list);
            bool isFirstWin = gameEndMessage.Win && difficulty == 0 && difficultyLevel == 0 &&
                              userGameInfo.DelayBoxSequence == 0;

            if (isFirstWin)
            {
                // 首次游戏给个新手计时宝箱
                if (serverConfigService.TryGetParameterInt(Params.FirstGameTreasureBoxId, out var boxId))
                {
                    if (boxId != -1)
                    {
                        finalReward.AddReward(boxId, 1);
                    }
                }
            }

            bool haveDelayBox = false;
            if (gameEndMessage.Win)
            {
                var winReward = serverConfigService.GetWinRewardConfigByDiff(difficulty);
                if (winReward == null)
                    return (null, (int)ErrorKind.NO_WIN_REWARD_CONFIG)!;
                finalReward.AddRewards(winReward.item_list, winReward.count_list);

                var winnerBoxId = winReward.box_id;
                if (winnerBoxId > 0 && !isFirstWin)
                {
                    var boxItemId = -1;
                    // 赢了给个胜利专属宝箱
                    var delayBoxDropConfig
                        = serverConfigService.GetDelayBoxDropConfigBySequence(userGameInfo.DelayBoxSequence++);
                    // 根据版本号区别通关奖励发放逻辑
                    if (gameVersion.CompareVersionStrServer("1.2.0") < 0)
                    {
                        // 选出合适的计时宝箱, 先写死, 跟段位有关
                        boxItemId = 51000 + division * 10 + delayBoxDropConfig.quality;
                    }
                    else
                    {
                        // 选出合适的计时宝箱, 写死, 跟难度有关
                        boxItemId = 57000 + gameEndMessage.Difficulty * 10 + delayBoxDropConfig.quality;
                    }

                    if (delayBoxDropConfig.fixed_box_id > 0)
                        boxItemId = delayBoxDropConfig.fixed_box_id;
                    if (boxItemId > 0)
                    {
                        haveDelayBox = true;
                        finalReward.AddReward(boxItemId, 1);
                    }
                }
            }

            bool haveTreasureBoxReward = finalReward.ItemList.Any(itemId =>
            {
                var config = serverConfigService.GetItemConfigById(itemId);
                return config is { type: ItemType.TreasureBox };
            });

            if (haveTreasureBoxReward)
            {
                // 竞技场计数每日重置
                var currentTimestamp = TimeUtils.GetCurrentTime();
                var diff = TimeUtils.GetDayDiffBetween(currentTimestamp, userGameInfo.LastArenaBoxRewardTime,
                    timeZoneOffset, 0);
                if (diff > 0)
                {
                    userGameInfo.TodayArenaBoxRewardCount = 0;
                }

                if (!serverConfigService.TryGetParameterInt(Params.ArenaBoxRewardDailyMaxCount, out int maxCount))
                {
                    return (finalReward, (int)ErrorKind.NO_PARAM_CONFIG);
                }

                if (userGameInfo.TodayArenaBoxRewardCount >= maxCount)
                {
                    // 增加通关限制，用时间戳来进行版本分化
                    var utcNow = DateTime.UtcNow;
                    if (utcNow >= s_minDateForDailyChestLimitUpdate)
                    {
                        // 超过每日上限，移除竞技场宝箱奖励
                        var newItemList = new List<int>();
                        var newCountList = new List<int>();
                        for (int i = 0; i < finalReward.ItemList.Count; i++)
                        {
                            var itemId = finalReward.ItemList[i];
                            var config = serverConfigService.GetItemConfigById(itemId);
                            if (config is { type: ItemType.TreasureBox })
                                continue;
                            newItemList.Add(itemId);
                            newCountList.Add(finalReward.CountList[i]);
                        }

                        finalReward.ItemList = newItemList;
                        finalReward.CountList = newCountList;

                        // 如果有计时宝箱的话，则回退计数
                        if (haveDelayBox)
                        {
                            userGameInfo.DelayBoxSequence--;
                        }
                    }
                }
                else
                {
                    // 记录
                    userGameInfo.LastArenaBoxRewardTime = currentTimestamp;
                    userGameInfo.TodayArenaBoxRewardCount++;
                }
            }

        }

        return (finalReward, (int)ErrorKind.SUCCESS)!;
    }

    /// <summary>
    /// 计算某类建筑的数量
    /// </summary>
    /// <param name="gameEndMessage">gameEndMessage</param>
    /// <param name="buildingType">特定建筑类型</param>
    /// <param name="match">筛选该建筑还是不筛选该建筑</param>
    /// <returns></returns>
    public IEnumerable<GameEndBuildingInfo> FilterBuildingList(
        NormalGameEndMessage gameEndMessage,
        BuildingConfig.BuildingTypeType buildingType,
        bool match)
    {
        return gameEndMessage.BuildingList.Where(building =>
        {
            var buildingConfig = serverConfigService.GetBuildingConfigById(building.Id);
            if (buildingConfig == null)
                return false;
            return match ? buildingConfig.building_type == buildingType : buildingConfig.building_type != buildingType;
        });
    }

    public (List<(int, bool)>, ErrorKind) CalculateTaskStars(NormalGameEndMessage gameEndMessage,
        FixedMapConfig fixedMapConfig)
    {
        var result = new List<(int, bool)>();
        if (!gameEndMessage.Win)
            return (result, ErrorKind.SUCCESS);
        foreach (var taskId in fixedMapConfig.star_tasks)
        {
            var taskConfig = serverConfigService.GetStarTaskConfig(taskId);
            if (taskConfig == null)
                return (null, ErrorKind.NO_TASK_CONFIG)!;
            switch (taskConfig.key)
            {
                case StarTaskKeys.GameWin:
                {
                    result.Add((taskId, gameEndMessage.Win));
                    break;
                }
                case StarTaskKeys.BuildingListCountBiggerOrEqualNotIncludeWall:
                {
                    var totalBuildingCount =
                        FilterBuildingList(gameEndMessage, BuildingConfig.BuildingTypeType.WALL, false)
                            .Sum(entity => entity.Count + entity.DamageCount);
                    var arg0 = int.Parse(taskConfig.arg_0);
                    result.Add((taskId, totalBuildingCount >= arg0));
                    break;
                }
                case StarTaskKeys.DamagedBuildingLessOrEqualNotIncludeWall:
                {
                    var damagedBuildingCount =
                        FilterBuildingList(gameEndMessage, BuildingConfig.BuildingTypeType.WALL, false)
                            .Sum(entity => entity.DamageCount);
                    var arg0 = int.Parse(taskConfig.arg_0);
                    result.Add((taskId, damagedBuildingCount <= arg0));
                    break;
                }
                case StarTaskKeys.UnlockTileCountBiggerOrEqual:
                {
                    var arg0 = int.Parse(taskConfig.arg_0);
                    result.Add((taskId, gameEndMessage.UnlockTileCount >= arg0));
                    break;
                }
                case StarTaskKeys.TrainCountBiggerOrEqual:
                {
                    var arg0 = int.Parse(taskConfig.arg_0);
                    result.Add((taskId, gameEndMessage.TrainCount >= arg0));
                    break;
                }
                case StarTaskKeys.BuildingTypeCountBiggerOrEqual:
                {
                    var buildingType = (BuildingConfig.BuildingTypeType)int.Parse(taskConfig.arg_0);
                    var arg1 = int.Parse(taskConfig.arg_1);
                    var buildingTypeCount = FilterBuildingList(gameEndMessage, buildingType, true)
                        .Sum(entity => entity.Count + entity.DamageCount);
                    result.Add((taskId, buildingTypeCount >= arg1));
                    break;
                }
                case StarTaskKeys.ResAccuCountBiggerOrEqual:
                {
                    var resKey = taskConfig.arg_0;
                    var arg1 = int.Parse(taskConfig.arg_1);
                    int accuCount = gameEndMessage.ResourceList.FirstOrDefault(res => res.Key == resKey)?.AccuCount ??
                                    0;
                    result.Add((taskId, accuCount >= arg1));
                    break;
                }
                case StarTaskKeys.ResProductionBiggerOrEqual:
                {
                    var resKey = taskConfig.arg_0;
                    var arg1 = int.Parse(taskConfig.arg_1);
                    int production = gameEndMessage.ResourceList.FirstOrDefault(res => res.Key == resKey)?.Production ??
                                     0;
                    result.Add((taskId, production >= arg1));
                    break;
                }
                case StarTaskKeys.HeadquarterHpPercentBiggerOrEqual:
                {
                    int arg0 = int.Parse(taskConfig.arg_0);
                    result.Add((taskId, gameEndMessage.HeadquarterHpPercent >= arg0));
                    break;
                }
                case StarTaskKeys.WinAndFightUnitCountLessEqual:
                {
                    int arg0 = int.Parse(taskConfig.arg_0);
                    result.Add((taskId, gameEndMessage.FightUnitList.Count <= arg0));
                    break;
                }
                case StarTaskKeys.FightUnitTotalDamageBiggerOrEqual:
                {
                    var arg0 = taskConfig.arg_0;
                    long arg1 = long.Parse(taskConfig.arg_1);
                    long totalDamage = gameEndMessage.FightUnitList.FirstOrDefault(fightUnit => fightUnit.Key == arg0)
                        ?.TotalDamage ?? 0;
                    result.Add((taskId, totalDamage >= arg1));
                    break;
                }
                case StarTaskKeys.DamagedBuildingTypeCountLessOrEqual:
                {
                    var buildingType = (BuildingConfig.BuildingTypeType)int.Parse(taskConfig.arg_0);
                    var arg1 = int.Parse(taskConfig.arg_1);
                    var damagedBuildingTypeCount = FilterBuildingList(gameEndMessage, buildingType, true)
                        .Sum(entity => entity.DamageCount);
                    result.Add((taskId, damagedBuildingTypeCount <= arg1));
                    break;
                }
                default: break;
            }
        }

        return (result, ErrorKind.SUCCESS);
    }

    public async Task<(List<UserCard> newCardList, ErrorKind result)> AddGameEndReward(
        GeneralReward generalReward,
        UserAssets userAsset,
        NormalGameEndMessage gameEndMessage,
        long playerId, short shardId,
        string gameVersion)
    {
        // 介入一下活动任务
        await activityService.AddTaskProgressToActiveActivityTask(ActivityTaskKeys.ExplorePoint,
            playerId, shardId, gameEndMessage.ExplorePoint, userAsset.TimeZoneOffset, gameVersion);

        var newCardList = new List<UserCard>();
        if (generalReward.ItemList.Count > 0)
        {
            var result = await userItemService.UnpackItemList(userAsset, generalReward.ItemList,
                generalReward.CountList, gameVersion, null, gameEndMessage.ExplorePoint);
            if (result == null)
                return (newCardList, ErrorKind.NO_ITEM_CONFIG);
            newCardList.AddRange(result.NewCardList);
        }

        // 宝箱奖励需要转化
        if (gameEndMessage.ExplorePoint > 0)
        {
            var exploreEventCount = gameEndMessage.ExplorePoint;
            var maxCount = serverConfigService.GetMaxExploreStarCount();
            int starCount = Math.Clamp(exploreEventCount, 0, maxCount);
            int convertTime = serverConfigService.GetExploreBoxConfigByCount(starCount)!.convert_time;
            for (int i = 0; i < generalReward.ItemList.Count; ++i)
            {
                var itemId = generalReward.ItemList[i];
                int originQuality = itemId % 10;
                var originItemConfig = serverConfigService.GetItemConfigById(itemId);
                if (originItemConfig == null || originItemConfig.type != ItemType.TreasureBox ||
                    originQuality != (int)originItemConfig.quality)
                    continue;
                int showQuality = Math.Max(0, originQuality - convertTime);
                int showBoxId = itemId - itemId % 10 + showQuality;
                // 判断一下转化是不是有效
                var itemConfig = serverConfigService.GetItemConfigById(showBoxId);
                if (itemConfig == null)
                    continue;
                generalReward.ItemList[i] = showBoxId;
            }
        }

        return (newCardList, ErrorKind.SUCCESS);
    }

    public async Task<(ErrorKind result, string errorMessage)> VerifyCheating(
        string cheatingBanTaskId,
        List<string> cheatingResult,
        UserGameInfo userGameInfo,
        long playerId, long userId,
        BuildingGameDB context)
    {
        if (cheatingResult.IsNullOrEmpty())
            return (ErrorKind.SUCCESS, "");

        // 需要计数，然后存库
        userGameInfo.CheatAccumulate++;
        var banedSeconds = serverConfigService.GetBanedSecondsByAccumulate(userGameInfo.CheatAccumulate);
        // 需要存一下库
        await context.SaveChangesWithDefaultRetryAsync();

        logger.LogInformation("PlayerId {PlayerId} cheating, Info : {Info}, ban duration: {Duration}", playerId,
            cheatingResult.ConcatToString(';'), banedSeconds);
        if (banedSeconds > 0)
        {
            await playerBanner.BanPlayerAsync(
                taskId: cheatingBanTaskId,
                playerId: playerId,
                banReason: $"Player Cheating: Accu = {userGameInfo.CheatAccumulate}",
                durationSeconds: banedSeconds,
                rules: BuildCheatingPunishmentRules()
            );
        }

        return (ErrorKind.PLAYER_CHEATING, banedSeconds.ToString());
    }

    private IEnumerable<PunishmentRule> BuildCheatingPunishmentRules()
    {
        // TODO: 完善封禁后的惩罚规则，如移除排行榜、清除货币等
        var leaderboardTypes = new[] { "世界榜", "十面埋伏榜", "墨浪坚防榜", "无尽模式榜" };
        return
        [
            new()
            {
                Key = PlayerPunishmentConsumer.RemoveFromLeaderboardRuleKey,
                Params = JsonSerializer.Serialize(leaderboardTypes)
            },
            // new()
            // {
            //     Key = PlayerPunishmentConsumer.CleanUserAssetsRuleKey
            // }
        ];
    }

    private async Task<UserDailyTreasureBoxProgress> GetOrAddDailyTreasureBoxProgressAsync(short shardId, long playerId)
    {
        var data = await gameRepository.GetDailyTreasureBoxProgressAsync(shardId, playerId);
        data ??= gameRepository.AddUserDailyTreasureBoxProgress(shardId, playerId);
        return data;
    }

    public async Task<UserDailyTreasureBoxProgress> GetAndRefreshUserDailyTreasureBoxProgress(
        long playerId, short shardId, int timeZoneOffset)
    {
        var data = await GetOrAddDailyTreasureBoxProgressAsync(shardId, playerId);
        // 检查下时间戳
        CheckDailyTreasureBoxTimestamp(data, timeZoneOffset);
        return data;
    }

    public async Task IncreaseDailyTreasureBoxProgress(
        long playerId, short shardId, int division, int addProgress, int timeZoneOffset)
    {
        var data = await GetAndRefreshUserDailyTreasureBoxProgress(playerId, shardId, timeZoneOffset);
        var divisionConfig = serverConfigService.GetDivisionConfig(division);
        var progressRewardList = divisionConfig.daily_progress_reward;
        data.Progress = Math.Min(progressRewardList.Length, data.Progress + addProgress);
    }

    private void CheckDailyTreasureBoxTimestamp(UserDailyTreasureBoxProgress data, int timeZoneOffset)
    {
        var currentTime = TimeUtils.GetCurrentTime();
        if (TimeUtils.GetDayDiffBetween(currentTime, data.Timestamp, timeZoneOffset, 0) > 0)
        {
            data.Timestamp = currentTime;
            data.Progress = 0;
            data.RewardClaimStatus = 0;
        }
    }

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
        return dbCtx.WithDefaultRetry(_ => gameRepository.GetUserGameInfoByIdAsync(shardId, playerId));
    }

    /// <summary>
    /// Notice: 函数内部已SaveChange
    /// </summary>
    public ValueTask<(int, ErrorKind)> ReRollFreeMapAsync(short shardId, long playerId, int times)
    {
        var costCoin = serverConfigService.GetMapReRollCost(times);
        return dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var userAssets = await userAssetService.GetUserAssetsSimpleAsync(shardId, playerId);
            if (userAssets is null)
            {
                return (0, ErrorKind.NO_USER_ASSET);
            }

            if (userAssets.CoinCount < costCoin)
            {
                return (0, ErrorKind.COIN_NOT_ENOUGH);
            }

            userAssets.CoinCount -= costCoin;
            await dbCtx.SaveChangesAsync();

            return (userAssets.CoinCount, ErrorKind.SUCCESS);
        });
    }
}
