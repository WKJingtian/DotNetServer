using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.ImService;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class FriendController(
    IConfiguration configuration,
    ILogger<FriendController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    PlayerModule playerModule,
    MessagingAPI.MessagingAPIClient imClient,
    MessageService messageService,
    FriendModule friendModule,
    GameService gameService,
    UserAssetService userAssetService,
    UserAchievementService userAchievementService) : BaseApiController(configuration)
{
    // uid <=> pid

    [Obsolete("废弃接口，客户端更新后删除")]
    [HttpPost]
    public async Task<ActionResult> RegisterLogin()
    {
        return Ok();
    }

    // TODO: 是否有必要暴露给客户端
    [HttpPost]
    public async Task<ActionResult<long>> GetLastLoginPlayerIdByUserId(long userId)
    {
        if (userId == UserId)
            return Ok(PlayerId);

        var pid = await playerModule.GetLastLoginPlayerIdByUserId(userId);
        if (pid == -1)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        else
            return Ok(pid);
    }

    [HttpPost]
    public async Task<ActionResult<Dictionary<long, long>>> BatchGetLastLoginPlayerIdsByUserIds(List<long> userIds)
    {
        var result = await playerModule.BatchGetLastLoginPlayerIdsByUserIds(userIds);
        return Ok(result);
    }

    // TODO: 是否有必要暴露给客户端
    [HttpPost]
    public async Task<ActionResult<long>> GetUserIdByPlayerId(long playerId)
    {
        var uid = await friendModule.GetUserIdByPlayerId(playerId, PlayerId, UserId);
        if (!uid.HasValue)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

        return Ok(uid);
    }

    // TODO: 是否有必要暴露给客户端
    [HttpPost]
    public async Task<ActionResult<Dictionary<long, long>>> GetUserIdByPlayerIdBatch(List<long> playerIdList)
    {
        Dictionary<long, long> dict = new(playerIdList.Count);
        foreach (var pid in playerIdList)
        {
            var uid = await friendModule.GetUserIdByPlayerId(pid, PlayerId, UserId);
            if (!uid.HasValue)
            {
                logger.LogWarning("Player pid({pid})=>uid not found", pid);
                continue;
            }

            dict.Add(pid, uid.Value);
        }

        return Ok(dict);
    }

    // 好友系统
    private Task<bool> CheckIsFriend(long userId1, long userId2)
    {
        return friendModule.CheckIsFriend(userId1, userId2);
    }

    // 挂机宝箱
    public class FriendIdleRewardStatus(
        long userId,
        long playerId,
        long idleRewardStartTime,
        List<long> idleRewardStolenRecords,
        int idleRewardId)
    {
        public long UserId { get; set; } = userId;
        public long PlayerId { get; set; } = playerId;
        public long IdleRewardStartTime { get; set; } = idleRewardStartTime;
        public List<long> IdleRewardStolenRecords { get; set; } = idleRewardStolenRecords;
        public int IdleRewardId { get; set; } = idleRewardId;
    }

    [HttpPost]
    public async Task<ActionResult<FriendIdleRewardStatus>> GetFriendIdleRewardStatus(long friendUserId)
    {
        if (UserId != friendUserId && !await CheckIsFriend(UserId, friendUserId))
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONTACT });

        var friendPlayerId = PlayerId;
        UserIdleRewardInfo? friendIdleRewardInfo = null;
        if (UserId == friendUserId)
        {
            friendIdleRewardInfo = await context.GetUserIdleRewardInfo(PlayerShard, PlayerId);
        }
        else
        {
            var pid = await playerModule.GetLastLoginPlayerIdByUserId(friendUserId);
            if (!pid.HasValue)
            {
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
            }

            friendPlayerId = pid.Value;

            var friendShardId = await playerModule.GetPlayerShardId(friendPlayerId);
            friendIdleRewardInfo = await context.GetUserIdleRewardInfo(friendShardId, friendPlayerId);
        }

        if (friendIdleRewardInfo == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

        var status = new FriendIdleRewardStatus(
            userId: friendUserId,
            playerId: friendPlayerId,
            idleRewardStartTime: friendIdleRewardInfo.StartTime,
            idleRewardStolenRecords: friendIdleRewardInfo.StolenRecords,
            idleRewardId: friendIdleRewardInfo.IdleRewardId);
        return Ok(status);
    }

    [HttpPost]
    public async Task<ActionResult<List<FriendIdleRewardStatus>>> GetAllFriendsIdleRewardStatus()
    {
        // 包括自己
        var selfIdleRewardInfo = await context.GetUserIdleRewardInfo(PlayerShard, PlayerId);
        if (selfIdleRewardInfo == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

        var selfStatus = new FriendIdleRewardStatus(userId: UserId, playerId: PlayerId,
            idleRewardStartTime: selfIdleRewardInfo.StartTime,
            idleRewardStolenRecords: selfIdleRewardInfo.StolenRecords,
            idleRewardId: selfIdleRewardInfo.IdleRewardId);

        // 查所有好友
        var request = new QueryRelatedUsersRequest() { UserId = UserId, RelationFilter = { UserRelationship.Friend } };
        var result = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(async () => await imClient.QueryRelatedUsersAsync(request));
        if (result is null)
            return BadRequest(ErrorKind.INVALID_FRIEND_LIST.Response());

        var statusList = new List<FriendIdleRewardStatus>(result.RelatedUsers.Count + 1) { selfStatus, };

        Dictionary<long, long> playerIdByUid
            = await playerModule.BatchGetLastLoginPlayerIdsByUserIds(result.RelatedUsers.Select(user => user.UserId)
                .ToList());
        var playerIdleRewardData = await friendModule.BatchGetPlayerIdleRewardData(playerIdByUid.Values.ToList());
        var friendsIdleRewardStatus = playerIdleRewardData.Join(playerIdByUid,
            data => data.Item1,
            player => player.Value,
            (data, player) => new FriendIdleRewardStatus(
                userId: player.Key,
                playerId: player.Value,
                idleRewardStartTime: data.Item2.StartTime,
                idleRewardStolenRecords: data.Item2.StolenRecords,
                idleRewardId: data.Item2.IdleRewardId));

        statusList.AddRange(friendsIdleRewardStatus);

        return Ok(statusList);
    }

    [HttpPost]
    public async Task<ActionResult<UserPaymentController.OpenTreasureBoxReply>> OpenIdleReward()
    {
        return await context.WithRCUDefaultRetry<ActionResult<UserPaymentController.OpenTreasureBoxReply>>(async _ =>
        {
            // 开箱必须获获取全部的用户资产数据
            var userAssets = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            var userIdleRewardInfo = await context.GetUserIdleRewardInfo(PlayerShard, PlayerId);
            if (userIdleRewardInfo is null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            var idleRewardConf = serverConfigService.GetIdleRewardConfigById(userIdleRewardInfo.IdleRewardId);
            if (idleRewardConf == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
            int unlockTime = idleRewardConf.fill_time;
            int boxId = idleRewardConf.box_id;
            int stealBoxId = idleRewardConf.steal_box_id;

            // 只有挂机时间已满才能打开
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var startTime = userIdleRewardInfo.StartTime;
            if (currentTime - startTime <= unlockTime)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.IDLE_REWARD_NOT_TIME_YET });

            // 自己挂机宝箱配置
            var treasureBoxConfig = serverConfigService.GetTreasureBoxConfigById(boxId);
            if (treasureBoxConfig == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            // 偷来挂机宝箱相的配置
            var stealTreasureBoxConfig = serverConfigService.GetTreasureBoxConfigById(stealBoxId);
            if (stealTreasureBoxConfig == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            var assetChange = new UserAssetsChange();
            var rewardList = new List<RewardItemData>();
            var random = Random.Shared;

            // 月卡，聚宝皮肤，玩家头衔让产出上升
            float rewardMultiplier = 1.0f + idleRewardConf.output_multiplier;
            int extraCoin = 0, extraCard = 0;
            bool monthPassEnabled
                = await context.GetNthDayOfMonthPass(PlayerId, PlayerShard, userAssets.TimeZoneOffset) >= 0;
            if (monthPassEnabled)
            {
                if (!serverConfigService.TryGetParameterFloat(Params.MonthPassIdleRewardMultiplier,
                        out float monthPassMultiplier))
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                rewardMultiplier += monthPassMultiplier;
            }

            var totalStarCount = await context.GetTotalStoryStarCount(PlayerId, PlayerShard);
            var storyStarRewardConfig = serverConfigService.GetTopStoryStarRewardConfigByStarCount(totalStarCount);
            if (storyStarRewardConfig != null)
            {
                extraCoin += storyStarRewardConfig.idle_reward_extra_coin;
                extraCard += storyStarRewardConfig.idle_reward_extra_card;
            }

            var finalBoxConfig = new TreasureBoxConfig()
            {
                id = treasureBoxConfig.id,
                coin_min = (int)((treasureBoxConfig.coin_min + extraCoin) * rewardMultiplier),
                coin_max = (int)((treasureBoxConfig.coin_max + extraCoin) * rewardMultiplier),
                diamond_min = treasureBoxConfig.diamond_min,
                diamond_max = treasureBoxConfig.diamond_max,
                box_type = treasureBoxConfig.box_type,
                card_count = (int)((treasureBoxConfig.card_count + extraCard) * rewardMultiplier),
                different_card_count = treasureBoxConfig.different_card_count,
                lucky_star_level = treasureBoxConfig.lucky_star_level,
                guarantee_count_list = treasureBoxConfig.guarantee_count_list,
                weight_list = treasureBoxConfig.weight_list,
                fixed_card_pool = treasureBoxConfig.fixed_card_pool,
                fixed_card_count_list = treasureBoxConfig.fixed_card_count_list,
                deco_item_pool = treasureBoxConfig.deco_item_pool,
            };

            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

            var (reward, errorKind)
                = await userItemService.RandomTreasureBoxReward(userGameInfo, userAssets, PlayerShard, PlayerId,
                    finalBoxConfig, GameVersion);
            if (errorKind != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)errorKind });

            // 去掉被偷走的部分
            if (!idleRewardConf.no_steal_effect)
            {
                int totalStolenCardAmount = userIdleRewardInfo.StolenRecords.Count * stealTreasureBoxConfig.card_count;
                for (int i = 0; i < totalStolenCardAmount && reward.CardList.Count > 0; i++)
                {
                    var index = random.Next(reward.CardList.Count);
                    reward.CardList[index].Count--;
                    if (reward.CardList[index].Count <= 0)
                        reward.CardList.RemoveAt(index);
                }
            }

            reward.CardList.Shuffle();
            rewardList.AddRange(reward.CurrencyList);
            rewardList.AddRange(reward.CardList);

            userIdleRewardInfo.StartTime = currentTime;
            userIdleRewardInfo.StolenRecords.Clear();

            var result = await userItemService.UnpackTreasureBoxRewardList(userAssets, rewardList, assetChange, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 记录每日任务进度
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.CLAIM_IDLE_REWARD, 1,
                PlayerShard, PlayerId, userAssets.TimeZoneOffset);
            assetChange.FillAssetInfo(userAssets);

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            userAssetService.DetachUserAssetCards(userAssets);
            await context.SaveChangesWithDefaultRetryAsync(false);
            // 卡牌变更 upsert
            await userAssetService.UpsertUserCardsAsync(result.CardChangeSet);
            // 成就项
            var achievements
                = await userAchievementService.IncreaseTreasureBoxAchievementAsync([treasureBoxConfig], [1], PlayerShard, PlayerId);
            var newCardList = result.NewCardList;
            achievements.AddRange(await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId));
            assetChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new UserPaymentController.OpenTreasureBoxReply()
            {
                AssetsChange = assetChange,
                RewardList = rewardList,
                BoxId = new() { treasureBoxConfig.id },
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserPaymentController.OpenTreasureBoxReply>> OpenFriendIdleReward(long friendUserId)
    {
        return await context.WithRCUDefaultRetry<ActionResult<UserPaymentController.OpenTreasureBoxReply>>(async _ =>
        {
            var friendPlayerId = await playerModule.GetLastLoginPlayerIdByUserId(friendUserId);
            if (!friendPlayerId.HasValue)
            {
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
            }

            var targetPlayerShard = await playerModule.GetPlayerShardId(friendPlayerId.Value);
            var playerIdleRewardInfo
                = await context.GetUserIdleRewardInfo(targetPlayerShard, friendPlayerId.Value);
            if (playerIdleRewardInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            var idleRewardConf = serverConfigService.GetIdleRewardConfigById(playerIdleRewardInfo.IdleRewardId);
            if (idleRewardConf == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });
            int unlockTime = idleRewardConf.fill_time;
            int stealBoxId = idleRewardConf.steal_box_id;
            int stealPlayerCountMax = idleRewardConf.steal_player_count;

            // 只有挂机时间已满才能打开
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var startTime = playerIdleRewardInfo.StartTime;
            if (currentTime - startTime <= unlockTime)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.IDLE_REWARD_NOT_TIME_YET });

            // 在被偷者开启前，每个好友只能偷一次
            if (playerIdleRewardInfo.StolenRecords.Contains(UserId))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.IDLE_REWARD_ALREADY_STOLEN });

            // 最多被n个人偷
            if (playerIdleRewardInfo.StolenRecords.Count >= stealPlayerCountMax)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.IDLE_REWARD_STEAL_COUNT_LIMIT });

            // 开箱必须获获取全部的用户资产数据
            var selfAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (selfAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            // 偷来挂机宝箱的配置
            var stealTreasureBoxConfig = serverConfigService.GetTreasureBoxConfigById(stealBoxId);
            if (stealTreasureBoxConfig == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            var assetChange = new UserAssetsChange();
            var rewardList = new List<RewardItemData>();

            // 偷别人挂机宝箱
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
            var (reward, errorKind) = await userItemService.RandomTreasureBoxReward(userGameInfo, selfAsset,
                PlayerShard, PlayerId, stealTreasureBoxConfig, GameVersion);

            if (errorKind != ErrorKind.SUCCESS)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)errorKind });

            reward.CardList.Shuffle();
            rewardList.AddRange(reward.CurrencyList);
            rewardList.AddRange(reward.CardList);
            playerIdleRewardInfo.StolenRecords.Add(UserId);

            var result = await userItemService.UnpackTreasureBoxRewardList(selfAsset, rewardList, assetChange, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
            assetChange.FillAssetInfo(selfAsset);

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            userAssetService.DetachUserAssetCards(selfAsset);
            await context.SaveChangesWithDefaultRetryAsync(false);
            // 卡牌变更 upsert
            await userAssetService.UpsertUserCardsAsync(result.CardChangeSet);
            // 成就项
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(result.NewCardList, PlayerShard, PlayerId);
            assetChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new UserPaymentController.OpenTreasureBoxReply()
            {
                AssetsChange = assetChange,
                RewardList = rewardList,
                BoxId = new() { stealBoxId },
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<bool>> ReportSelfIdleRewardChanged()
    {
        var request = new QueryRelatedUsersRequest() { UserId = UserId, RelationFilter = { UserRelationship.Friend } };
        var friendsResult = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(async () => await imClient.QueryRelatedUsersAsync(request));
        if (friendsResult == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_FRIEND_LIST });
        foreach (var user in friendsResult.RelatedUsers)
            messageService.NoticeFriendIdleRewardChanged(user.UserId, UserId);
        return Ok(true);
    }

    [HttpPost]
    public async Task<ActionResult<bool>> SetIdleRewardBox(int boxId)
    {
        return await context.WithRCUDefaultRetry<ActionResult<bool>>(async _ =>
        {
            var userIdleRewardInfo = await context.GetUserIdleRewardInfo(PlayerShard, PlayerId);
            if (userIdleRewardInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            if (boxId == 0)
                userIdleRewardInfo.IdleRewardId = boxId;
            else
            {
                bool hasItem = await userItemService.HasItemAsync(PlayerShard, PlayerId, boxId);
                if (!hasItem)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.ITEM_NOT_ENOUGH });
                userIdleRewardInfo.IdleRewardId = boxId;
            }

            await context.SaveChangesWithDefaultRetryAsync();
            return Ok(true);
        });
    }
}