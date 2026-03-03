using System.Reflection;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using ChillyRoom.PayService;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Services;
using GameOutside.Services.KafkaConsumers;
using GameOutside.Util;
using GenericPlayerManagementService.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class GmController(
    IConfiguration configuration,
    ILogger<GmController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    PlayerModule playerModule,
    IServiceProvider serviceProvider,
    UserRankService userRankService,
    UserEndlessRankService userEndlessRankService,
    DivisionService divisionService,
    UserInfoService userInfoService,
    ExportGameDataService exportGameDataService,
    BattlePassService battlePassService,
    UserAssetService userAssetService,
    SeasonService seasonService,
    LeaderboardModule leaderboardModule,
    UserAchievementService userAchievementService,
    PlayerBanner playerBanner)
    : BaseApiController(configuration)
{
    private PayEventHandler PayEventHandler
        => serviceProvider.GetServices<IHostedService>().OfType<PayEventHandler>().Single();

    public record struct UserSpec(
        UserAssets? UserAssets,
        UserCustomData? UserCustomData,
        UserDivision? UserDivision,
        List<UserDailyStoreItem> UserDailyStoreItems,
        UserDailyStoreIndex? UserDailyStoreIndex,
        UserGameInfo? UserGameInfo,
        UserInfo? UserInfo,
        List<UserCommodityBoughtRecord> UserCommodityBoughtRecords,
        UserAttendance? UserAttendance,
        List<UserAchievement> UserAchievements,
        UserBeginnerTask? UserBeginnerTasks,
        List<UserBattlePassInfo> UserBattlePassInfos,
        List<UserCustomCardPool> UserCustomCardPools,
        List<UserFixedLevelMapProgress> UserFixedLevelMapProgresses,
        List<UserIapPurchaseRecord> UserIapPurchaseRecords,
        ActivityLuckyStar? ActivityLuckyStar,
        UserPaymentAndPromotionStatus? PromotionStatus,
        UserStarStoreInfo? UserStarStoreInfos,
        ActivityPiggyBank? ActivityPiggyBank,
        List<ActivityUnrivaledGod> ActivityUnrivaledGods,
        List<UserFortuneBagInfo> UserFortuneBags,
        MonthPassInfo? MonsMonthPassInfo,
        List<ActivityCoopBossInfo> ActivityCoopBossInfo,
        List<ActivityTreasureMaze> ActivityTreasureMazeInfo,
        UserDailyTask? UserDailyTask,
        UserIdleRewardInfo? UserIdleReward
    );

    [HttpGet]
    public async Task<ActionResult<string>> ExportPlayerInfo(long playerId)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var userInfo = await userInfoService.GetUserInfoWithHistoriesAsync(shardId.Value, playerId, "100.0.0");
        if (userInfo == null)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

        var userSpec = await exportGameDataService.ExportPlayerInfo(playerId, shardId.Value);
        return Ok(userSpec);
    }

    [HttpPost]
    public async Task<ActionResult<bool>> DeleteUserRecords(long playerId)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        exportGameDataService.DeleteUserRecords(playerId);
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(true);
    }

    [HttpPost]
    public async Task<ActionResult<bool>> SkipAllUserGuideSteps(long playerId)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        await context.SetUserIntData(shardId.Value, playerId,
            "13|-1|17|1|11|0|22|4|23|10|32|2|30|1|14|2|26|275|28|2|39|4|31|1|15|1|9|1|29|1");
        await context.SetUserStrData(shardId.Value, playerId,
            "21|0;1;27;1;31;1;35;1;36;1;40;1;8;1;18;1;45;1;54;1;49;1;50;1;52;1;17;1;55;1|16|0;1;1;0|27|80;1;81;1;82;1;83;1;84;1;85;1;86;1|38|2;1;1;1;0;1|34|1;1");
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(true);
    }

    [HttpPost]
    public async Task<ActionResult<int>> ChangeUserDiamondCount(long playerId, int diamond)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        var userAssets = await userAssetService.GetUserAssetsSimpleAsync(shardId.Value, playerId);
        if (userAssets == null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());
        userAssets.DiamondCount = diamond;
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(userAssets.DiamondCount);
    }

    [HttpPost]
    public async Task<ActionResult<int>> ChangeUserCoinCount(long playerId, int coin)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var shardId = await playerModule.GetPlayerShardId(playerId);
        var userAssets = await userAssetService.GetUserAssetsSimpleAsync(shardId, playerId);
        if (userAssets == null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());
        userAssets.CoinCount = coin;
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(userAssets.CoinCount);
    }

    [HttpPost]
    public async Task<ActionResult<UserLevelData>> AddUserExp(long playerId, int expAdd)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        var userAssets = await userAssetService.GetUserAssetsSimpleAsync(shardId.Value, playerId);
        if (userAssets == null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());
        await userItemService.AddExpAsync(userAssets, expAdd, GameVersion);
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(userAssets.LevelData);
    }


    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> AddItem(long playerId, int itemId, int itemCount)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var shardId = await playerModule.GetPlayerShardId(playerId);
        var userAssets = await userAssetService.GetUserAssetsDetailedAsync(shardId, playerId);
        if (userAssets == null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());

        var reward = new GeneralReward() { ItemList = [itemId], CountList = [itemCount] };
        var (newCardList, result) = await userItemService.TakeReward(userAssets, reward, GameVersion);
        if (result == null)
            return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

        await using var t = await context.Database.BeginTransactionAsync();
        await context.SaveChangesWithDefaultRetryAsync(false);
        var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList,
            shardId.Value, playerId);
        if (result.AssetsChange != null)
            result.AssetsChange.AchievementChanges.AddRange(achievements);
        await t.CommitAsync();
        context.ChangeTracker.AcceptAllChanges();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserBattlePassController.FetchBattlePassInfoResult>> AddBattleExp(
        long playerId,
        int expAdd)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        var userAssets = await userAssetService.GetUserAssetsSimpleAsync(shardId.Value, playerId);
        if (userAssets is null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());
        if (expAdd <= 0)
            return Ok(true);
        int activeBattlePassId = serverConfigService.GetActiveBattlePassId();
        if (activeBattlePassId == -1)
            return BadRequest(ErrorKind.NO_BATTLE_PASS_CONFIG.Response());
        var currentBattlePassInfo
            = await battlePassService.GetUserBattlePassInfoByPassIdAsync(PlayerShard, PlayerId, activeBattlePassId);
        currentBattlePassInfo ??= battlePassService.AddUserBattlePassInfo(PlayerShard, PlayerId, activeBattlePassId);
        var firstBattlePassInfo = await battlePassService.GetUserBattlePassInfoByPassIdAsync(PlayerShard, PlayerId, -1);
        firstBattlePassInfo ??= battlePassService.AddUserBattlePassInfo(PlayerShard, PlayerId, -1);
        currentBattlePassInfo.Exp += expAdd;
        firstBattlePassInfo.Exp += expAdd;

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok(new UserBattlePassController.FetchBattlePassInfoResult()
        {
            FirstPass = firstBattlePassInfo,
            BattlePass = currentBattlePassInfo
        });
    }

    // 反射去调用PayEventHandler中的函数
    [HttpPost]
    public async Task<ActionResult<long>> Purchase(long userId, long playerId, string payload, int quantity)
    {
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());

        MethodInfo? methodOnOrderPaid
            = typeof(PayEventHandler).GetMethod("OnOrderPaid", BindingFlags.NonPublic | BindingFlags.Instance);
        if (methodOnOrderPaid == null)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());

        // 测试用的，orderId暂时用时间戳
        long orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var orderStatusEvent = new OrderStatusEvent()
        {
            OrderStatus = 0,
            OrderId = orderId,
            UserId = userId,
            PlayerId = playerId,
            Payload = payload,
            Quantity = quantity,
            SkuType = 1,
        };

        var task = (ValueTask)methodOnOrderPaid.Invoke(PayEventHandler, [orderStatusEvent, CancellationToken.None])!;
        await task;
        return Ok(orderId);
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ObtainAllCard(long playerId)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var userAsset = await userAssetService.GetUserAssetsDetailedAsync(shardId.Value, playerId);
        if (userAsset is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

        List<int> itemList = new();
        List<int> countList = new();
        foreach (var itemConfig in serverConfigService.GetItemConfigList())
        {
            var type = itemConfig.type;
            if (type is ItemType.SoldierCard or ItemType.TowerCard or ItemType.BuildingCard)
            {
                itemList.Add(itemConfig.id);
                countList.Add(1);
            }
        }

        var generalReward = new GeneralReward() { ItemList = itemList, CountList = countList, };
        var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
        if (result == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

        await using var t = await context.Database.BeginTransactionAsync();
        await context.SaveChangesWithDefaultRetryAsync(false);
        var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList,
            shardId.Value, playerId);
        result.AssetsChange.AchievementChanges.AddRange(achievements);
        await t.CommitAsync();
        context.ChangeTracker.AcceptAllChanges();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserAssetsChange>> AddChestWithExplorePoints(long playerId)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        if (!serverConfigService.TryGetParameterInt(Params.LocalServerGmEnable, out var enabled))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (enabled == 0)
            return BadRequest(ErrorKind.LOCAL_GM_NOT_ENABLED.Response());
        var userAsset = await userAssetService.GetUserAssetsDetailedAsync(shardId.Value, playerId);
        if (userAsset is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

        var assetChangeRecorder = new UserAssetsChange();
        var treasureBox = new UserTreasureBox()
        {
            ShardId = shardId.Value,
            ItemId = 52032,
            PlayerId = playerId,
            ItemCount = 1,
            StarCount = 10,
        };
        List<UserTreasureBox> boxes = userAsset.UserTreasureBoxes;
        boxes.Add(treasureBox);
        assetChangeRecorder.TreasureBoxChange.AddList.Add(treasureBox);
        assetChangeRecorder.FillAssetInfo(userAsset);
        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }
        return Ok(assetChangeRecorder);
    }

    [HttpPost]
    public async Task<ActionResult<ExportedPlayerData>> ExportPlayerData(ExportPlayerDataRequest request)
    {
        var shardId = request.PlayerShard;
        var playerId = request.PlayerId;
        var playerData = new ExportedPlayerData
        {
            PlatformNotifies = await context.PlatformNotifies
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            PaidOrderWithShards = await context.PaidOrderWithShards
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserRanks = await context.UserRanks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserEndlessRanks = await context.UserEndlessRanks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDivision = await context.UserDivisions
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserInfo = await context.UserInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserAssets = await context.UserAssets
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserItems = await context.UserItems
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserCards = await context.UserCards
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserTreasureBoxes = await context.UserTreasureBoxes
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserGameInfos = await context.UserGameInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserCustomData = await context.UserCustomData
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserIdleRewardInfos = await context.UserIdleRewardInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserHistories = await context.UserHistories
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDailyStoreItems = await context.UserDailyStoreItems
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDailyStoreIndex = await context.UserDailyStoreIndices
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserCommodityBoughtRecords = await context.UserCommodityBoughtRecords
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserAttendances = await context.UserAttendances
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserBattlePassInfos = await context.UserBattlePassInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserAchievements = await context.UserAchievements
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserBeginnerTask = await context.UserBeginnerTasks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserDailyTasks = await context.UserDailyTasks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserMallAdvertisements = await context.UserMallAdvertisements
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserCustomCardPools = await context.UserCustomCardPools
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserFixedLevelMapProgress = await context.UserFixedLevelMapProgress
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserIapPurchases = await context.UserIapPurchases
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityLuckyStar = await context.ActivityLuckyStars
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            PromotionStatus = await context.PromotionStatus
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserStarStoreStatus = await context.UserStarStoreStatus
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivityPiggyBanks = await context.ActivityPiggyBanks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityUnrivaledGods = await context.ActivityUnrivaledGods
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserFortuneBagInfo = await context.UserFortuneBagInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivityTreasureMazeInfos = await context.ActivityTreasureMazeInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserMonthPassInfo = await context.UserMonthPassInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivityCoopBossInfoSet = await context.ActivityCoopBossInfoSet
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityEndlessChallenges = await context.ActivityEndlessChallenges
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDailyTreasureBoxProgress = await context.UserDailyTreasureBoxProgresses
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserH5FriendActivityInfo = await context.UserH5FriendActivityInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserEncryptionInfo = await context.UserEncryptionInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            IosGameCenterRewardInfo = await context.IosGameCenterRewardInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivitySlotMachineInfos = await context.ActivitySlotMachines
                .IgnoreQueryFilters()
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityOneShotKillInfo = await context.ActivityOneShotKills
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityTreasureHunts = await context.ActivityTreasureHunts.IgnoreQueryFilters()
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
            ActivityRpgGames = await context.ActivityRpgGames.Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
            ActivityLoogGames = await context.ActivityLoogGames.Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
            ActivityCsgoStyleLotteries = await context.ActivityCsgoStyleLotteryInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
        };

        // 清空关联数据，避免重复输出
        if (playerData.UserAssets != null)
        {
            playerData.UserAssets.UserCards = [];
            playerData.UserAssets.UserItems = [];
            playerData.UserAssets.UserTreasureBoxes = [];
        }
        if (playerData.UserInfo != null)
        {
            playerData.UserInfo.Histories = [];
        }

        return Ok(playerData);
    }

    // 线上谨慎使用！
    [HttpPost]
    public async Task<ActionResult> ImportPlayerData(ImportPlayerDataRequest request)
    {
        if (request.PlayerData == null)
        {
            return BadRequest(ErrorKind.INVALID_INPUT.Response());
        }

        // 清空关联数据，避免重复跟踪
        if (request.PlayerData.UserAssets != null)
        {
            request.PlayerData.UserAssets.UserCards = [];
            request.PlayerData.UserAssets.UserItems = [];
            request.PlayerData.UserAssets.UserTreasureBoxes = [];
        }
        if (request.PlayerData.UserInfo != null)
        {
            request.PlayerData.UserInfo.Histories = [];
        }

        // history 以 Id+ShardId 为主键，需要重新生成以避免冲突
        foreach (var history in request.PlayerData.UserHistories)
        {
            history.Id = Guid.NewGuid();
        }

        await DeletePlayerDataAsync(request.PlayerShard, request.PlayerId);
        context.ChangeTracker.AcceptAllChanges();

        ReplaceWithNewShardIdPlayerId(request.PlayerData, request.PlayerShard, request.PlayerId);

        context.PlatformNotifies.AddRange(request.PlayerData.PlatformNotifies);
        context.PaidOrderWithShards.AddRange(request.PlayerData.PaidOrderWithShards);
        context.UserRanks.AddRange(request.PlayerData.UserRanks);
        context.UserEndlessRanks.AddRange(request.PlayerData.UserEndlessRanks);

        if (request.PlayerData.UserDivision != null)
            context.UserDivisions.Add(request.PlayerData.UserDivision);
        if (request.PlayerData.UserInfo != null)
            context.UserInfos.Add(request.PlayerData.UserInfo);
        if (request.PlayerData.UserAssets != null)
            context.UserAssets.Add(request.PlayerData.UserAssets);

        context.UserItems.AddRange(request.PlayerData.UserItems);
        context.UserCards.AddRange(request.PlayerData.UserCards);
        context.UserTreasureBoxes.AddRange(request.PlayerData.UserTreasureBoxes);
        context.UserGameInfos.AddRange(request.PlayerData.UserGameInfos);

        if (request.PlayerData.UserCustomData != null)
            context.UserCustomData.Add(request.PlayerData.UserCustomData);
        if (request.PlayerData.UserIdleRewardInfos != null)
            context.UserIdleRewardInfos.Add(request.PlayerData.UserIdleRewardInfos);

        context.UserHistories.AddRange(request.PlayerData.UserHistories);
        context.UserDailyStoreItems.AddRange(request.PlayerData.UserDailyStoreItems);

        if (request.PlayerData.UserDailyStoreIndex != null)
            context.UserDailyStoreIndices.Add(request.PlayerData.UserDailyStoreIndex);

        context.UserCommodityBoughtRecords.AddRange(request.PlayerData.UserCommodityBoughtRecords);

        if (request.PlayerData.UserAttendances != null)
            context.UserAttendances.Add(request.PlayerData.UserAttendances);

        context.UserBattlePassInfos.AddRange(request.PlayerData.UserBattlePassInfos);
        context.UserAchievements.AddRange(request.PlayerData.UserAchievements);

        if (request.PlayerData.UserBeginnerTask != null)
            context.UserBeginnerTasks.Add(request.PlayerData.UserBeginnerTask);
        if (request.PlayerData.UserDailyTasks != null)
            context.UserDailyTasks.Add(request.PlayerData.UserDailyTasks);

        context.UserMallAdvertisements.AddRange(request.PlayerData.UserMallAdvertisements);
        context.UserCustomCardPools.AddRange(request.PlayerData.UserCustomCardPools);
        context.UserFixedLevelMapProgress.AddRange(request.PlayerData.UserFixedLevelMapProgress);
        context.UserIapPurchases.AddRange(request.PlayerData.UserIapPurchases);

        if (request.PlayerData.ActivityLuckyStar != null)
            context.ActivityLuckyStars.Add(request.PlayerData.ActivityLuckyStar);
        if (request.PlayerData.PromotionStatus != null)
            context.PromotionStatus.Add(request.PlayerData.PromotionStatus);
        if (request.PlayerData.UserStarStoreStatus != null)
            context.UserStarStoreStatus.Add(request.PlayerData.UserStarStoreStatus);

        context.ActivityPiggyBanks.AddRange(request.PlayerData.ActivityPiggyBanks);
        context.ActivityUnrivaledGods.AddRange(request.PlayerData.ActivityUnrivaledGods);

        if (request.PlayerData.UserFortuneBagInfo != null)
            context.UserFortuneBagInfos.Add(request.PlayerData.UserFortuneBagInfo);

        context.ActivityTreasureMazeInfos.AddRange(request.PlayerData.ActivityTreasureMazeInfos);

        if (request.PlayerData.UserMonthPassInfo != null)
            context.UserMonthPassInfos.Add(request.PlayerData.UserMonthPassInfo);

        context.ActivityCoopBossInfoSet.AddRange(request.PlayerData.ActivityCoopBossInfoSet);
        context.ActivityEndlessChallenges.AddRange(request.PlayerData.ActivityEndlessChallenges);

        if (request.PlayerData.UserDailyTreasureBoxProgress != null)
            context.UserDailyTreasureBoxProgresses.Add(request.PlayerData.UserDailyTreasureBoxProgress);
        if (request.PlayerData.UserH5FriendActivityInfo != null)
            context.UserH5FriendActivityInfos.Add(request.PlayerData.UserH5FriendActivityInfo);
        if (request.PlayerData.UserEncryptionInfo != null)
            context.UserEncryptionInfos.Add(request.PlayerData.UserEncryptionInfo);
        if (request.PlayerData.IosGameCenterRewardInfo != null)
            context.IosGameCenterRewardInfos.Add(request.PlayerData.IosGameCenterRewardInfo);

        context.ActivitySlotMachines.AddRange(request.PlayerData.ActivitySlotMachineInfos);
        context.ActivityOneShotKills.AddRange(request.PlayerData.ActivityOneShotKillInfo);
        context.ActivityTreasureHunts.AddRange(request.PlayerData.ActivityTreasureHunts);
        context.ActivityRpgGames.AddRange(request.PlayerData.ActivityRpgGames);
        context.ActivityLoogGames.AddRange(request.PlayerData.ActivityLoogGames);
        context.ActivityCsgoStyleLotteryInfos.AddRange(request.PlayerData.ActivityCsgoStyleLotteries);

        try
        {
            await context.SaveChangesWithDefaultRetryAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }

        return Ok();
    }

    private void ReplaceWithNewShardIdPlayerId(ExportedPlayerData data, short newShardId, long newPlayerId)
    {
        foreach (var prop in data.GetType().GetProperties())
        {
            var value = prop.GetValue(data);
            if (value is null)
                continue;

            if (value is IEnumerable<object> list)
            {
                foreach (var item in list)
                {
                    var shardIdProp = item.GetType().GetProperty("ShardId");
                    if (shardIdProp != null)
                    {
                        shardIdProp.SetValue(item, newShardId);
                    }
                    var playerIdProp = item.GetType().GetProperty("PlayerId");
                    if (playerIdProp != null)
                    {
                        playerIdProp.SetValue(item, newPlayerId);
                    }
                }
            }
            else
            {
                var shardIdProp = value.GetType().GetProperty("ShardId");
                if (shardIdProp != null)
                {
                    shardIdProp.SetValue(value, newShardId);
                }
                var playerIdProp = value.GetType().GetProperty("PlayerId");
                if (playerIdProp != null)
                {
                    playerIdProp.SetValue(value, newPlayerId);
                }
            }
        }
    }

    private async Task DeletePlayerDataAsync(short shardId, long playerId)
    {
        await context.PlatformNotifies.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.PaidOrderWithShards.Where(p => p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserRanks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserEndlessRanks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserDivisions.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserAssets.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserItems.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserCards.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserTreasureBoxes.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserGameInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserCustomData.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserIdleRewardInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserHistories.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserDailyStoreItems.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserDailyStoreIndices.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserCommodityBoughtRecords.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserAttendances.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserBattlePassInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserAchievements.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserBeginnerTasks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserDailyTasks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserMallAdvertisements.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserCustomCardPools.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserFixedLevelMapProgress.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserIapPurchases.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityLuckyStars.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.PromotionStatus.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserStarStoreStatus.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityPiggyBanks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityUnrivaledGods.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserFortuneBagInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityTreasureMazeInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserMonthPassInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityCoopBossInfoSet.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityEndlessChallenges.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserDailyTreasureBoxProgresses.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserH5FriendActivityInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.UserEncryptionInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.IosGameCenterRewardInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivitySlotMachines.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await context.ActivityOneShotKills.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
    }

    private int[] Wanxiangfu = [90001, 90002, 90003, 90004];

    [HttpPost]
    public async Task<ActionResult<long>> BanAndClearPlayerData(BanAndClearPlayerDataRequest request)
    {
        short shardId = request.PlayerShard;
        long playerId = request.PlayerId;

        if (request.BanDurationSeconds.HasValue && request.BanDurationSeconds.Value > 0)
        {
            await playerBanner.BanPlayerAsync(
                taskId: Guid.NewGuid().ToString(),
                playerId: playerId,
                banReason: request.BanReason ?? string.Empty,
                durationSeconds: request.BanDurationSeconds.Value,
                rules: []
            );
        }

        return await context.WithRCUDefaultRetry<ActionResult<long>>(async _ =>
        {
            // 正常榜单
            if (request.ClearNormalRank)
            {
                var divisionNumber
                    = await divisionService.GetDivisionNumberAsync(shardId, playerId,
                        CreateOptions.DoNotCreateWhenNotExists);
                var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(divisionNumber);
                // 移除UserRank数据
                await userRankService.RemoveUserRankAsync(shardId, playerId, seasonNumber);
                // 移除redis数据
                await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.NormalModeLeaderBoardId,
                    seasonNumber);
            }

            // 娱乐模式榜单
            bool clearSurvivorRank = request.ClearSurvivorRank;
            bool clearTowerDefenceRank = request.ClearTowerDefenceRank;
            bool clearTrueEndlessRank = request.ClearTrueEndlessRank;

            if (clearSurvivorRank || clearTowerDefenceRank || clearTrueEndlessRank)
            {
                var endlessRank = await userEndlessRankService.GetCurrentSeasonUserEndlessRankAsync(shardId, playerId);
                if (endlessRank != null)
                {
                    int currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                    if (clearSurvivorRank)
                    {
                        endlessRank.SurvivorScore = 0;
                        await leaderboardModule.RemovePlayerFromLeaderBoard(playerId,
                            LeaderboardModule.SurvivorModeLeaderBoardId, currentSeasonNumber);
                    }

                    if (clearTowerDefenceRank)
                    {
                        endlessRank.TowerDefenceScore = 0;
                        await leaderboardModule.RemovePlayerFromLeaderBoard(playerId,
                            LeaderboardModule.TowerDefenceModeLeaderBoardId, currentSeasonNumber);
                    }

                    if (clearTrueEndlessRank)
                    {
                        endlessRank.TrueEndlessScore = 0;
                        await leaderboardModule.RemovePlayerFromLeaderBoard(playerId,
                            LeaderboardModule.TrueEndlessModeLeaderBoardId, currentSeasonNumber);
                    }
                }
            }

            // 清理资源
            if (request.ClearAssets)
            {
                var userAssets = await userAssetService.GetUserAssetsByIncludeOptionAsync(shardId, playerId, UserAssetIncludeOptions.IncludeItems);
                if (userAssets is not null)
                {
                    userAssets.CoinCount = 0;
                    if (userAssets.DiamondCount > 0)
                    {
                        userAssets.DiamondCount = 0;
                    }

                    context.UserItems.RemoveRange(userAssets.UserItems.Where(item => Wanxiangfu.Contains(item.ItemId)));
                }
            }

            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(playerId);
        });
    }


    public class PlayerInfo
    {
        public long PlayerId { get; set; }
        public short ShardId { get; set; }
    }

    /// <summary>
    /// 重置玩家分数
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<bool>> ResetPlayerRankScore(List<PlayerInfo> players)
    {
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        return await context.WithRCUDefaultRetry<ActionResult<bool>>(async _ =>
        {
            foreach (var player in players)
            {
                var pid = player.PlayerId;
                var shardId = player.ShardId;
                var division
                    = await divisionService.GetDivisionNumberAsync(shardId, pid, CreateOptions.DoNotCreateWhenNotExists);
                // 需要兼容青铜段位的情况
                int seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
                var userRank = await userRankService.GetCurrentSeasonUserRankByDivisionAsync(shardId, pid, division);
                if (userRank == null)
                    continue;
                if (userRank.HighestScore != int.MaxValue)
                {
                    logger.LogInformation($"[ResetPlayerRankScore] Player {player.PlayerId} does not have highest score Int.Max, skipped");
                    continue;
                }
                var now = DateTime.UtcNow;
                var time1120 = new DateTime(2025, 11, 24, 3, 20, 0, DateTimeKind.Utc);
                if (userRank.UpdatedAt < time1120)
                {
                    // 如果玩家最高分是在1120之前打出来的，也不处理
                    logger.LogInformation($"[ResetPlayerRankScore] Player {player.PlayerId} Highest score before 11:20, skipped");
                    continue;
                }

                var diffSeconds = (int)(now - time1120).TotalSeconds;
                var oldUserRank = await context.WithDefaultRetry(_ => context.UserRanks.AsOfSystemTime($"-{diffSeconds}s").AsNoTracking().Where(rank =>
                        rank.PlayerId == pid && rank.ShardId == shardId && rank.SeasonNumber == seasonNumber)
                    .FirstOrDefaultAsync());

                if (oldUserRank == null || oldUserRank.HighestScore <= 0)
                {
                    // 移除UserRank数据
                    await userRankService.RemoveUserRankAsync(shardId, pid, seasonNumber);
                    // 移除redis数据
                    await leaderboardModule.RemovePlayerFromLeaderBoard(pid, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);
                    logger.LogInformation($"[ResetPlayerRankScore] Player {player.PlayerId} Dose Not Have Old Rank Score, Remove From leaderBoard");
                }
                else
                {
                    userRank.HighestScore = oldUserRank.HighestScore;
                    await leaderboardModule.UpdateScore(pid, oldUserRank.HighestScore, userRank.HighestScore,
                        oldUserRank.Timestamp, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);
                    logger.LogInformation($"[ResetPlayerRankScore] Player {player.PlayerId} Rank Score Changed: Now Is {oldUserRank.HighestScore}");
                }
            }

            await context.SaveChangesWithDefaultRetryAsync();
            return Ok(true);
        });
    }
}

public class ImportPlayerDataRequest
{
    public short PlayerShard { get; set; }
    public long PlayerId { get; set; }
    public ExportedPlayerData? PlayerData { get; set; }
}

public class ExportedPlayerData
{
    public List<PlatformNotify> PlatformNotifies { get; set; } = [];
    public List<PaidOrderWithShard> PaidOrderWithShards { get; set; } = [];
    public List<UserRank> UserRanks { get; set; } = [];
    public List<UserEndlessRank> UserEndlessRanks { get; set; } = [];
    public UserDivision? UserDivision { get; set; }
    public UserInfo? UserInfo { get; set; }
    public UserAssets? UserAssets { get; set; }
    public List<UserItem> UserItems { get; set; } = [];
    public List<UserCard> UserCards { get; set; } = [];
    public List<UserTreasureBox> UserTreasureBoxes { get; set; } = [];
    public List<UserGameInfo> UserGameInfos { get; set; } = [];
    public UserCustomData? UserCustomData { get; set; }
    public UserIdleRewardInfo? UserIdleRewardInfos { get; set; }
    public List<UserHistory> UserHistories { get; set; } = [];
    public List<UserDailyStoreItem> UserDailyStoreItems { get; set; } = [];
    public UserDailyStoreIndex? UserDailyStoreIndex { get; set; }
    public List<UserCommodityBoughtRecord> UserCommodityBoughtRecords { get; set; } = [];
    public UserAttendance? UserAttendances { get; set; }
    public List<UserBattlePassInfo> UserBattlePassInfos { get; set; } = [];
    public List<UserAchievement> UserAchievements { get; set; } = [];
    public UserBeginnerTask? UserBeginnerTask { get; set; }
    public UserDailyTask? UserDailyTasks { get; set; }
    public List<UserMallAdvertisement> UserMallAdvertisements { get; set; } = [];
    public List<UserCustomCardPool> UserCustomCardPools { get; set; } = [];
    public List<UserFixedLevelMapProgress> UserFixedLevelMapProgress { get; set; } = [];
    public List<UserIapPurchaseRecord> UserIapPurchases { get; set; } = [];
    public ActivityLuckyStar? ActivityLuckyStar { get; set; }
    public UserPaymentAndPromotionStatus? PromotionStatus { get; set; }
    public UserStarStoreInfo? UserStarStoreStatus { get; set; }
    public List<ActivityPiggyBank> ActivityPiggyBanks { get; set; } = [];
    public List<ActivityUnrivaledGod> ActivityUnrivaledGods { get; set; } = [];
    public UserFortuneBagInfo? UserFortuneBagInfo { get; set; }
    public List<ActivityTreasureMaze> ActivityTreasureMazeInfos { get; set; } = [];
    public MonthPassInfo? UserMonthPassInfo { get; set; }
    public List<ActivityCoopBossInfo> ActivityCoopBossInfoSet { get; set; } = [];
    public List<ActivityEndlessChallenge> ActivityEndlessChallenges { get; set; } = [];
    public UserDailyTreasureBoxProgress? UserDailyTreasureBoxProgress { get; set; }
    public UserH5FriendActivityInfo? UserH5FriendActivityInfo { get; set; }
    public UserEncryptionInfo? UserEncryptionInfo { get; set; }
    public IosGameCenterRewardInfo? IosGameCenterRewardInfo { get; set; }
    public List<ActivitySlotMachine> ActivitySlotMachineInfos { get; set; } = [];
    public List<ActivityOneShotKill> ActivityOneShotKillInfo { get; set; } = [];
    public List<ActivityTreasureHunt> ActivityTreasureHunts { get; set; } = [];
    public List<ActivityRpgGame> ActivityRpgGames { get; set; } = [];
    public List<ActivityLoogGame> ActivityLoogGames { get; set; } = [];
    public List<ActivityCsgoStyleLottery> ActivityCsgoStyleLotteries { get; set; } = [];
}

public class ExportPlayerDataRequest
{
    public short PlayerShard { get; set; }
    public long PlayerId { get; set; }
}

public class BanAndClearPlayerDataRequest
{
    public short PlayerShard { get; set; }
    public long PlayerId { get; set; }

    public bool ClearNormalRank { get; set; }
    public bool ClearSurvivorRank { get; set; }
    public bool ClearTowerDefenceRank { get; set; }
    public bool ClearTrueEndlessRank { get; set; }

    public long? BanDurationSeconds { get; set; }
    public string? BanReason { get; set; }

    public bool ClearAssets { get; set; }
}
