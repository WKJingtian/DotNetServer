using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class UserInfoController(
    IConfiguration configuration,
    ILogger<UserInfoController> logger,
    UserItemService userItemService,
    ServerConfigService serverConfigService,
    BuildingGameDB context,
    LeaderboardModule leaderboardModule,
    PlayerModule playerModule,
    UserRankService userRankService,
    DivisionService divisionService,
    SeasonService seasonService,
    UserInfoService userInfoService,
    UserAssetService userAssetService,
    GameService gameService,
    IapPackageService iapPackageService,
    UserAchievementService userAchievementService)
    : BaseApiController(configuration)
{
    [HttpPost]
    public async Task<ActionResult<UserInfo>> FetchUserInfo()
    {
        var user = await userInfoService.GetUserInfoWithHistoriesAsync(PlayerShard, PlayerId, GameVersion);
        if (user is null)
        {
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        }

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserInfo>> GetUserInfoDetailedById(long playerId)
    {
        var user = await userInfoService.GetUserInfoWithHistoriesAsync(null, playerId, GameVersion);
        if (user is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS, });
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<int>> GetUserAvatarFrame(long playerId)
    {
        var avatarFrameItemId = await userInfoService.GetAvatarFrameItemIDAsync(null, playerId);
        if (avatarFrameItemId is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS, });
        return Ok(avatarFrameItemId);
    }

    [HttpPost]
    public async Task<ActionResult<int>> GetUserNameCard(long playerId)
    {
        var nameCardItemId = await userInfoService.GetNameCardItemIDAsync(null, playerId);
        if (nameCardItemId is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS, });
        return Ok(nameCardItemId);
    }

    [HttpPost]
    public async Task<ActionResult<bool>> UpdateAvatarFrame(int frameItemId)
    {
        bool hasFrameItem = frameItemId == 0;
        if (!hasFrameItem)
            hasFrameItem = await userItemService.HasItemAsync(PlayerShard, PlayerId, frameItemId);
        if (!hasFrameItem)
            return BadRequest(ErrorKind.NO_USER_ITEM.Response());
        var result = await userInfoService.UpdateAvatarFrameItemIDAsync(PlayerShard, PlayerId, frameItemId);
        if (result != ErrorKind.SUCCESS)
            return BadRequest(result.Response());
        return Ok(true);
    }

    [HttpPost]
    public async Task<ActionResult<bool>> UpdateNameCard(int nameCardItemId)
    {
        bool hasNameCard = nameCardItemId == 0;
        if (!hasNameCard)
            hasNameCard = await userItemService.HasItemAsync(PlayerShard, PlayerId, nameCardItemId);
        if (!hasNameCard)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ITEM });
        var result = await userInfoService.UpdateNameCardItemIDAsync(PlayerShard, PlayerId, nameCardItemId);
        if (result != ErrorKind.SUCCESS)
            return BadRequest(result.Response());
        return Ok(true);
    }

    [HttpPost]
    // 参数名没对上，但已经上线了，先这样吧
    public async Task<ActionResult<bool>> UpdateIdleReward(int nameCardItemId)
    {
        return await context.WithRCUDefaultRetry<ActionResult<bool>>(async _ =>
        {
            if (nameCardItemId > 0)
            {
                bool hasIdleBoxItem = await userItemService.HasItemAsync(PlayerShard, PlayerId, nameCardItemId);
                if (!hasIdleBoxItem)
                    return BadRequest(ErrorKind.NO_USER_ITEM.Response());
            }

            var idleRewardInfo = await context.GetUserIdleRewardInfo(PlayerShard, PlayerId);
            if (idleRewardInfo is null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            idleRewardInfo.IdleRewardId = nameCardItemId;
            await context.SaveChangesWithDefaultRetryAsync();
            return Ok(true);
        });
    }

    public record struct UserInfoOverall(
        UserInfo UserInfo,
        int Level,
        int LevelScore,
        int CurrentDivision,
        int DivisionRank,
        int WorldRank,
        int TotalStarCount);

    public record struct UserRankInfo(int DivisionRank, int WorldRank);

    [HttpPost]
    public async Task<ActionResult<UserInfoOverall>> GetUserInfoOverallById(long playerId)
    {
        // ================== 该方法已过期，将在1.0.0版本发布之后被删除 =========================
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var userInfo = await userInfoService.GetUserInfoWithHistoriesAsync(shardId, playerId, GameVersion);
        if (userInfo == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        var levelData = await userAssetService.GetLevelDataAsync(shardId.Value, playerId);
        if (levelData is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        int division = await divisionService.GetDivisionNumberAsync(shardId.Value, playerId, CreateOptions.DoNotCreateWhenNotExists);
        var rankInfo = new UserRankInfo();
        var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
        rankInfo.DivisionRank = await userRankService.GetUserDivisionRankAsync(shardId.Value, playerId, seasonNumber);
        rankInfo.WorldRank = await leaderboardModule.GetPlayerRank(playerId, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);

        int realRank = rankInfo.DivisionRank;
        if (realRank >= 0 && division > 0)
        {
            var divisionConf = serverConfigService.GetDivisionConfig(division);
            var rankDivisionInfo = await userRankService.GetCurrentSeasonUserRankByDivisionAsync(shardId.Value, playerId, division);
            if (rankDivisionInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            long selfScore = rankDivisionInfo.HighestScore;
            var prngForScore = new System.Random(division * 1000000 + (int)rankDivisionInfo.GroupId);
            int robotCount = divisionConf.max_population - divisionConf.population;
            for (int robotIdx = 0; robotIdx < robotCount; robotIdx++)
            {
                long scoreToAdd = prngForScore.Next(divisionConf.robot_score_min, divisionConf.robot_score_max);
                if (scoreToAdd > selfScore)
                    ++realRank;
            }
        }

        var fixedLevelMapProgresses = await context.GetAllUserFixedLevelMapProgresses(playerId, shardId.Value);
        int starCount = 0;
        foreach (var fixedLevelMapProgress in fixedLevelMapProgresses)
            starCount += fixedLevelMapProgress.StarCount;

        return Ok(new UserInfoOverall
        {
            UserInfo = userInfo,
            Level = levelData.Level,
            LevelScore = levelData.LevelScore,
            CurrentDivision = division,
            DivisionRank = realRank,//rankInfo.DivisionRank,
            WorldRank = rankInfo.WorldRank,
            TotalStarCount = starCount,
        });
    }

    public record struct UserInfoOverall_V1_0_0(
        UserInfo UserInfo,
        int Level,
        int LevelScore,
        int CurrentDivision,
        int WorldRank,
        int TotalStarCount);

    [HttpPost]
    public async Task<ActionResult<UserInfoOverall_V1_0_0>> GetUserInfoOverallById_V_1_0_0(long playerId)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var userInfo = await userInfoService.GetUserInfoWithHistoriesAsync(shardId, playerId, GameVersion);
        if (userInfo == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        var levelData = await userAssetService.GetLevelDataAsync(shardId.Value, playerId);
        if (levelData is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        int division = await divisionService.GetDivisionNumberAsync(shardId.Value, playerId, CreateOptions.DoNotCreateWhenNotExists);
        var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
        var worldRank = await leaderboardModule.GetPlayerRank(playerId, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);

        var fixedLevelMapProgresses = await context.GetAllUserFixedLevelMapProgresses(playerId, shardId.Value);
        int starCount = 0;
        foreach (var fixedLevelMapProgress in fixedLevelMapProgresses)
            starCount += fixedLevelMapProgress.StarCount;

        return Ok(new UserInfoOverall_V1_0_0
        {
            UserInfo = userInfo,
            Level = levelData.Level,
            LevelScore = levelData.LevelScore,
            CurrentDivision = division,
            WorldRank = worldRank,
            TotalStarCount = starCount,
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserInfo>> GetUserInfoById(long playerId)
    {
        var userInfo = await userInfoService.GetUserInfoWithHistoriesAsync(null, playerId, GameVersion);
        if (userInfo == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        return Ok(userInfo);
    }

    // TODO: 这个接口是什么时候使用的？参数长度未经校验，伪造请求可能对数据库产生巨大压力
    [HttpPost]
    public async Task<ActionResult<List<UserInfo>>> BatchGetUserInfoById(List<long> playerIdList)
    {
        var result = await userInfoService.BatchGetUserInfosByPlayerIdsAsync(playerIdList);
        return Ok(result.Values.ToList());
    }

    public record struct AddUserInfoArg(string Name, string Signature, int IconIndex);

    [HttpPost]
    public async Task<ActionResult<UserInfo>> AddUserInfo(int timeZoneOffset)
    {
        // 已存在的话直接返回得了
        var userInfo = await userInfoService.GetUserInfoAsync(PlayerShard, PlayerId);
        if (userInfo is not null)
            return Ok(userInfo);

        if (!TimeUtils.IsValidTimeZoneOffset(timeZoneOffset))
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_INPUT });

        try
        {
            var newUser = await context.WithRCUDefaultRetry(async _ =>
            {
                var newUser = userInfoService.CreateNewUserInfo(new UserInfo
                {
                    Signature = ":-)",
                    ShardId = PlayerShard,
                    PlayerId = PlayerId,
                    UserId = UserId,
                    HideHistory = false,
                });
                // 创建资产条目
                var (result, userAsset) = await userItemService.CreateDefaultUserAssets(PlayerShard, PlayerId, timeZoneOffset, GameVersion);
                if (result == null)
                    throw new Exception("Create Default User Asset Failed");
                userAssetService.AddUserAssetAsync(userAsset);
                // 创建开宝箱相关数据
                gameService.AddUserGameInfoById(PlayerShard, PlayerId);
                // 创建推广相关数据
                iapPackageService.AddPromotionData(PlayerId, PlayerShard);
                // 创建聚宝信息
                context.AddUserIdleRewardInfo(PlayerShard, PlayerId);
                // 创建签到数据
                context.CreateUserAttendanceRecord(PlayerShard, PlayerId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                userAssetService.DetachUserAssetCards(userAsset);
                userAssetService.DetachUserAssetItems(userAsset);

                await using var transaction = await context.Database.BeginTransactionAsync();
                await context.SaveChangesAsync(false);
                // 卡牌变更 upsert
                await userAssetService.UpsertUserCardsAsync(result.CardChangeSet);
                // 物品变更 upsert
                await userAssetService.UpsertUserItemsAsync(result.ItemChangeSet);
                // 成就项
                await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(result.NewCardList, PlayerShard, PlayerId);
                await transaction.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();
                return newUser;
            });

            return Ok(newUser);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }
    }

    public record struct ChangeSignatureArg(string Signature);

    public record struct ChangeSignatureReply(string Signature, bool Senstive, bool LengthLimit);

    [HttpPost]
    public async Task<ActionResult<ChangeSignatureReply>> ChangeSignature([FromBody] ChangeSignatureArg arg)
    {
        var (isSenstive, maskedSignature, exceedLengthLimit, result) = await userInfoService.UpdateSignatureAsync(PlayerShard, PlayerId, arg.Signature);
        if (result != ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)result });

        return Ok(new ChangeSignatureReply()
        {
            Signature = maskedSignature,
            Senstive = isSenstive,
            LengthLimit = exceedLengthLimit
        });
    }

    [HttpPost]
    public async Task<ActionResult<bool>> ChangeHideHistory(bool hide)
    {
        var result = await userInfoService.UpdateHideHistoryAsync(PlayerShard, PlayerId, hide);
        if (result != ErrorKind.SUCCESS)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)result });
        return Ok(hide);
    }
}