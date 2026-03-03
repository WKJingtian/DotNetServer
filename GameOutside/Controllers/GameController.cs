using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Games.BuildingGame.Services;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;

namespace GameOutside.Controllers;

[Authorize]
public class GameController(
    IConfiguration configuration,
    BuildingGameDB context,
    ILogger<UserLevelController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    PlayerModule playerModule,
    ActivityService activityService,
    SeasonService seasonService,
    UserEndlessRankService userEndlessRankService,
    DivisionService divisionService,
    UserInfoService userInfoService,
    AntiCheatService antiCheatService,
    GameService gameService,
    UserAssetService userAssetService,
    UserCardService userCardService,
    IapPackageService iapPackageService,
    BattlePassService battlePassService,
    CacheManager cacheManager,
    UserAchievementService userAchievementService
    ) : BaseApiController(configuration)
{
    private const string GameEndDesKey = "jO*&}.;H";

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnNormalGameEnd(string message, string hash)
    {
        // ============================ 校验消息 ============================ //
        var gameEndMessage = gameService.CheckGameEndMessageValid<NormalGameEndMessage>(message, hash, GameEndDesKey);
        if (gameEndMessage is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(PlayerId, gameEndMessage);
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateNormalAndFixGameScore(gameEndMessage, false, PlayerId, currentSeasonNumber);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        // 如果不是1.1.0版本，需要截取到int.Max，否则结算会报错
        if (GameVersion.CompareVersionStrServer("1.1.0") < 0)
        {
            scoreLong = Math.Min(int.MaxValue, scoreLong);
        }

        if (scoreLong < 0)
        {
            cheatingList.Add($"Final Score = {scoreLong} < 0");
        }

        // ============================ 计算战令经验 ============================ //
        var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, true, currentSeasonNumber);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            if (!gameService.IsMessageObjValid(userGameInfo, gameEndMessage))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            antiCheatService.CheckDifficultyCheating(userAsset, gameEndMessage, cheatingList);

            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo,
                PlayerId, PlayerShard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 通用游戏结束处理和分数上传 ============================ //
            var (division, commonError) = await gameService.ProcessCommonGameEndLogicWithScore(battleExpTotal,
                gameEndMessage.Win, scoreLong, gameEndMessage.TimeStamp, PlayerId, PlayerShard, GameVersion, LeaderboardModule.NormalModeLeaderBoardId);
            if (commonError != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = commonError });

            var divisionConfig = serverConfigService.GetDivisionConfig(division);
            var category = divisionConfig.category;

            // ============================ 计算奖励 ============================ //
            // 计算
            var (generalReward, error1) = await gameService.CalculateNormalGameRewards(gameEndMessage, PlayerId,
                PlayerShard, category, GameVersion, userAsset.TimeZoneOffset);
            if (error1 != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error1 });

            // ============================ 发放奖励 ============================ //
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            // 这里不包含卡牌信息，后面使用 AsNoTracking() 查询 UserCards，使用 upsert 避免 UserCards 产生大量 update 往返
            // includeOption |= UserAssetIncludeOptions.IncludeCards;
            if (includeOption != GameOutside.Repositories.UserAssetIncludeOptions.NoInclude)
            {
                userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
                if (userAsset == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            }
            var (newCardList, rewardError) = await gameService.AddGameEndReward(generalReward, userAsset,
                gameEndMessage, PlayerId, PlayerShard, GameVersion);
            if (rewardError != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)rewardError });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录难度等级变化 ============================ //
            var difficultyChanges = new List<DifficultyChange>();
            if (gameEndMessage.Win)
            {
                var difficulty = gameEndMessage.Difficulty;
                var difficultyLevels = userAsset.DifficultyData.Levels;
                var difficultyStars = userAsset.DifficultyData.Stars;
                if (difficultyLevels.Count < difficulty + 1)
                    difficultyLevels.AddRange(new int[difficulty + 1 - difficultyLevels.Count]);
                if (difficultyStars.Count < difficulty + 1)
                    difficultyStars.AddRange(new int[difficulty + 1 - difficultyStars.Count]);
                var difficultyLevel = gameEndMessage.DifficultyLevel;
                var difficultyConfig = serverConfigService.GetDifficultyConfig(difficulty);
                if (difficultyConfig == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_DIFFICULTY_CONFIG });

                int currentMaxLevel = difficultyLevels[difficulty];
                // 当当前通关的层级比当前解锁的层级高时，关卡等级升至下一级
                if (currentMaxLevel <= difficultyLevel)
                {
                    var currentStar = difficultyStars[difficulty];
                    var unlockedLevel = currentMaxLevel + 1;
                    int needStar =
                        difficultyConfig.need_stars[
                            Math.Min(difficultyConfig.need_stars.Count - 1, unlockedLevel)];
                    if (currentStar < needStar)
                        difficultyStars[difficulty] = ++currentStar;
                    bool isMaxLevel = !difficultyConfig.endless &&
                                      currentMaxLevel == difficultyConfig.score_mult.Count - 1;
                    if (!isMaxLevel && currentStar >= needStar)
                    {
                        difficultyLevels[difficulty] = unlockedLevel;
                        difficultyStars[difficulty] = 0;
                        difficultyChanges.Add(new DifficultyChange()
                        {
                            Difficulty = difficulty,
                            Level = unlockedLevel,
                            Star = 0
                        });
                    }
                }
            }

            // ============================ 记录卡牌战绩 ============================ //
            var changedCards = new List<UserCard>();
            if (gameEndMessage.Win)
            {
                HashSet<int> allCardInBattle = new HashSet<int>();
                foreach (var fightUnit in gameEndMessage.FightUnitList)
                    allCardInBattle.Add(serverConfigService.GetCardIdByDetailKey(fightUnit.Key));
                foreach (var buildingKey in gameEndMessage.AllBuildingBuiltThroughoutGame)
                    allCardInBattle.Add(serverConfigService.GetCardIdByDetailKey(buildingKey));

                var cardDataToPromote = await userCardService.GetReadonlyUserCardsByCardIdsAsync(PlayerShard, PlayerId, allCardInBattle);
                foreach (var cardData in cardDataToPromote)
                {
                    bool isChanged = false;
                    if (cardData.CardArenaDifficultyReached < gameEndMessage.Difficulty)
                    {
                        cardData.CardArenaDifficultyReached = gameEndMessage.Difficulty;
                        cardData.CardArenaLevelReached = gameEndMessage.DifficultyLevel;
                        isChanged = true;
                    }
                    else if (cardData.CardArenaDifficultyReached == gameEndMessage.Difficulty &&
                             cardData.CardArenaLevelReached < gameEndMessage.DifficultyLevel)
                    {
                        cardData.CardArenaLevelReached = gameEndMessage.DifficultyLevel;
                        isChanged = true;
                    }
                    if (isChanged)
                    {
                        changedCards.Add(cardData);
                    }
                }
            }

            // ============================ 增加每日宝箱进度 ============================ //
            if (gameEndMessage.Win &&
                serverConfigService.TryGetParameterInt(Params.ArenaModeTreasureBoxProgressAdd, out int addValue))
            {
                await gameService.IncreaseDailyTreasureBoxProgress(PlayerId, PlayerShard, divisionConfig.level, addValue, userAsset.TimeZoneOffset);
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, gameEndMessage.GameStartTime,
                () => gameEndMessage.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, gameEndMessage.TaskRecords,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            // TODO 这里存在重复查询了，需要改写一下
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY,
                gameEndMessage.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_ARENA_MAP, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);

            // 合并一下相同的奖励
            generalReward.DistinctAndMerge();

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // 更新卡牌
            if (changedCards.Count > 0)
            {
                await userCardService.UpsertUserCardsAsync(changedCards);
            }
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                gameEndMessage.AchievementRecords, PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = difficultyChanges
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnFixedMapGameEnd(string message, string hash)
    {
        // ============================ 校验消息 ============================ //
        var gameEndMessage = gameService.CheckGameEndMessageValid<NormalGameEndMessage>(message, hash, GameEndDesKey);
        if (gameEndMessage is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(PlayerId, gameEndMessage);
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateNormalAndFixGameScore(gameEndMessage, true, PlayerId, currentSeasonNumber);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        if (scoreLong < 0)
        {
            cheatingList.Add($"Final Score = {scoreLong} < 0");
        }

        // ============================ 计算战令经验 ============================ //
        var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, true, currentSeasonNumber);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            if (!gameService.IsMessageObjValid(userGameInfo, gameEndMessage))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo,
                PlayerId, PlayerShard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 通用游戏结束处理和分数上传 ============================ //
            var (division, commonError) = await gameService.ProcessCommonGameEndLogicWithScore(battleExpTotal,
                gameEndMessage.Win, scoreLong, gameEndMessage.TimeStamp, PlayerId, PlayerShard, GameVersion, LeaderboardModule.NormalModeLeaderBoardId);
            if (commonError != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = commonError });

            // ============================ 计算星星 ============================ //
            var fixedMapConfig = serverConfigService.GetFixedMapConfig(gameEndMessage.MapId);
            if (fixedMapConfig is null)
                return BadRequest(ErrorKind.NO_MAP_CONFIG.Response());
            var (startResult, error2) = gameService.CalculateTaskStars(gameEndMessage, fixedMapConfig);
            if (error2 != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error2 });
            var starCount = 0;
            var taskIdList = new List<int>();
            var starStatus = new List<bool>();
            var succeedTaskList = new List<int>();
            foreach ((int taskId, bool hasStar) in startResult)
            {
                taskIdList.Add(taskId);
                starStatus.Add(hasStar);
                starCount += hasStar ? 1 : 0;
                if (hasStar)
                    succeedTaskList.Add(taskId);
            }

            var customArgs = new FixedMapGameEndCustomArgs(taskIdList, starStatus);
            // ============================ 发放星星 ============================ //
            var fixedLevelMapProgress =
                await context.GetUserFixedLevelMapProgress(PlayerId, PlayerShard, fixedMapConfig.id);
            var oldStars = 0;
            if (fixedLevelMapProgress is null)
            {
                fixedLevelMapProgress = new UserFixedLevelMapProgress
                {
                    MapId = fixedMapConfig.id,
                    PlayerId = PlayerId,
                    ShardId = PlayerShard,
                    StarCount = starCount,
                };
                await context.AddUserFixedLevelMapProgress(fixedLevelMapProgress);
                fixedLevelMapProgress.FinishedTaskList = succeedTaskList;
                oldStars = 0;
            }
            else
            {
                oldStars = fixedLevelMapProgress.StarCount;
                fixedLevelMapProgress.StarCount = fixedLevelMapProgress.StarCount < starCount
                    ? starCount
                    : fixedLevelMapProgress.StarCount;
                if (fixedLevelMapProgress.StarCount <= starCount)
                    fixedLevelMapProgress.FinishedTaskList = succeedTaskList;
            }

            // ============================ 直接按照配置给固定奖励 ============================ //
            var generalReward = new GeneralReward() { ItemList = [], CountList = [] };
            if (oldStars <= 0 && fixedLevelMapProgress.StarCount > 0)
            {
                // 通关奖励
                generalReward.ItemList.AddRange(fixedMapConfig.item_list);
                generalReward.CountList.AddRange(fixedMapConfig.item_count_list);
            }

            // 如果是第一次通过1-1，额外发放历练之路奖励
            if (oldStars <= 0 && fixedMapConfig.id == 2)
            {
                generalReward.ItemList.Add(2);
                generalReward.CountList.Add(100);
            }

            // ============================ 发放奖励 ============================ //
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var (newCardList, rewardError) = await gameService.AddGameEndReward(generalReward, userAsset,
                gameEndMessage, PlayerId, PlayerShard, GameVersion);
            if (rewardError != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)rewardError });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 增加每日宝箱进度 ============================ //
            // 第一次过1-1写死了不增加每日宝箱进度
            if (gameEndMessage.Win &&
                serverConfigService.TryGetParameterInt(Params.BattleModeTreasureBoxProgressAdd, out int addValue) &&
                !(oldStars <= 0 && fixedMapConfig.id == 2))
            {
                await gameService.IncreaseDailyTreasureBoxProgress(PlayerId, PlayerShard, division, addValue, userAsset.TimeZoneOffset);
            }

            // ============================ 记录新难度解锁 ============================ //
            var difficultyChanges = new List<DifficultyChange>();
            if (gameEndMessage.Win)
            {
                var difficultyConfigList = serverConfigService.GetDifficultyConfigList();
                var difficultyData = userAsset.DifficultyData;
                for (int i = 0; i < difficultyConfigList.Count; i++)
                {
                    var difficultyConfig = difficultyConfigList[i];
                    if (difficultyConfig.unlock_fixed_map == -1 ||
                        gameEndMessage.MapId == difficultyConfig.unlock_fixed_map ||
                        oldStars > 0)
                        continue;
                    difficultyChanges.Add(new DifficultyChange() { Difficulty = i, Level = 0, Star = 0 });
                }
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, gameEndMessage.GameStartTime,
                () => gameEndMessage.ToFixedMapUserHistory(PlayerShard, PlayerId, scoreLong, starCount));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, gameEndMessage.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY,
                gameEndMessage.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_STORY_MAP, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);

            // 合并一下相同的奖励
            generalReward.DistinctAndMerge();

            // ============================ 是否符合礼包推销条件 ============================ //
            if (!serverConfigService.TryGetParameterInt(Params.GeneralPromotionUnlockMap, out var promotionUnlockLevelId) ||
               !serverConfigService.TryGetParameterInt(Params.PromotionShowInterval, out var packagePromotionShowInterval) ||
               !serverConfigService.TryGetParameterString(Params.PromotedPackageIapIdList, out var promotedPackageIapIds))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
            if (fixedMapConfig.id == promotionUnlockLevelId)
            {
                var userPromotionStatus = await iapPackageService.GetPromotionData(PlayerId, PlayerShard);
                if (userPromotionStatus == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
                // 向玩家推销破冰付费
                if (userPromotionStatus.IceBreakingPayPromotion == 0)
                    userPromotionStatus.IceBreakingPayPromotion = 1;
            }
            else if (!gameEndMessage.Win && fixedMapConfig.id > promotionUnlockLevelId && !fixedMapConfig.IsTrainLevel)
            {
                var userPromotionStatus = await iapPackageService.GetPromotionData(PlayerId, PlayerShard);
                if (userPromotionStatus == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
                var promotedPackageIapIdList = promotedPackageIapIds.Split('|').ToList();
                if ((userPromotionStatus.LastPromotedPackage == "" && userPromotionStatus.PackagePromotionTime == 0) ||
                    // 玩家从未被推销过限时礼包
                    (userPromotionStatus.LastPromotedPackage != "" &&
                     TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), userPromotionStatus.PackagePromotionTime, userAsset.TimeZoneOffset, 0) >= packagePromotionShowInterval))
                // 玩家未完成所有限时礼包的购买，且冷却时间到了
                {
                    if (userPromotionStatus.LastPromotedPackage == "")
                        userPromotionStatus.LastPromotedPackage = promotedPackageIapIdList[0];
                    userPromotionStatus.PackagePromotionTime = TimeUtils.GetCurrentTime();
                }
            }

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                gameEndMessage.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = difficultyChanges,
                CustomArgs = customArgs.ToJson(),
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnEndlessGameEnd(string message, string hash)
    {
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<EndlessGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(messageObj);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            if (!gameService.IsMessageObjValid(userGameInfo, messageObj))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo,
                PlayerId, PlayerShard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 计算分数 ============================ //
            var (scoreList, playerScoreList, scoreLong) = gameService.CalculateEndlessGameScore(messageObj, PlayerId);

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, false, currentSeasonNumber);

            // ============================ 通用游戏结束处理 ============================ //
            var error = await gameService.ProcessCommonGameEndLogic(PlayerId, PlayerShard, GameVersion, battleExpTotal, messageObj.Win);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            var endlessRewardConfig = serverConfigService.GetEndlessRewardConfigByScore(scoreLong);
            if (endlessRewardConfig == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.WRONG_GAME_SCORE });

            var treasureBoxId = endlessRewardConfig.treasure_box_id;
            // 宝箱奖励
            List<int> generalRewardItemList = new();
            List<int> generalRewardCountList = new();
            if (treasureBoxId > 0)
            {
                generalRewardItemList.Add(treasureBoxId);
                generalRewardCountList.Add(1);
            }

            var generalReward
                = new GeneralReward { ItemList = generalRewardItemList, CountList = generalRewardCountList };
            var newCardList = new List<UserCard>();

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            // 需要检查今日挑战次数限制
            int activityId = messageObj.ActivityId;
            var activityData = await activityService.GetEndlessChallengeDataAsync(PlayerId, PlayerShard, activityId);
            if (activityData == null)
            {
                // 检查下活动有没有已经结束
                var activityTimeConfig = serverConfigService.GetActivityConfigById(activityId);
                if (activityTimeConfig != null)
                {
                    if (activityService.IsOpen(activityTimeConfig, GameVersion))
                    {
                        // 默认数据
                        activityData = activityService.CreateDefaultEndlessChallengeData(PlayerId, PlayerShard, activityId);
                    }
                }
            }

            if (activityData != null)
            {
                var gameStartTime = messageObj.GameStartTime;
                // 检查挑战次数是否需要重置
                if (TimeUtils.GetDayDiffBetween(gameStartTime, activityData.LastGameTime, userAsset.TimeZoneOffset, 0) >
                    0)
                {
                    activityData.TodayGameCount = 0;
                }

                // 检查次数限制
                if (!serverConfigService.TryGetParameterInt(Params.EndlessChallengeDailyMaxTime, out int maxPlayCount))
                    return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
                // 今日次数已达上限
                if (activityData.TodayGameCount >= maxPlayCount)
                    return BadRequest(ErrorKind.ENDLESS_CHALLENGE_PLAY_COUNT_LIMIT.Response());
                activityData.TodayGameCount++;
                activityData.LastGameTime = gameStartTime;

                // 检查是否解锁了新难度
                if (messageObj.Win)
                {
                    var maxDifficulty = serverConfigService.GetEndlessChallengeMaxDifficulty();
                    if (activityData.MaxUnlockDifficulty <= messageObj.Difficulty)
                    {
                        activityData.MaxUnlockDifficulty = Math.Min(messageObj.Difficulty + 1, maxDifficulty - 1);
                    }
                }
            }

            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, messageObj.GameStartTime,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_ARENA_MAP, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await activityService.AddTaskProgressToActiveActivityTask(ActivityTaskKeys.EndlessChallenge,
                PlayerId, PlayerShard, 1, userAsset.TimeZoneOffset, GameVersion);

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = []
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnSurvivorGameEnd(string message, string hash)
    {
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<SurvivorGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(messageObj);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            if (!gameService.IsMessageObjValid(userGameInfo, messageObj))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo,
                PlayerId, PlayerShard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 计算分数 ============================ //
            var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateSurvivorGameScore(messageObj, PlayerId);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 上传分数 ============================ //
            await userEndlessRankService.UploadEndlessScoreAsync(PlayerShard, PlayerId, scoreLong, messageObj.TimeStamp,
                "survivor");

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, false, currentSeasonNumber);

            // ============================ 发放战令经验 =================\=========== //
            error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

            // ============================ 检查共斗boss隐藏关卡 ============================ //
            var coopBossTimeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityCoopBoss, GameVersion);
            error = await activityService.TryOpenBossActivityAsync(PlayerId, PlayerShard, coopBossTimeConfig);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 计算奖励 ============================ //
            var calculateRewardArgs = serverConfigService.GetParameterFloatList(Params.SurvivorGameRewardItemCountArgs);
            if (calculateRewardArgs is null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            if (calculateRewardArgs.Count != 3)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            if (!serverConfigService.TryGetParameterInt(Params.SurvivorGameRewardItemId, out var itemId))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            var itemCount = calculateRewardArgs[0] -
                            calculateRewardArgs[0] / (messageObj.DifficultyLevel * calculateRewardArgs[1] + 1) +
                            calculateRewardArgs[2];
            var intItemCount = (int)MathF.Ceiling(itemCount);
            if (!messageObj.Win)
            {
                if (!serverConfigService.TryGetParameterInt(Params.OtherModeFailedItemCount, out var failedItemCount))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                intItemCount = failedItemCount;
            }

            List<int> generalRewardItemList = new List<int> { itemId };
            List<int> generalRewardCountList = new List<int> { intItemCount };
            var generalReward
                = new GeneralReward { ItemList = generalRewardItemList, CountList = generalRewardCountList };
            var newCardList = new List<UserCard>();

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, messageObj.GameStartTime,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_ARENA_MAP, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = []
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnTowerDefenceGameEnd(string message, string hash)
    {
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<TowerDefenceGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(messageObj);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            if (!gameService.IsMessageObjValid(userGameInfo, messageObj))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo,
                PlayerId, PlayerShard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 计算分数 ============================ //
            var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateTowerDefenceGameScore(messageObj, PlayerId);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 上传分数 ============================ //
            await userEndlessRankService.UploadEndlessScoreAsync(PlayerShard, PlayerId, scoreLong, messageObj.TimeStamp,
                "towerdefence");

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, false, currentSeasonNumber);

            // ============================ 发放战令经验 ============================ //
            error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

            // ============================ 计算奖励 ============================ //
            var calculateRewardArgs =
                serverConfigService.GetParameterFloatList(Params.TowerDefenceGameRewardItemCountArgs);
            if (calculateRewardArgs is null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            if (calculateRewardArgs.Count != 3)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            if (!serverConfigService.TryGetParameterInt(Params.SurvivorGameRewardItemId, out var itemId))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            var itemCount = calculateRewardArgs[0] -
                            calculateRewardArgs[0] / (messageObj.DifficultyLevel * calculateRewardArgs[1] + 1) +
                            calculateRewardArgs[2];
            var intItemCount = (int)MathF.Ceiling(itemCount);
            if (!messageObj.Win)
            {
                if (!serverConfigService.TryGetParameterInt(Params.OtherModeFailedItemCount, out var failedItemCount))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                intItemCount = failedItemCount;
            }

            List<int> generalRewardItemList = new List<int> { itemId };
            List<int> generalRewardCountList = new List<int> { intItemCount };
            var generalReward
                = new GeneralReward { ItemList = generalRewardItemList, CountList = generalRewardCountList };
            var newCardList = new List<UserCard>();

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 检查共斗boss隐藏关卡 ============================ //
            var coopBossTimeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityCoopBoss, GameVersion);
            error = await activityService.TryOpenBossActivityAsync(PlayerId, PlayerShard, coopBossTimeConfig);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, messageObj.GameStartTime,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_ARENA_MAP, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnTrueEndlessGameEnd(string message, string hash)
    {
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<TrueEndlessGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        }
        
        // 获取当前赛季，因为计分规则可能根据赛季改变
        var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();

        // ============================ 作弊检测 ============================ //
        var cheatingList = antiCheatService.CheckCheating(messageObj);

        var cheatingBanTaskId = Guid.NewGuid().ToString();
        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            if (!gameService.IsMessageObjValid(userGameInfo, messageObj))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });

            // ============================ 作弊检测 ============================ //
            var (verifyCheatingResult, verifyCheatingMessage) = await gameService.VerifyCheating(cheatingBanTaskId,
                cheatingList, userGameInfo, PlayerId, PlayerShard, context);
            if (verifyCheatingResult != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse()
                {
                    ErrorCode = (int)verifyCheatingResult,
                    Message = verifyCheatingMessage
                });

            // ============================ 计算分数 ============================ //
            var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateTrueEndlessGameScore(messageObj, PlayerId);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 上传分数 ============================ //
            await userEndlessRankService.UploadEndlessScoreAsync(PlayerShard, PlayerId, scoreLong, messageObj.TimeStamp,
                "trueendless");

            // ============================ 检查共斗boss隐藏关卡 ============================ //
            var coopBossTimeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityCoopBoss, GameVersion);
            error = await activityService.TryOpenBossActivityAsync(PlayerId, PlayerShard, coopBossTimeConfig);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExp(scoreList, false, currentSeasonNumber);

            // ============================ 发放战令经验 ============================ //
            error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

            // ============================ 计算奖励 ============================ //
            if (!serverConfigService.TryGetParameterInt(Params.TrueEndlessRewardItemId, out var itemId) ||
                !serverConfigService.TryGetParameterInt(Params.TrueEndlessRewardCountMax, out var itemCountMax))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONFIG });
            // todo: a better formula for reward item count calculation
            var intItemCount = (int)MathF.Floor(messageObj.KillCount * 0.4f);
            intItemCount = Math.Min(itemCountMax, intItemCount);

            List<int> generalRewardItemList = new List<int> { itemId };
            List<int> generalRewardCountList = new List<int> { intItemCount };
            var generalReward
                = new GeneralReward { ItemList = generalRewardItemList, CountList = generalRewardCountList };
            var newCardList = new List<UserCard>();

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, messageObj.GameStartTime,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.PLAY_ARENA_MAP, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnTreasureMazeGameEnd(string message, string hash)
    {
        // 这个先不检测作弊，因为没啥收益
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<TreasureMazeGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        }

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateTreasureMazeGameScore(messageObj, PlayerId);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            // ============================ 计算奖励 ============================ //
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var activityData = await activityService.GetTreasureMazeDataAsync(PlayerId, PlayerShard, messageObj.ActivityId);
            if (activityData == null)
            {
                var activityConfig = serverConfigService.GetActivityConfigById(messageObj.ActivityId);
                long activityStartTime = 0;
                if (activityConfig != null)
                    activityStartTime = TimeUtils.ParseDateTimeStrToUnixSecond(activityConfig.start_time);
                activityData = activityService.CreateTreasureMazeData(PlayerId, PlayerShard, messageObj.ActivityId, activityStartTime);
            }
            var (treasureMazeUpdateSuccess, _) = activityService.CheckRefreshTreasureMazeData(activityData, userAsset.TimeZoneOffset);
            if (!treasureMazeUpdateSuccess)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
            var difficultyConfig
                = serverConfigService.GetTreasureMazeDifficultyConfig(messageObj.ActivityId, messageObj.DifficultyLevel);
            if (difficultyConfig == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

            bool gameIsValid = false; // 只有玩家还有剩余游戏次数的时候才会发放奖励
            // ============================ 记录活动结束 ============================ //
            if (!messageObj.Invited)
            {
                if (activityData.GameKeyCount > 0)
                {
                    gameIsValid = true;
                    activityData.GameKeyCount -= 1;
                }
            }
            else
            {
                if (!serverConfigService.TryGetParameterInt(Params.TreasureMazeGuestGamePerDay, out int maxAwayGame))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                activityData.AwayGameCountToday += 1;
                if (activityData.AwayGameCountToday <= maxAwayGame)
                    gameIsValid = true;
            }

            var generalReward = new GeneralReward { ItemList = new List<int>(), CountList = new List<int>() };
            var newCardList = new List<UserCard>();
            if (gameIsValid)
            {
                if (messageObj.Win)
                {
                    int killRewardCoinCount = (int)(messageObj.KillCount * difficultyConfig.coin_reward_mult);
                    if (killRewardCoinCount > 0)
                        generalReward.AddReward(1, killRewardCoinCount);
                    if (!activityData.LevelPassed.Contains(messageObj.DifficultyLevel))
                    {
                        generalReward.ItemList.AddRange(difficultyConfig.first_time_reward_list);
                        generalReward.CountList.AddRange(difficultyConfig.first_time_reward_count);
                        activityData.LevelPassed.Add(messageObj.DifficultyLevel);
                    }
                }

                Random rand = new();
                foreach (var lootId in messageObj.TreasurePileLooted)
                {
                    var lootConfig = serverConfigService.GetTreasureMazeLootConfig(messageObj.ActivityId, lootId);
                    if (lootConfig == null)
                        return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                    var rewardPool = serverConfigService.GetTreasureMazeRewardPoolByLootId(messageObj.ActivityId, lootId);
                    if (rewardPool == null)
                        return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                    var rewardDict =
                        rewardPool.WeightedRandomSelectAllowDuplicate(1, item => item.Weight);
                    foreach (var reward in rewardDict)
                        generalReward.AddReward(reward.Key.ItemId, reward.Value);
                }

                // ============================ 发放奖励 ============================ //
                if (generalReward.ItemList.Count > 0)
                {
                    var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                    if (result == null)
                        return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                    newCardList.AddRange(cardList);
                }

                // ============================ 计算战令经验 ============================ //
                var battleExpTotal = battlePassService.CalculateGameBattleExpByActivityGame(messageObj.KillCount);

                // ============================ 发放战令经验 ============================ //
                error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
                if (error != (int)ErrorKind.SUCCESS)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

                // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
                if (battleExpTotal > 0)
                {
                    generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                    generalReward.CountList.Add(battleExpTotal);
                }
            }

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.JOIN_ACTIVITY, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await activityService.AddTaskProgressToActiveActivityTask(ActivityTaskKeys.TreasureMaze,
                PlayerId, PlayerShard, 1, userAsset.TimeZoneOffset, GameVersion);

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, null,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnOneShotKillGameEnd(string message, string hash)
    {
        // 这个先不检测作弊，因为没啥收益
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<OneShotKillGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        }

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateOneShotKillGameScore(messageObj, PlayerId);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            // ============================ 计算奖励 ============================ //
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var activityData = await activityService.GetOneShotKillDataAsync(PlayerId, PlayerShard, messageObj.ActivityId, TrackingOptions.Tracking);
            activityData ??= activityService.CreateDefaultOneShotKillData(PlayerId, PlayerShard, messageObj.ActivityId);
            var oneShotKillMapConfigList
                = serverConfigService.GetOneShotKillMapConfigListByActivityId(messageObj.ActivityId);
            if (messageObj.DifficultyLevel >= oneShotKillMapConfigList.Count)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
            var oneShotKillMapConfig = oneShotKillMapConfigList[messageObj.DifficultyLevel];

            // ============================ 记录活动结束 ============================ //
            bool allowReward = false; // 只有玩家还有剩余游戏次数的时候才会发放每次通关奖励
            if (messageObj.IsChallengeMode)
            {
                if (!serverConfigService.TryGetParameterInt(Params.OneShotKillMaxChallengeVictoryPerDay, out int maxVictory))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                if (TimeUtils.GetDayDiffBetween(
                        TimeUtils.GetCurrentTime(), activityData.ChallengeVictoryUpdateTimestamp, userAsset.TimeZoneOffset,
                        0) > 0)
                {
                    activityData.ChallengeVictoryUpdateTimestamp = TimeUtils.GetCurrentTime();
                    activityData.ChallengeVictoryCount = 0;
                }
                if (messageObj.Win) activityData.ChallengeVictoryCount += 1;
                allowReward = activityData.ChallengeVictoryCount <= maxVictory;
            }
            else
            {
                if (!serverConfigService.TryGetParameterInt(Params.OneShotKillMaxNormalVictoryPerDay, out int maxVictory))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                if (TimeUtils.GetDayDiffBetween(
                        TimeUtils.GetCurrentTime(), activityData.NormalVictoryUpdateTimestamp, userAsset.TimeZoneOffset,
                        0) > 0)
                {
                    activityData.NormalVictoryUpdateTimestamp = TimeUtils.GetCurrentTime();
                    activityData.NormalVictoryCount = 0;
                }
                if (messageObj.Win) activityData.NormalVictoryCount += 1;
                allowReward = activityData.NormalVictoryCount <= maxVictory;
            }

            var activityConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityOneShotKill, GameVersion);
            if (!messageObj.Invited && activityConfig != null && activityConfig.id == messageObj.ActivityId)
            {
                await activityService.AddOneShotKillVictoryAsync(messageObj.ActivityId, messageObj.DifficultyLevel,
                    messageObj.OneShotKillTaskRecords, messageObj.Win, messageObj.IsChallengeMode);
            }

            var generalReward = new GeneralReward { ItemList = new List<int>(), CountList = new List<int>() };
            var newCardList = new List<UserCard>();
            if (messageObj.Win)
            {
                if ((activityData.MapCompleteRewardClaimStatus & (1 << messageObj.DifficultyLevel)) == 0)
                {
                    for (int i = 0; i < oneShotKillMapConfig.first_time_reward_list.Count; i++)
                        generalReward.AddReward(oneShotKillMapConfig.first_time_reward_list[i], oneShotKillMapConfig.first_time_reward_count[i]);
                }

                if (allowReward)
                {
                    if (messageObj.IsChallengeMode)
                    {
                        for (int i = 0; i < oneShotKillMapConfig.challenge_reward_list.Count; i++)
                            generalReward.AddReward(oneShotKillMapConfig.challenge_reward_list[i], oneShotKillMapConfig.challenge_reward_count[i]);

                        var gameTimeRewardConfig
                            = serverConfigService.GetScoreRewardConfigByTime((int)MathF.Ceiling(messageObj.GameTime), GameVersion);
                        if (gameTimeRewardConfig != null)
                        {
                            for (int i = 0; i < gameTimeRewardConfig.item_list.Count; i++)
                                generalReward.AddReward(gameTimeRewardConfig.item_list[i], gameTimeRewardConfig.count_list[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < oneShotKillMapConfig.normal_reward_list.Count; i++)
                            generalReward.AddReward(oneShotKillMapConfig.normal_reward_list[i], oneShotKillMapConfig.normal_reward_count[i]);
                    }
                }

                activityData.MapCompleteRewardClaimStatus |= ((long)1 << messageObj.DifficultyLevel);
            }

            // ============================ 发放奖励 ============================ //
            var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (result == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
            newCardList.AddRange(cardList);

            if (allowReward)
            {
                // ============================ 计算战令经验 ============================ //
                var battleExpTotal = battlePassService.CalculateGameBattleExpByActivityGame(messageObj.KillCount);

                // ============================ 发放战令经验 ============================ //
                error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
                if (error != (int)ErrorKind.SUCCESS)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

                // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
                if (battleExpTotal > 0)
                {
                    generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                    generalReward.CountList.Add(battleExpTotal);
                }
            }

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard,
                PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.JOIN_ACTIVITY, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await activityService.AddTaskProgressToActiveActivityTask(ActivityTaskKeys.OneShotKill,
                PlayerId, PlayerShard, 1, userAsset.TimeZoneOffset, GameVersion);

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, null,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnCoopBossGameEnd(string message, string hash)
    {
        // 这个先不检测作弊，因为没啥收益
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<CoopBossGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        }

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateCoopBossGameScore(messageObj, PlayerId);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            // ============================ 计算奖励 ============================ //
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            var generalReward = new GeneralReward { ItemList = new List<int>(), CountList = new List<int>() };
            var newCardList = new List<UserCard>();
            if (messageObj.Win)
            {
                var rewardConfigList
                    = serverConfigService.GetCoopBossRewardConfigListByActivityId(messageObj.ActivityId);
                if (rewardConfigList == null)
                    return BadRequest(ErrorKind.NO_COOP_BOSS_REWARD_CONFIG.Response());
                var reward = rewardConfigList[messageObj.Level];
                generalReward.ItemList.AddRange(reward.item_list);
                generalReward.CountList.AddRange(reward.count_list);

                // ============================ 记录活动结束 ============================ //
                var coopBossStatus = await activityService.GetCoopBossDataAsync(PlayerId, PlayerShard, messageObj.ActivityId, TrackingOptions.Tracking);
                if (!messageObj.Invited)
                {
                    if (coopBossStatus == null)
                        return BadRequest(ErrorKind.NO_COOP_BOSS_STATUS.Response());
                    coopBossStatus.LastLevelActivateTime = 0;
                }

                // ============================ 记录破绽之眼的结算次数 ============================ //
                coopBossStatus
                    ??= activityService.CreateDefaultCoopBossData(PlayerId, PlayerShard, messageObj.ActivityId);

                // 检查一下跨天刷新
                var newDay = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), coopBossStatus.LastRefreshTime,
                    userAsset.TimeZoneOffset, 0) > 0;
                if (newDay)
                {
                    coopBossStatus.LastRefreshTime = TimeUtils.GetCurrentTime();
                    coopBossStatus.GameEndCountToday = 0;
                    coopBossStatus.RefreshCountToday = 0;
                }
                if (messageObj.Invited)
                    coopBossStatus.GameEndCountToday += 1;
            }

            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExpByActivityGame(messageObj.KillCount);

            // ============================ 发放战令经验 ============================ //
            error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.JOIN_ACTIVITY, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await activityService.AddTaskProgressToActiveActivityTask(ActivityTaskKeys.CoopBoss,
                PlayerId, PlayerShard, 1, userAsset.TimeZoneOffset, GameVersion);

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, null,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnRpgGameEnd(string message, string hash)
    {
        // 这个先不检测作弊，因为没啥收益
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<RpgGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        }

        // ============================ 读个配置 ============================ //
        var levelConfig =
            serverConfigService.GetRpgGameLevelConfigByLevelAndActivityId(messageObj.ActivityId,
                messageObj.Level);
        if (levelConfig is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.CONFIG_NOT_FOUND });
        if (!serverConfigService.TryGetParameterInt(Params.MaxRpgGamePerDay, out var gameLimit))
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateRpgGameScore(messageObj, PlayerId, levelConfig.score_mult);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var rpgGameData =
                await activityService.GetRpgGameDataAsync(PlayerId, PlayerShard, messageObj.ActivityId, TrackingOptions.Tracking);
            if (rpgGameData == null)
                rpgGameData = activityService.CreateDefaultRpgGameData(PlayerId, PlayerShard, messageObj.ActivityId);
            var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), rpgGameData.LastGameCountRecordTime,
                userAsset.TimeZoneOffset, 0);
            if (dayDiff > 0)
                rpgGameData.TodayGameCount = 0;

            // ============================ 计算奖励 ============================ //
            var generalReward = new GeneralReward { ItemList = new List<int>(), CountList = new List<int>() };
            var newCardList = new List<UserCard>();
            bool allowReward = rpgGameData.TodayGameCount < gameLimit;
            if (messageObj.Win)
            {
                if (allowReward)
                {
                    rpgGameData.TodayGameCount += 1;
                    rpgGameData.LastGameCountRecordTime = TimeUtils.GetCurrentTime();
                    generalReward.ItemList.AddRange(levelConfig.item_list);
                    generalReward.CountList.AddRange(levelConfig.count_list);
                }
                if ((rpgGameData.LevelPassedStatus & (1 << messageObj.Level)) == 0)
                {
                    rpgGameData.LevelPassedStatus |= (long)1 << messageObj.Level;
                    generalReward.ItemList.AddRange(levelConfig.first_victory_item_list);
                    generalReward.CountList.AddRange(levelConfig.first_victory_count_list);
                }
            }

            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExpByActivityGame(messageObj.KillCount);

            // ============================ 发放战令经验 ============================ //
            error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.JOIN_ACTIVITY, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await activityService.AddTaskProgressToActiveActivityTask(ActivityTaskKeys.RpgGameChallenge,
                PlayerId, PlayerShard, 1, userAsset.TimeZoneOffset, GameVersion);

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, null,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<OnGameEndReply>> OnLoogGameEnd(string message, string hash)
    {
        // 这个先不检测作弊，因为没啥收益
        // ============================ 校验消息 ============================ //
        var messageObj = gameService.CheckGameEndMessageValid<LoogGameEndMessage>(message, hash, GameEndDesKey);
        if (messageObj is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.MESSAGE_INVALID });
        }

        // ============================ 读个配置 ============================ //
        var levelConfig =
            serverConfigService.GetLoogGameLevelConfigByLevelAndActivityId(messageObj.ActivityId,
                messageObj.Level);
        if (levelConfig is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.CONFIG_NOT_FOUND });
        if (!serverConfigService.TryGetParameterInt(Params.MaxLoogGamePerDay, out var gameLimit))
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });

        // ============================ 计算分数 ============================ //
        var (scoreList, playerScoreList, scoreLong, error) = gameService.CalculateLoogGameScore(messageObj, PlayerId, levelConfig.score_mult);
        if (error != (int)ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = error });

        return await context.WithRCUDefaultRetry<ActionResult<OnGameEndReply>>(async _ =>
        {
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var loogGameData =
                await activityService.GetLoogGameDataAsync(PlayerId, PlayerShard, messageObj.ActivityId, TrackingOptions.Tracking);
            if (loogGameData == null)
                loogGameData = activityService.CreateDefaultLoogGameData(PlayerId, PlayerShard, messageObj.ActivityId);
            var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), loogGameData.LastGameCountRecordTime,
                userAsset.TimeZoneOffset, 0);
            if (dayDiff > 0)
                loogGameData.TodayGameCount = 0;

            // ============================ 计算奖励 ============================ //
            var generalReward = new GeneralReward { ItemList = new List<int>(), CountList = new List<int>() };
            var newCardList = new List<UserCard>();
            bool allowReward = loogGameData.TodayGameCount < gameLimit;
            if (messageObj.Win)
            {
                if (allowReward)
                {
                    loogGameData.TodayGameCount += 1;
                    loogGameData.LastGameCountRecordTime = TimeUtils.GetCurrentTime();
                    generalReward.ItemList.AddRange(levelConfig.item_list);
                    generalReward.CountList.AddRange(levelConfig.count_list);
                }
                if ((loogGameData.LevelPassedStatus & (1 << messageObj.Level)) == 0)
                {
                    loogGameData.LevelPassedStatus |= (long)1 << messageObj.Level;
                    generalReward.ItemList.AddRange(levelConfig.first_victory_item_list);
                    generalReward.CountList.AddRange(levelConfig.first_victory_count_list);
                }
            }

            // ============================ 发放奖励 ============================ //
            if (generalReward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
                if (result == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
                newCardList.AddRange(cardList);
            }

            // ============================ 计算战令经验 ============================ //
            var battleExpTotal = battlePassService.CalculateGameBattleExpByActivityGame(messageObj.KillCount);

            // ============================ 发放战令经验 ============================ //
            error = await battlePassService.AddBattlePassExp(PlayerId, PlayerShard, battleExpTotal);
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)error });

            // ============================ 给客户端返回的奖励列表里加上站令经验(仅客户端展示用) ============================ //
            if (battleExpTotal > 0)
            {
                generalReward.ItemList.Add((int)MoneyType.BattlePassExp);
                generalReward.CountList.Add(battleExpTotal);
            }

            // ============================ 记录任务 ============================ //
            var userTask = await context.UpdateBeginnerTaskProgress(serverConfigService, messageObj.TaskRecords,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.KILL_ENEMY, messageObj.KillCount,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.JOIN_ACTIVITY, 1,
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            await activityService.AddTaskProgressToActiveActivityTask(
                ActivityTaskKeys.LoogGameChallenge, PlayerId, PlayerShard, 1, userAsset.TimeZoneOffset, GameVersion);

            // ============================ 记录游玩历史 ============================ //
            error = (int)await userInfoService.RecordGameHistoryAsync(PlayerShard, PlayerId, null,
                () => messageObj.ToUserHistory(PlayerShard, PlayerId, scoreLong));
            if (error != (int)ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = error });

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            // ============================ 记录成就 ============================ //
            var achievementChange = await userAchievementService.IncreaseAchievementProgressAsync(
                messageObj.AchievementRecords,
                PlayerShard, PlayerId);
            achievementChange.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new OnGameEndReply
            {
                Scores = scoreList,
                PlayerScoreList = playerScoreList,
                GeneralReward = generalReward,
                AchievementChange = achievementChange,
                TaskChange = userTask,
                ScoreTotal = scoreLong,
                DifficultyChanges = [],
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimGuideFixedMapReward(int fixedMapId)
    {
        var fixedMapConfig = serverConfigService.GetFixedMapConfig(fixedMapId);
        if (fixedMapConfig is null)
            return BadRequest(ErrorKind.NO_MAP_CONFIG.Response());
        // 校验一下关卡
        if (!fixedMapConfig.IsTrainLevel)
            return BadRequest(ErrorKind.NONE_GUIDE_MAP_NOT_SUPPORTED.Response());

        return await context.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            // 发放星星
            var fixedLevelMapProgress =
                await context.GetUserFixedLevelMapProgress(PlayerId, PlayerShard, fixedMapConfig.id);
            var starCount = 1;
            var oldStars = 0;
            if (fixedLevelMapProgress is null)
            {
                fixedLevelMapProgress = new UserFixedLevelMapProgress
                {
                    MapId = fixedMapConfig.id,
                    PlayerId = PlayerId,
                    ShardId = PlayerShard,
                    StarCount = starCount,
                };
                await context.AddUserFixedLevelMapProgress(fixedLevelMapProgress);
                oldStars = 0;
            }
            else
            {
                oldStars = fixedLevelMapProgress.StarCount;
                fixedLevelMapProgress.StarCount = fixedLevelMapProgress.StarCount < starCount
                    ? starCount
                    : fixedLevelMapProgress.StarCount;
            }

            if (oldStars == starCount)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.REWARD_CLAIMED });

            // ============================ 直接按照配置给固定奖励 ============================ //
            var generalReward = new GeneralReward() { ItemList = [], CountList = [] };
            generalReward.ItemList.AddRange(fixedMapConfig.item_list);
            generalReward.CountList.AddRange(fixedMapConfig.item_count_list);

            // ============================ 发放奖励 ============================ //
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(takeRewardResult);
        });
    }

    [HttpPost]
    public Task<ActionResult<int>> GetSeasonNumber()
    {
        var currentSeasonNum = seasonService.GetCurrentSeasonNumber();
        return Task.FromResult<ActionResult<int>>(Ok(currentSeasonNum));
    }

    [HttpPost]
    public async Task<ActionResult<int>> ReRollFreeMap(int times)
    {
        if (times <= 0)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());
        try
        {
            var (coinCount, result) = await gameService.ReRollFreeMapAsync(PlayerShard, PlayerId, times);
            if (result != ErrorKind.SUCCESS)
                return BadRequest(result.Response());
            return Ok(coinCount);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }
    }

    // 临时的匹配拉取用户卡牌信息代码
    [HttpPost]
    public async Task<ActionResult<MatchResult>> MatchUser(List<long> playerIdList)
    {
        var matchResult = new MatchResult() { PlayerList = new() };
        foreach (var pId in playerIdList)
        {
            var shardId = await playerModule.GetPlayerShardId(pId);
            if (shardId is null)
            {
                logger.LogWarning("MatchUser: shardId is null for playerId {PlayerId}", pId);
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
            }

            if (!await userInfoService.IsUserInfoExistsAsync(shardId, pId))
            {
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
            }

            var level = await userAssetService.GetLevelDataAsync(shardId, pId);
            if (level is null)
            {
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            }

            int division = await divisionService.GetDivisionNumberAsync(shardId.Value, pId, CreateOptions.DoNotCreateWhenNotExists);
            var cards = await userCardService.GetReadonlyUserCardsAsync(shardId.Value, pId);
            matchResult.PlayerList.Add(new MatchedPlayerInfo()
            {
                PlayerId = pId,
                DivisionRank = division,
                Level = level.Level,
                CardList = cards
            });
        }

        return Ok(matchResult);
    }

    // TODO 后面的内容可以分到另外的controller里
    public record struct SensitiveWordResult(bool NeedReplace, string Text);

    public record struct UserFixedLevelState(int MapId, int StartCount, List<int>? FinishedTaskList);

    public record struct UserStartGameInfo(
        List<UserCustomCardPool> CustomCardPools,
        long SeasonNo,
        DivisionController.UserDivisionReply? DivisionReply,
        UserLevelData UserLevelData,
        UserAttendance UserAttendance,
        List<UserFixedLevelState> FixedLevelStates,
        List<long> StarRewardClaimStatus,
        int TimeZoneOffset,
        string EncryptionKey
    );

    [HttpPost]
    public async Task<ActionResult<UserStartGameInfo>> FetchUserStartGameInfo()
    {
        return await context.WithRCUDefaultRetry<ActionResult<UserStartGameInfo>>(async _ =>
        {
            var reply = new UserStartGameInfo();
            bool databaseChanged = false;
            // 刷新次数
            {
                reply.SeasonNo = seasonService.GetCurrentSeasonNumber();
            }
            // 卡池编辑
            {
                reply.CustomCardPools = await context.GetAllUserCustomCardPoolsAsync(PlayerShard, PlayerId);
            }
            var userAssets = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            reply.TimeZoneOffset = userAssets.TimeZoneOffset;
            // 段位信息
            {
                var divisionScore = await divisionService.GetUserDivisionScoreAsync(PlayerShard, PlayerId,
                    CreateOptions.DoNotCreateWhenNotExists);
                if (divisionScore is null)
                {
                    reply.DivisionReply = null;
                }
                else
                {
                    reply.DivisionReply = new DivisionController.UserDivisionReply()
                    {
                        DivisionScore = divisionScore.Value,
                        Division = serverConfigService.GetDivisionByDivisionScore(divisionScore.Value),
                        SubDivision = -1
                    };
                }
            }
            // 历练之路等级
            {
                reply.UserLevelData = userAssets.LevelData;
            }
            // 签到记录
            {
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var attendanceRecord = await context.GetUserAttendanceRecord(PlayerShard, PlayerId);
                if (attendanceRecord == null)
                {
                    attendanceRecord = context.CreateUserAttendanceRecord(PlayerShard, PlayerId, currentTime);
                    databaseChanged = true;
                }

                if (!serverConfigService.TryGetParameterInt(Params.AttendanceTimeOffset, out int timeOffset))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });

                var localMidnightEpoch = TimeUtils.GetLocalMidnightEpoch(userAssets.TimeZoneOffset, timeOffset);
                if (localMidnightEpoch > attendanceRecord.LastLoginDate)
                {
                    attendanceRecord.TotalLoginDays++;
                    attendanceRecord.LastLoginDate = localMidnightEpoch;
                    databaseChanged = true;
                }
                reply.UserAttendance = attendanceRecord;
            }

            // 固定关卡信息
            {
                var fixedLevelMapProgresses = await context.GetAllUserFixedLevelMapProgresses(PlayerId, PlayerShard);
                reply.FixedLevelStates = fixedLevelMapProgresses.Select(entry =>
                        new UserFixedLevelState(entry.MapId, entry.StarCount, entry.FinishedTaskList))
                    .ToList();
                var starRewardInfo = await context.GetOrAddUserStarRewardClaimStatusAsync(PlayerId, PlayerShard);
                reply.StarRewardClaimStatus = starRewardInfo.StarRewardClaimStatus;
            }

            // 加密信息
            {
                var (encryptionInfo, added) = await context.GetOrAddUserEncryptionInfoAsync(PlayerId, PlayerShard);
                if (added)
                    databaseChanged = true;
                reply.EncryptionKey = EncryptHelper.EncryptHelper.DesEncrypt(encryptionInfo.EncryptionKey, "73Husi*g");
            }

            await cacheManager.SetPidByUidAsync(UserId, PlayerId);
            if (databaseChanged)
            {
                // 使用事务确保一致性
                await using var t = await context.Database.BeginTransactionAsync();
                await context.SaveChangesWithDefaultRetryAsync(false);
                await t.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();
            }

            return Ok(reply);
        });
    }

    public record struct MenuSceneStartInfoV1_0_0(
        DivisionController.UserDivisionReply? divisionInfo,
        int seasonNumber,
        UserPaymentController.FetchCommodityListReply? commodityList,
        UserAssets userAsset,
        TaskController.DailyTaskStatusReply dailyTaskStatus,
        UserBeginnerTask beginnerTaskStatus,
        MonthPassController.MonthPassData monthPassStatus,
        UserBattlePassController.FetchBattlePassInfoResult battlePassInfo,
        TakeRewardResult? expiredBattlePassReward,
        Dictionary<string, int>? iapPurchaseTimeDict,
        PaymentAndPromotionStatusReply promotionStatus,
        UserDailyTreasureBoxProgress dailyTreasureBoxInfo,
        List<UserMallAdvertisement> advertisementInfo,
        bool divisionChangedFromBronzeLevel, // TODO 不需要的字段，后续清除掉
        DivisionController.CheckDivisionRewardResponse? divisionRewardResponse,
        PiggyBankDataClient? piggyBankInfo,
        UserH5FriendActivityInfo userH5FriendActivityInfo
    );

    [HttpPost]
    public async Task<ActionResult<MenuSceneStartInfoV1_0_0>> FetchMenuSceneStartInfoV1_0_0(
        bool needToUpdateCommodityList, bool needToUpdateIapPurchaseTime)
    {
        return await context.WithRCUDefaultRetry<ActionResult<MenuSceneStartInfoV1_0_0>>(async _ =>
        {
            var reply = new MenuSceneStartInfoV1_0_0();
            bool needUpdateDatabase = false;
            var userAssets = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var userDivision
                = await divisionService.GetUserDivisionAsync(PlayerShard, PlayerId, TrackingOptions.Tracking);
            var newCardList = new List<UserCard>();
            // 段位信息
            {
                int division = 0;
                if (userDivision == null)
                    reply.divisionInfo = null;
                else
                {
                    reply.divisionInfo = new DivisionController.UserDivisionReply()
                    {
                        DivisionScore = userDivision.DivisionScore,
                        Division = serverConfigService.GetDivisionByDivisionScore(userDivision.DivisionScore),
                        SubDivision = -1
                    };
                    division = serverConfigService.GetDivisionByDivisionScore(userDivision.DivisionScore);
                }

                int seasonNumber = seasonService.GetCurrentSeasonNumber();
                reply.seasonNumber = seasonNumber;
                reply.divisionChangedFromBronzeLevel = false;
                if (userDivision != null && division == 0)
                {
                    if (await divisionService.RefreshBronzeDivisionAsync(userDivision, PlayerShard, PlayerId))
                    {
                        needUpdateDatabase = true;
                        reply.divisionChangedFromBronzeLevel = true;
                        reply.divisionInfo = new DivisionController.UserDivisionReply()
                        {
                            DivisionScore = userDivision.DivisionScore,
                            Division = serverConfigService.GetDivisionByDivisionScore(userDivision.DivisionScore),
                            SubDivision = -1
                        };
                    }
                }

                if (userDivision != null && !userDivision.RewardReceived)
                {
                    var (cardList, result) = await TryClaimDivisionReward(userDivision, userAssets, seasonNumber);
                    reply.divisionRewardResponse = result;
                    newCardList.AddRange(cardList);
                    needUpdateDatabase = true;
                }
            }
            // 商店
            if (needToUpdateCommodityList)
            {
                var commodities = serverConfigService.GetCommodityConfigList();
                var divisionScore = 0;
                if (userDivision != null)
                    divisionScore = userDivision.DivisionScore;
                var division = serverConfigService.GetDivisionByDivisionScore(divisionScore);
                var divisionConfig = serverConfigService.GetDivisionConfig(division);
                var category = divisionConfig.category;
                commodities = commodities
                    .Where(config => config.require_division < 0 || config.require_division == category)
                    .ToList();
                var boughtCommodities = await context.GetAllBoughtCommodityRecords(PlayerShard, PlayerId);
                var dailyStoreIndex = await context.GetUserDailyStoreIndex(PlayerShard, PlayerId);
                if (dailyStoreIndex == null)
                {
                    dailyStoreIndex = new UserDailyStoreIndex() { ShardId = PlayerShard, PlayerId = PlayerId, Index = 0 };
                    context.AddUserDailyStoreIndex(dailyStoreIndex);
                    needUpdateDatabase = true;
                }

                var dailyStoreItems = await context.GetUserDailyStoreItems(PlayerShard, PlayerId);
                var currentTime = (TimeUtils.GetCurrentTime() + userAssets.TimeZoneOffset) / 86400 * 86400;
                var needExpire = dailyStoreItems.Count <= 0;
                if (dailyStoreItems.Count > 0)
                    needExpire = dailyStoreItems[0].TimeStamp < currentTime;
                if (needExpire)
                {
                    context.DeleteAllDailyStoreItems(PlayerShard, PlayerId);
                    dailyStoreItems.Clear();
                }

                if (dailyStoreItems.Count <= 0)
                {
                    var userCards = userAssets.UserCards;
                    dailyStoreItems = serverConfigService.GenerateDailyStoreItems(PlayerShard, PlayerId, currentTime,
                        ref dailyStoreIndex, in userCards);
                    context.AddDailyStoreItems(dailyStoreItems);
                    needUpdateDatabase = true;
                }

                reply.commodityList = new UserPaymentController.FetchCommodityListReply()
                {
                    commonCommodities = commodities,
                    boughtCommodities = boughtCommodities,
                    dailyStoreItems = dailyStoreItems
                };
            }
            else
                reply.commodityList = null;

            // 任务
            {
                var (taskStatus, taskDataChanged) = await context.AddDailyTaskProgress(serverConfigService,
                    DailyTaskType.LOG_IN, 1,
                    PlayerShard, PlayerId, userAssets.TimeZoneOffset);
                if (taskDataChanged) needUpdateDatabase = true;
                reply.dailyTaskStatus = new TaskController.DailyTaskStatusReply()
                {
                    progress = taskStatus.TaskProgress,
                    taskRewardClaimStatus = taskStatus.DailyTaskRewardClaimStatus,
                    activeScoreRewardClaimStatus = taskStatus.ActiveScoreRewardClaimStatus,
                };

                var beginnerTask = await context.GetBeginnerTaskAsync(PlayerShard, PlayerId);
                if (beginnerTask == null)
                {
                    beginnerTask = new UserBeginnerTask()
                    {
                        ShardId = PlayerShard,
                        PlayerId = PlayerId,
                        FinishedCount = 0,
                        Received = false,
                        StartTime = TimeUtils.GetCurrentTime(),
                        DayIndex = 0,
                        TaskList = new(),
                    };

                    // 随机三个任务
                    var configList = serverConfigService.GetBeginnerTaskList();
                    var selectionList = configList.Where(config =>
                            !config.key.Equals("soldier_max_level") || beginnerTask.DayIndex >= 2)
                        .ToList();
                    var beginnerTaskConfig = serverConfigService.GetBeginnerTaskDayConfig(beginnerTask.DayIndex);
                    var taskList = new List<BeginnerTaskData>();
                    int i = 0;
                    if (beginnerTaskConfig != null)
                        for (; i < 3 && i < beginnerTaskConfig.predefined_tasks.Length; i++)
                        {
                            var predefinedTask
                                = serverConfigService.GetBeginnerTaskConfig(beginnerTaskConfig.predefined_tasks[i]);
                            selectionList.RemoveAll(config => config.key == predefinedTask.key);
                            taskList.Add(new BeginnerTaskData() { Id = predefinedTask.id, Progress = 0, });
                        }

                    for (; i < 3 && selectionList.Count > 0; i++)
                    {
                        var randomOne = selectionList.WeightedRandomSelectOne(_ => 10)!;
                        selectionList.RemoveAll(config => config.key == randomOne.key);

                        taskList.Add(new BeginnerTaskData() { Id = randomOne.id, Progress = 0, });
                    }

                    beginnerTask.TaskList = taskList;
                    context.Entry(beginnerTask).Property(t => t.TaskList).IsModified = true;

                    context.AddBeginnerTask(beginnerTask);
                    needUpdateDatabase = true;
                }

                reply.beginnerTaskStatus = beginnerTask;
            }
            // 月卡
            {
                MonthPassController.MonthPassData monthPass = new();
                var monthPassInfo = await context.GetMonthPassInfo(PlayerId, PlayerShard);
                if (monthPassInfo == null)
                    monthPass.nthDay = -2;
                else
                {
                    var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), monthPassInfo.PassAcquireTime,
                        userAssets.TimeZoneOffset, 0);
                    monthPass.nthDay = dayDiff < monthPassInfo.PassDayLength ? dayDiff : -1;
                    monthPass.dayLeft = monthPassInfo.PassDayLength - dayDiff - 1;
                }

                if (monthPass.nthDay >= 0 && monthPassInfo != null)
                {
                    if (monthPassInfo.LastRewardClaimDay / 30 != monthPass.nthDay / 30)
                        monthPassInfo.RewardClaimStatus = 0;
                    monthPass.claimStatus = monthPassInfo.RewardClaimStatus;
                    if (monthPassInfo.LastRewardClaimDay < monthPass.nthDay)
                    {
                        monthPassInfo.LastRewardClaimDay = monthPass.nthDay;
                        monthPassInfo.RewardClaimStatus |= 1 << (monthPass.nthDay % 30);
                        monthPass.claimStatus = monthPassInfo.RewardClaimStatus;
                        serverConfigService.TryGetParameterInt(Params.MonthPassDailyDiamond, out var diamondCount);
                        GeneralReward generalReward = new GeneralReward()
                        {
                            ItemList = new List<int>() { 0 },
                            CountList = new List<int>() { diamondCount },
                        };
                        // 这里确认只有玉璧，就不处理new card list返回了
                        await userItemService.TakeReward(userAssets, generalReward, GameVersion);
                        monthPass.totalDiamondCount = userAssets.DiamondCount;
                        monthPass.diamondAddCount = diamondCount;
                        needUpdateDatabase = true;
                    }
                }

                reply.monthPassStatus = monthPass;
            }
            // 貔貅/存钱罐活动
            {
                var piggyBankData = await activityService.GetPiggyBankStatusAsync(PlayerId, PlayerShard);
                if (piggyBankData == null)
                    piggyBankData = DataUtil.DefaultPiggyBankData(PlayerId, PlayerShard);
                if (serverConfigService.IfAllPiggyBankRewardClaimed(piggyBankData.ClaimStatus[0]) &&
                    serverConfigService.IfAllPiggyBankRewardClaimed(piggyBankData.ClaimStatus[1]))
                    reply.piggyBankInfo = null;
                else
                    reply.piggyBankInfo = piggyBankData.ToClientApi();
            }
            // 战令
            {
                var battlePassInfoList = await battlePassService.GetAllUserBattlePassInfoAsync(PlayerShard, PlayerId);
                int activeBattlePassId = serverConfigService.GetActiveBattlePassId();
                if (activeBattlePassId == -1)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_BATTLE_PASS_CONFIG });
                UserBattlePassInfo currentBattlePassInfo = null,
                    firstBattlePassInfo = null;
                GeneralReward battlePassReward = new GeneralReward() { ItemList = new(), CountList = new(), };
                Dictionary<int, int> battlePassRewardDic = new();
                foreach (var battlePassInfo in battlePassInfoList)
                {
                    if (battlePassInfo.PassId == -1)
                        firstBattlePassInfo = battlePassInfo;
                    else if (battlePassInfo.PassId == activeBattlePassId)
                        currentBattlePassInfo = battlePassInfo;
                    else
                    {
                        if (battlePassInfo.Exp == 0) continue;
                        var battlePassConfigList
                            = serverConfigService.GetBattlePassConfigListByPassId(battlePassInfo.PassId);
                        if (battlePassConfigList == null)
                            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_BATTLE_PASS_CONFIG });
                        needUpdateDatabase = true;
                        foreach (var passReward in battlePassConfigList)
                        {
                            if (battlePassInfo.Exp < passReward.exp)
                                break;
                            for (int i = 0; i <= battlePassInfo.SuperPassLevel; i++)
                            {
                                var claimed = battlePassInfo.ClaimStatus[i].GetNthBits(passReward.level);
                                if (claimed)
                                    continue;
                                battlePassRewardDic.TryAdd(passReward.item_list[i], 0);
                                battlePassRewardDic[passReward.item_list[i]] += passReward.count_list[i];
                                battlePassInfo.ClaimStatus[i]
                                    = battlePassInfo.ClaimStatus[i].SetNthBits(passReward.level, true);
                            }
                        }

                        // 领完奖之后以后就不用再检查了
                        battlePassInfo.Exp = 0;
                    }
                }

                if (battlePassRewardDic.Count > 0)
                {
                    foreach (var pair in battlePassRewardDic)
                    {
                        battlePassReward.ItemList.Add(pair.Key);
                        battlePassReward.CountList.Add(pair.Value);
                    }

                    var (cardList, result) = await userItemService.TakeReward(userAssets, battlePassReward, GameVersion);
                    newCardList.AddRange(cardList);
                    reply.expiredBattlePassReward = result;
                }

                if (currentBattlePassInfo == null)
                {
                    currentBattlePassInfo
                        = battlePassService.AddUserBattlePassInfo(PlayerShard, PlayerId, activeBattlePassId);
                    needUpdateDatabase = true;
                }

                if (firstBattlePassInfo == null)
                {
                    firstBattlePassInfo = battlePassService.AddUserBattlePassInfo(PlayerShard, PlayerId, -1);
                    needUpdateDatabase = true;
                }

                reply.battlePassInfo =
                    new UserBattlePassController.FetchBattlePassInfoResult()
                    {
                        BattlePass = currentBattlePassInfo,
                        FirstPass = firstBattlePassInfo
                    };
            }
            // 购买次数
            if (needToUpdateIapPurchaseTime)
            {
                Dictionary<string, int> purchaseTimeRecord = new();
                Dictionary<string, int> purchaseLimit = new();
                var configList = serverConfigService.GetSkuItemConfigList();
                foreach (var config in configList)
                {
                    if (config.buy_limit == 0) continue;
                    purchaseLimit.Add(config.prop_id, config.buy_limit);
                }

                var purchaseHistory = await iapPackageService.GetFullPurchaseListAsync(PlayerShard, PlayerId);
                foreach (var record in purchaseHistory)
                {
                    var config = serverConfigService.GetSkuItemConfig(record.IapItemId);
                    if (config == null)
                        return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                    if (!TimeUtils.IfRecordTimeIsInRange(config.limit_refresh_interval, config.sp_limit_refresh_rule,
                            TimeUtils.GetCurrentTime(), record.WhenPurchased, userAssets.TimeZoneOffset))
                        continue;
                    purchaseTimeRecord.TryAdd(config.prop_id, 0);
                    purchaseTimeRecord[record.IapItemId]++;
                }

                foreach (var record in purchaseLimit)
                {
                    var config = serverConfigService.GetSkuItemConfig(record.Key);
                    if (config == null)
                        return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                    if (config.share_limit_with.Count == 0)
                    {
                        if (purchaseTimeRecord.ContainsKey(record.Key))
                            purchaseLimit[record.Key] -= purchaseTimeRecord[record.Key];
                    }
                    else
                    {
                        foreach (var groupMemberId in config.share_limit_with)
                            if (purchaseTimeRecord.ContainsKey(groupMemberId))
                                purchaseLimit[record.Key] -= purchaseTimeRecord[groupMemberId];
                    }
                }

                reply.iapPurchaseTimeDict = purchaseLimit;
            }
            else
                reply.iapPurchaseTimeDict = null;

            // 推广
            {
                var status = await iapPackageService.GetPromotionData(PlayerId, PlayerShard);
                if (status == null)
                    return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
                reply.promotionStatus = new PaymentAndPromotionStatusReply()
                {
                    LastPromotedPackageId = status.LastPromotedPackage,
                    WhenPromoted = status.PackagePromotionTime,
                    IceBreakingPromotionStatus = status.IceBreakingPayPromotion,
                    DoubleDiamondBonusTriggerRecords = status.DoubleDiamondBonusTriggerRecords.Keys.ToHashSet(),
                };
            }
            // 每日宝箱
            {
                var userDailyProgress = await gameService.GetAndRefreshUserDailyTreasureBoxProgress(
                    PlayerId, PlayerShard, userAssets.TimeZoneOffset);
                reply.dailyTreasureBoxInfo = userDailyProgress;
            }
            // 广告
            {
                reply.advertisementInfo = await context.ListUserMallAdStatus(PlayerShard, PlayerId);
            }
            // H5 拉取好友活动
            {
                if (serverConfigService.TryGetParameterInt(Params.H5FriendActivityUnlockLevel,
                        out var activityUnlockLevel))
                {
                    var (created, activityInfo) =
                        await context.GetOrCreateUserH5FriendActivityInfo(PlayerId, PlayerShard, activityUnlockLevel);
                    if (created)
                        needUpdateDatabase = true;
                    reply.userH5FriendActivityInfo = activityInfo;
                    if (TimeUtils.ShouldOldH5ActivityClose())
                        reply.userH5FriendActivityInfo.NextShowingLevel = 99999999;
                }
            }
            // asset同步
            needUpdateDatabase |= await userItemService.AutoConvertSurplusCardsToMagicCard(userAssets, GameVersion);
            reply.userAsset = userAssets;

            if (needUpdateDatabase)
            {
                await using var t = await context.Database.BeginTransactionAsync();
                await context.SaveChangesWithDefaultRetryAsync(false);
                var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                    newCardList, PlayerShard, PlayerId);
                if (reply.expiredBattlePassReward != null && reply.expiredBattlePassReward.AssetsChange != null)
                {
                    reply.expiredBattlePassReward.AssetsChange.AchievementChanges = achievements;
                }
                await t.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();
            }

            return Ok(reply);
        });
    }


    private async Task<(List<UserCard> newCardList, DivisionController.CheckDivisionRewardResponse?)> TryClaimDivisionReward(
        UserDivision userDivision,
        UserAssets userAsset,
        int currentSeason)
    {
        var newCardList = new List<UserCard>();
        var response = new DivisionController.CheckDivisionRewardResponse()
        {
            HaveReward = false,
            NotifyDivisionChange = false,
            LastDivisionRank = -1,
            LastEndlessRank = -1,
            LastWorldRank = -1
        };
        response.LastDivisionScore = userDivision.LastDivisionScore;
        if (userDivision.RewardReceived) return (newCardList, response);
        userDivision.RewardReceived = true;
        if (userDivision.LastSeasonNumber >= currentSeason)
            return (newCardList, response);
        response.NotifyDivisionChange = true;
        int historyMaxDivision = serverConfigService.GetDivisionByDivisionScore(userDivision.MaxDivisionScore);
        int currentDivision = serverConfigService.GetDivisionByDivisionScore(userDivision.DivisionScore);

        // 升段奖励 只会在第一次升到该段位时领取
        if (currentDivision > historyMaxDivision)
        {
            var divisionReward = serverConfigService.GetDivisionRewardConfig(currentDivision);
            if (divisionReward == null)
                return (newCardList, null);
            userAsset.CoinCount += divisionReward.coin;
            userAsset.DiamondCount += divisionReward.diamond;
            var reward = new GeneralReward()
            {
                ItemList = divisionReward.item_list.ToList(),
                CountList = divisionReward.count_list.ToList(),
            };
            if (reward.ItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
                if (result == null)
                    return (newCardList, null);
                newCardList.AddRange(cardList);
            }

            response.HaveReward = true;
            response.DivisionReward = reward;
            if (divisionReward.coin > 0)
            {
                response.DivisionReward.ItemList.Add((int)MoneyType.Coin);
                response.DivisionReward.CountList.Add(divisionReward.coin);
            }

            if (divisionReward.diamond > 0)
            {
                response.DivisionReward.ItemList.Add((int)MoneyType.Diamond);
                response.DivisionReward.CountList.Add(divisionReward.diamond);
            }
        }

        // 段位排行奖励
        if (userDivision.LastDivisionRank >= 0)
        {
            var division = serverConfigService.GetDivisionByDivisionScore(userDivision.LastDivisionScore);
            var rankReward = serverConfigService.GetRankRewardConfig(division, userDivision.LastDivisionRank);
            if (rankReward == null)
                return (newCardList, null);
            var rewardItemList = rankReward.item_list.ToList();
            var rewardCountList = rankReward.count_list.ToList();
            var reward = new GeneralReward()
            {
                ItemList = rankReward.item_list.ToList(),
                CountList = rankReward.count_list.ToList(),
            };
            if (rewardItemList.Count > 0)
            {
                var (cardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
                if (result == null)
                    return (newCardList, null);
                newCardList.AddRange(cardList);
            }

            response.LastDivisionRank = userDivision.LastDivisionRank;
            response.HaveReward = true;
            response.RankReward = new GeneralReward()
            {
                ItemList = rewardItemList,
                CountList = rewardCountList,
            };
        }

        // 世界排名
        var userInfo = await userInfoService.GetUserInfoAsync(userAsset.ShardId, userAsset.PlayerId);
        if (userInfo != null)
        {
            var index = userInfo.WorldRankSeasonHistories.IndexOf(userDivision.LastSeasonNumber);
            if (index >= 0)
            {
                response.LastWorldRank = userInfo.WorldRankHistories[index];
            }
        }

        return (newCardList, response);
    }

    [HttpPost]
    public async Task<ActionResult<bool>> SetUserCustomCardPoolByHeroId(int heroId, List<int> cardIds)
    {
        // 校验参数合法性
        var heroConfig = serverConfigService.GetHeroConfigById(heroId);
        if (heroConfig == null)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());
        
        var cardPool = await context.GetUserCustomCardPoolAsync(PlayerShard, PlayerId, heroId);
        var extraSlot = cardPool == null ? 0 : cardPool.ExtraSlotCount;
        
        // 首先校验长度
        if (serverConfigService.TryGetParameterInt(Params.EditableCardCount, out var editableCardCount))
        {
            if (cardIds.Count > editableCardCount + extraSlot)
                return BadRequest(ErrorKind.ARG_OUT_OF_RANGE.Response());
        }

        // 校验下卡牌是否存在
        foreach (int cardId in cardIds)
        {
            // 允许使用-1表示空槽位
            if (cardId < 0)
                continue;
            var itemConfig = serverConfigService.GetItemConfigById(cardId);
            if (itemConfig == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
            if (itemConfig.type != ItemType.SoldierCard && itemConfig.type != ItemType.TowerCard)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());
        }

        if (cardPool != null)
        {
            cardPool.CardList = cardIds;
        }
        else
        {
            cardPool = new UserCustomCardPool()
            {
                HeroId = heroId,
                PlayerId = PlayerId,
                ShardId = PlayerShard,
                CardList = cardIds,
                ExtraSlotCount = 0,
            };
            context.AddUserCustomCardPool(cardPool);
        }

        await context.SaveChangesWithDefaultRetryAsync();
        return Ok(true);
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult?>> ClaimFixedMapStarReward(int rewardIdx)
    {
        return await context.WithRCUDefaultRetry<ActionResult<TakeRewardResult?>>(async _ =>
        {
            var starRewardInfo = await context.GetOrAddUserStarRewardClaimStatusAsync(PlayerId, PlayerShard);
            var starRewardClaimStatus = starRewardInfo.StarRewardClaimStatus;
            if (starRewardClaimStatus.Count > rewardIdx / 64 &&
                starRewardClaimStatus[rewardIdx / 64].GetNthBits(rewardIdx % 64))
                return BadRequest(ErrorKind.ALREADY_TOOK_STORY_STAR_REWARD.Response());
            var rewardConfig = serverConfigService.GetFixedMapStarRewardConfigs(rewardIdx);
            if (rewardConfig == null)
                return BadRequest(ErrorKind.ARG_OUT_OF_RANGE.Response());
            var totalStarCount = await context.GetTotalStoryStarCount(PlayerId, PlayerShard);
            if (rewardConfig.star_required > totalStarCount)
                return BadRequest(ErrorKind.NOT_READY_FOR_CLAIM.Response());

            while (starRewardClaimStatus.Count <= rewardIdx / 64)
                starRewardClaimStatus.Add(0);
            var claimState = starRewardClaimStatus[rewardIdx / 64];
            starRewardClaimStatus[rewardIdx / 64] = claimState.SetNthBits(rewardIdx % 64, true);

            var generalReward = new GeneralReward()
            {
                ItemList = rewardConfig.item_list.ToList(),
                CountList = rewardConfig.count_list.ToList(),
            };

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG);

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(result);
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserDailyTreasureBoxProgress>> GetUserDailyTreasureBoxProgress()
    {
        var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
        if (timeZoneOffset is null)
            return BadRequest(ErrorKind.NO_USER_ASSET);
        var userDailyProgress = await gameService.GetAndRefreshUserDailyTreasureBoxProgress(
            PlayerId, PlayerShard, timeZoneOffset.Value);
        return userDailyProgress;
    }

    public record struct ClaimDailyTreasureBoxRewardResult(
        UserDailyTreasureBoxProgress Progress,
        TakeRewardResult RewardResult);

    [HttpPost]
    public async Task<ActionResult<ClaimDailyTreasureBoxRewardResult>> ClaimDailyTreasureBoxReward()
    {
        return await context.WithRCUDefaultRetry<ActionResult<ClaimDailyTreasureBoxRewardResult>>(async _ =>
        {
            // 每日奖励只会发放宝箱，这里只include宝箱
            var userAssets = await userAssetService.GetUserAssetsWithTreasureBoxAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(ErrorKind.NO_USER_ASSET);
            var userDailyProgress = await gameService.GetAndRefreshUserDailyTreasureBoxProgress(
                PlayerId, PlayerShard, userAssets.TimeZoneOffset);

            var division
                = await divisionService.GetDivisionNumberAsync(PlayerShard, PlayerId,
                    CreateOptions.DoNotCreateWhenNotExists);
            var divisionConfig = serverConfigService.GetDivisionConfig(division);
            // 检查是不是可以领奖
            var progressReward = divisionConfig.daily_progress_reward;
            List<int> rewardList = [];
            List<int> countList = [];
            int iterCount = Math.Min(userDailyProgress.Progress, progressReward.Length);
            for (int i = 0; i < iterCount; i++)
            {
                var reward = progressReward[i];
                if (reward < 0) // < 0说明没有奖励
                    continue;
                // 检查是否已领取过奖励
                if (userDailyProgress.RewardClaimStatus.GetNthBits(i))
                    continue;
                userDailyProgress.RewardClaimStatus = userDailyProgress.RewardClaimStatus.SetNthBits(i, true);
                rewardList.Add(reward);
                countList.Add(1);
            }

            if (rewardList.IsNullOrEmpty())
                return BadRequest(ErrorKind.NO_DAILY_TREASURE_BOX_REWARD.Response());
            var generalReward = new GeneralReward() { ItemList = rewardList, CountList = countList };
            var (newCardList, result) = await userItemService.TakeReward(userAssets, generalReward, GameVersion);
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

            return Ok(new ClaimDailyTreasureBoxRewardResult() { Progress = userDailyProgress, RewardResult = result });
        });
    }
}