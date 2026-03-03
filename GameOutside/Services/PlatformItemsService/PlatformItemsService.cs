using System.Text.Json;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using ChillyRoom.Infra.PlatformDef.DBModel.Repositories;
using GameOutside.DBContext;
using GameOutside.Util;
using StackExchange.Redis;

namespace GameOutside.Services.PlatformItemsService;

public record struct PlatformNotifyAttachmentsMetadata(
    long PlayerId,
    short ShardId,
    NotifyType NotifyType,
    Guid Id);

public partial class PlatformItemsService(
    ILogger<PlatformItemsService> logger,
    BuildingGameDB dbCtx,
    IPlatformNotifyRepository platformNotifyRepository,
    IPaidOrderWithShardRepository paidOrderWithShardRepository,
    IPlatformItemRepository platformItemRepository,
    IConnectionMultiplexer redisConn,
    UserItemService userItemService,
    PlayerModule playerModule,
    ServerConfigService serverConfigService,
    ActivityService activityService,
    IapPackageService iapPackageService,
    BattlePassService battlePassService,
    UserAssetService userAssetService,
    UserInfoService userInfoService,
    UserAchievementService userAchievementService)
{
    /// <summary>
    /// 领取邮箱、礼包码到账附件，写入存档
    /// </summary>
    /// <remarks>
    /// 1. 确保返回值不返回 null
    /// 2. 所有异常需要抛出以进行 DLQ 重试
    /// </remarks>
    /// <param name="rewardList"></param>
    /// <param name="owner"></param>
    /// <returns></returns>
    /// <exception cref="ClaimAttachmentsException"></exception>
    public ValueTask<TakeRewardResult> ClaimPlatformNotifyAttachmentsAsync(List<RewardItemData> rewardList, PlatformNotifyAttachmentsMetadata owner)
    {
        var payload = JsonSerializer.Serialize(rewardList);
        return dbCtx.WithRCUDefaultRetry(async _ =>
        {
            platformNotifyRepository.AddNotify(owner.NotifyType, owner.Id, owner.ShardId, owner.PlayerId, payload,
                ClaimStatus.Claimed);
            // 发放物品
            var generalReward = new GeneralReward
            {
                ItemList = rewardList.Select(entry => entry.Id).ToList(),
                CountList = rewardList.Select(entry => entry.Count).ToList()
            };

            // 优化查询
            var includeOptions = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(owner.ShardId, owner.PlayerId, includeOptions);
            if (userAsset is null)
            {
                throw _userAssetNullException;
            }

            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, "100.0.0");
            if (result is null)
            {
                throw _payloadTakeRewardException;
            }

            await using var t = await dbCtx.Database.BeginTransactionAsync();
            await dbCtx.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, owner.ShardId, owner.PlayerId);
            if (result.AssetsChange is not null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbCtx.ChangeTracker.AcceptAllChanges();

            return result;
        });
    }

    // 一些商品Id定义：
    private const string _propIdDiamondPrefix = "com.chillyroom.inkvasion.diamond";
    private const string _propIdFirstPass = "com.chillyroom.inkvasion.firstpass0";
    private const string _propIdBattlePass = "com.chillyroom.inkvasion.battlepass0";
    private const string _propIdPiggyBank = "com.chillyroom.inkvasion.piggybank0";
    private const string _propIdMonthPass = "com.chillyroom.inkvasion.monthpass0";
    private const string _propIdFortuneBagPrefix = "com.chillyroom.inkvasion.fortunebag";
    private const string _propIdSlotMachineDoubleUp = "com.chillyroom.inkvasion.slotmachinedoubleup";
    private const string _propIdTreasureHuntKey = "com.chillyroom.inkvasion.treasurehuntkey";
    private const string _propIdCsgoLotteryPass = "com.chillyroom.inkvasion.csgolotterypass";
    
    // 通用异常
    private readonly ClaimAttachmentsException _userAssetNullException =
        new(ClaimAttachmentsErrorCode.UserAssetNull, "user asset is null");
    private readonly ClaimAttachmentsException _payloadTakeRewardException =
        new(ClaimAttachmentsErrorCode.ErrorPayloadTakeReward, "payload take reward failed");
    private readonly ClaimAttachmentsException _paramOrConfigNotFoundException =
        new(ClaimAttachmentsErrorCode.ErrorPayloadTakeReward, "missing params or config");

    private static readonly JsonSerializerOptions DEFAULT_JSON_SERIALIZER_OPTIONS =
        new() { PropertyNameCaseInsensitive = true };
}

public sealed class ClaimAttachmentsException(ClaimAttachmentsErrorCode errorCode, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public ClaimAttachmentsErrorCode ErrorCode { get; } = errorCode;
}

public enum ClaimAttachmentsErrorCode
{
    None = 0,
    UserAssetNull = 1,
    ErrorPayloadTakeReward = 2,
}
