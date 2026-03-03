using System.Text.Json;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using ChillyRoom.PayService;
using GameExternal;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Services.KafkaConsumers;
using GameOutside.Util;
using StaleReadOptions = ChillyRoom.Infra.PlatformDef.DBModel.Models.StaleReadOptions;

namespace GameOutside.Services.PlatformItemsService;

public partial class PlatformItemsService
{
    public async ValueTask AddPaidOrderWithShard(OrderStatusEvent ev)
    {
        // 入库，默认状态为未领取
        var payload = JsonSerializer.Deserialize<SkuPayload>(ev.Payload, DEFAULT_JSON_SERIALIZER_OPTIONS);
        if (payload is null)
        {
            throw _payloadNullException;
        }

        var iapConfig = serverConfigService.GetSkuItemConfig(payload.PropId);
        if (iapConfig == null)
        {
            throw _propIdNotValidException;
        }

        var shardId = await playerModule.GetPlayerShardId(ev.PlayerId);
        if (!shardId.HasValue)
        {
            throw _shardIdNullException;
        }

        paidOrderWithShardRepository.AddPaidOrder(ev.OrderId, shardId.Value, ev.PlayerId, ev.Payload, ev.Quantity, null,
                GeneralClaimStatus.NotClaimed);
        await dbCtx.SaveChangesWithDefaultRetryAsync();
    }

    /// <summary>
    /// 领取付费订单到账物品，写入存档
    /// </summary>
    /// <param name="shardId"></param>
    /// <param name="playerId"></param>
    /// <param name="gameVersion"></param>
    /// <returns></returns>
    public async ValueTask<PayEventMessage> ClaimPaidOrderAttachmentsAsync(short shardId, long playerId, string gameVersion)
    {
        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var unclaimedPaidOrdersWithShard
                = await paidOrderWithShardRepository.GetPaidOrdersAsync(playerId, shardId, TrackingOptions.Tracking, StaleReadOptions.NoStaleRead, [GeneralClaimStatus.NotClaimed]);
            if (unclaimedPaidOrdersWithShard.IsNullOrEmpty())
                return new PayEventMessage() { OrderIds = [], Result = null };

            var generalReward = new GeneralReward { ItemList = [], CountList = [] };

            foreach (var order in unclaimedPaidOrdersWithShard)
            {
                var payload = JsonSerializer.Deserialize<SkuPayload>(order.Payload, DEFAULT_JSON_SERIALIZER_OPTIONS);
                if (payload is null)
                {
                    throw _payloadNullException;
                }

                var iapConfig = serverConfigService.GetSkuItemConfig(payload.PropId);
                if (iapConfig == null)
                    throw _propIdNotValidException;

                int itemId = iapConfig.item_id;
                int itemCount = iapConfig.item_count;

                iapPackageService.AddIapPurchaseRecord(shardId, playerId, payload.PropId);
                order.ClaimStatus = GeneralClaimStatus.Claimed;

                // 更新付费推广状态
                if (!serverConfigService.TryGetParameterString(Params.PromotedPackageIapIdList,
                        out var promotedPackageIapIds) ||
                    !serverConfigService.TryGetParameterString(Params.IceBreakingIapIdList, out var iceBreakIapIds))
                    throw _paramOrConfigNotFoundException;
                var promotedPackageIapIdList = promotedPackageIapIds.Split('|').ToList();
                var iceBreakIapIdList = iceBreakIapIds.Split('|').ToList();
                var paymentAndPromotionStatus = await iapPackageService.GetPromotionData(playerId, shardId);
                if (paymentAndPromotionStatus != null)
                {
                    var promoteItemIdx = promotedPackageIapIdList.IndexOf(payload.PropId);
                    if (promoteItemIdx != -1)
                    {
                        var nextPromoteItemIdx = promoteItemIdx + 1;
                        if (nextPromoteItemIdx >= promotedPackageIapIdList.Count)
                            paymentAndPromotionStatus.LastPromotedPackage = string.Empty;
                        else
                            paymentAndPromotionStatus.LastPromotedPackage
                                = promotedPackageIapIdList[nextPromoteItemIdx];
                        paymentAndPromotionStatus.PackagePromotionTime = TimeUtils.GetCurrentTime();
                    }

                    // 只要玩家买过指定礼包，就不需要继续推销破冰付费了
                    if (iceBreakIapIdList.Contains(payload.PropId))
                        paymentAndPromotionStatus.IceBreakingPayPromotion = 2;

                    // 首次购买玉璧包有双倍奖励
                    if (itemId == 0 &&
                        !paymentAndPromotionStatus.DoubleDiamondBonusTriggerRecords.ContainsKey(payload.PropId))
                    {
                        paymentAndPromotionStatus.DoubleDiamondBonusTriggerRecords.Add(payload.PropId, order.Id);
                        dbCtx.Entry(paymentAndPromotionStatus)
                            .Property(t => t.DoubleDiamondBonusTriggerRecords)
                            .IsModified = true;
                        itemCount *= 2;
                    }
                }
                else
                {
                    logger.LogError("player paymentAndPromotionStatus is null!");
                }

                // 发放商品
                generalReward.ItemList.Add(itemId);
                generalReward.CountList.Add(itemCount * order.Quantity);
            }

            // 优化查询
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(shardId, playerId, includeOption);
            if (userAsset is null)
            {
                throw _userAssetNullException;
            }
            // 记录每日任务进度
            await dbCtx.AddDailyTaskProgress(serverConfigService, DailyTaskType.CHARGE_MONEY, unclaimedPaidOrdersWithShard.Count,
                shardId, playerId, userAsset.TimeZoneOffset);

            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, gameVersion);
            if (result is null)
            {
                throw _payloadTakeRewardException;
            }

            await using var t = await dbCtx.Database.BeginTransactionAsync();
            await dbCtx.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, shardId, playerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbCtx.ChangeTracker.AcceptAllChanges();

            return new PayEventMessage()
            {
                OrderIds = unclaimedPaidOrdersWithShard.Select(order => order.Id).ToList(),
                Result = result
            };
        });
    }

    /// <summary>
    /// 付费订单退款，存档回退
    /// </summary>
    /// <remarks>
    /// 所有异常需要抛出以进行 DLQ 重试
    /// </remarks>
    /// <param name="ev"></param>
    /// <exception cref="PayException"></exception>
    /// <exception cref="ClaimAttachmentsException"></exception>
    public async ValueTask<int> RefundPaidOrderAttachmentsAsync(OrderStatusEvent ev)
    {
        var payload = JsonSerializer.Deserialize<SkuPayload>(ev.Payload, DEFAULT_JSON_SERIALIZER_OPTIONS);
        if (payload is null)
        {
            throw _payloadNullException;
        }

        var iapConfig = serverConfigService.GetSkuItemConfig(payload.PropId);
        if (iapConfig == null)
            throw _propIdNotValidException;

        var shardId = await playerModule.GetPlayerShardId(ev.PlayerId);
        if (!shardId.HasValue)
        {
            throw _shardIdNullException;
        }

        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            DateTime orderCreateTime;
            var paidOrderWithShard = await paidOrderWithShardRepository.GetPaidOrderByOrderIdAsync(ev.OrderId, shardId.Value, TrackingOptions.Tracking, StaleReadOptions.NoStaleRead);
            if (paidOrderWithShard is null)
            {
                throw _orderNotFoundException;
            }

            if (paidOrderWithShard.ClaimStatus == GeneralClaimStatus.Revoked)
            {
                throw _orderAlreadyRevokedException;
            }

            var paidOrderClaimStatus = paidOrderWithShard.ClaimStatus;
            paidOrderWithShard.ClaimStatus = GeneralClaimStatus.Revoked;
            orderCreateTime = paidOrderWithShard.CreatedAt.ToUniversalTime();

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(shardId, ev.PlayerId);
            if (userAsset is null)
            {
                throw _userAssetNullException;
            }

            int beforeDiamondCount = userAsset.DiamondCount;

            // 如果退款时订单已领取，则进行物品回退
            if (paidOrderClaimStatus == GeneralClaimStatus.Claimed)
            {
                if (iapConfig.prop_id.StartsWith(_propIdDiamondPrefix))
                {
                    // 玉璧退款
                    await RefundDiamond(ev, shardId, payload, iapConfig, userAsset);
                }
                else if (iapConfig.prop_id.StartsWith(_propIdFortuneBagPrefix))
                {
                    // 福袋退款
                    RefundFortuneBag(iapConfig, userAsset, orderCreateTime);
                }
                else if (iapConfig.prop_id.Equals(_propIdFirstPass))
                {
                    // 新手战令
                    await RefundBattlePass(shardId.Value, userAsset, orderCreateTime, false);
                }
                else if (iapConfig.prop_id.Equals(_propIdBattlePass))
                {
                    // 赛季战令
                    await RefundBattlePass(shardId.Value, userAsset, orderCreateTime, true);
                }
                else if (iapConfig.prop_id.Equals(_propIdPiggyBank))
                {
                    // 貔貅翁
                    await RefundPiggyBank(shardId.Value, userAsset);
                }
                else if (iapConfig.prop_id.Equals(_propIdMonthPass))
                {
                    // 月卡
                    await RefundMonthPass(shardId, userAsset);
                }
                else if (iapConfig.prop_id.StartsWith(_propIdSlotMachineDoubleUp))
                {
                    // 乾坤试运(老虎机) 奖励翻倍
                    await RefundSlotMachineDoubleUp(shardId.Value, userAsset, orderCreateTime, iapConfig);
                }
                else if (iapConfig.prop_id.StartsWith(_propIdTreasureHuntKey))
                {
                    await RefundTreasureHuntKey(shardId.Value, userAsset, orderCreateTime, iapConfig);
                }
                else if (iapConfig.prop_id.StartsWith(_propIdCsgoLotteryPass))
                {
                    await RefundCsgoLotteryPass(shardId.Value, userAsset, orderCreateTime, iapConfig);
                }
                else
                {
                    // 礼包退款
                    var packageConfig = serverConfigService.GetPackageConfigById(iapConfig.item_id);
                    if (packageConfig is null)
                        throw _paramOrConfigNotFoundException;
                    RefundPackage(packageConfig, userAsset);
                }

                if (beforeDiamondCount >= 0 && userAsset.DiamondCount < 0)
                {
                    userAsset.LastInDebtTime = TimeUtils.GetCurrentTime();
                }
            }

            await using var t = await dbCtx.Database.BeginTransactionAsync();
            await dbCtx.SaveChangesAsync(false);
            await t.CommitAsync();
            dbCtx.ChangeTracker.AcceptAllChanges();

            int diff = beforeDiamondCount - userAsset.DiamondCount;
            return diff;
        });
    }

    private async ValueTask RefundDiamond(
        OrderStatusEvent ev,
        short? shardId,
        SkuPayload payload,
        IapCommodityConfig iapConfig,
        UserAssets userAsset)
    {
        var paymentAndPromotionStatus = await iapPackageService.GetPromotionData(ev.PlayerId, shardId);
        if (paymentAndPromotionStatus is null)
        {
            throw _promotionStatusNullException;
        }

        var refundDiamondCount = iapConfig.item_count;
        if (paymentAndPromotionStatus.DoubleDiamondBonusTriggerRecords.TryGetValue(payload.PropId,
                out var doubledDiamondOrderId) && doubledDiamondOrderId == ev.OrderId)
            refundDiamondCount *= 2;
        userAsset.DiamondCount -= refundDiamondCount;
    }

    private void RefundFortuneBag(IapCommodityConfig iapConfig, UserAssets userAsset, DateTime orderCreateTime)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityFortuneBag, "100.0.0", orderCreateTime);
        if (activity is null)
        {
            throw _activityNotFoundException;
        }

        var fortuneBagConfig = serverConfigService.GetActivityFortuneBagConfigByActivityId(activity.id);
        if (fortuneBagConfig is null)
        {
            throw _paramOrConfigNotFoundException;
        }

        // 不涉及重置FortuneBag的数据
        int count = iapConfig.item_count;
        int refundDiamondCount = count * fortuneBagConfig.fortune_bag_diamond_count.Sum();
        // 直接扣除
        userAsset.DiamondCount -= refundDiamondCount;
    }

    private async Task RefundBattlePass(
        short shardId,
        UserAssets userAsset,
        DateTime orderCreateTime,
        bool isBattlePass)
    {
        var passId = -1;
        if (isBattlePass)
        {
            passId = serverConfigService.GetActiveBattlePassId(orderCreateTime);
            if (passId == -1)
                throw _battlePassConfigNotFoundException;
        }

        var playerId = userAsset.PlayerId;
        DateTime? startTime = null;
        DateTime? endTime = null;
        if (isBattlePass)
        {
            var timeConfig = serverConfigService.GetBattlePassTimeConfigByPassId(passId);
            if (timeConfig == null)
                throw _battlePassConfigNotFoundException;
            startTime = TimeUtils.ParseDateTimeStr(timeConfig.start_time);
            endTime = TimeUtils.ParseDateTimeStr(timeConfig.end_time);
        }

        // 筛选已发货订单
        var propId = isBattlePass ? _propIdBattlePass : _propIdFirstPass;
        int battlePassOrderCount = await GetClaimedOrderCountAsync(shardId, playerId, startTime, endTime, propId);

        // 筛选已发货邮件
        var detailedKey = isBattlePass ? Consts.BattlePassItemKey : Consts.FirstPassItemKey;
        int battlePassAttachmentCount
            = await GetClaimedAttachmentCountAsync(shardId, playerId, ItemType.BattlePass, detailedKey, startTime, endTime);

        int totalCount = battlePassOrderCount + battlePassAttachmentCount;
        // totalCount > 1就说明有重复领取过对应战令
        if (totalCount > 1)
        {
            logger.LogInformation("Player:{PlayerId} 重复领取战令，此次退款无需退货, TotalCount:{TotalCount}", playerId, totalCount);
            // 有重复订单，这里就不用去扣东西了，直接返回
            return;
        }

        // 扣除玉璧
        int refundDiamondCount = await RefundBattlePassByPassId(shardId, playerId, passId);
        userAsset.DiamondCount -= refundDiamondCount;
    }

    private async Task<int> RefundBattlePassByPassId(short? shardId, long playerId, int passId)
    {
        // 没有对应战令数据就不管了，没啥可扣的
        var battlePassData = await battlePassService.GetUserBattlePassInfoByPassIdAsync(shardId, playerId, passId);
        if (battlePassData == null)
            return 0;
        var battPassConfigList = serverConfigService.GetBattlePassConfigListByPassId(passId);
        if (battPassConfigList == null)
            throw _battlePassConfigNotFoundException;
        int refundDiamondCount = 0;
        for (int i = 0; i < battPassConfigList.Count; i++)
        {
            BattlePassConfig? config = battPassConfigList[i];
            if (battlePassData.ClaimStatus[1].GetNthBits(i))
            {
                refundDiamondCount += config.refund_diamond;
            }
        }

        // 重置superPassLevel和领取状态
        battlePassData.SuperPassLevel = 0;
        battlePassData.ClaimStatus[1] = 0;
        return refundDiamondCount;
    }

    private async Task RefundPiggyBank(short shardId, UserAssets userAsset)
    {
        var playerId = userAsset.PlayerId;
        // 筛选订单
        var orderCount = await GetClaimedOrderCountAsync(shardId, playerId, null, null, _propIdPiggyBank);
        var attachmentCount = await GetClaimedAttachmentCountAsync(shardId, playerId, ItemType.PiggyBank, Consts.PiggyBankItemKey);
        int totalCount = orderCount + attachmentCount;
        // 数量大于1说明有重复获得了貔貅翁商品
        if (totalCount > 1)
        {
            logger.LogInformation("Player:{PlayerId} 重复领取貔貅翁，此次退款无需退货, TotalCount:{TotalCount}", playerId, totalCount);
            return;
        }

        var piggyBandData = await activityService.GetPiggyBankStatusAsync(playerId, shardId);
        if (piggyBandData is null)
            throw _piggyBandDataNotFoundException;

        int totalRefundDiamond = 0;
        // 计算已领取的高级奖励总玉璧价值
        var configList = serverConfigService.GetPiggyBankConfigList();
        for (int i = 0; i < configList.Count; i++)
        {
            var config = configList[i];
            if (piggyBandData.ClaimStatus[1].GetNthBits(i))
            {
                if (config.item_list[1] == (int)MoneyType.Diamond)
                {
                    totalRefundDiamond += config.count_list[1];
                }
            }
        }

        // 重置状态
        piggyBandData.ClaimStatus[1] = 0;
        piggyBandData.PaidLevel = 0;
        // 扣除玉璧
        userAsset.DiamondCount -= totalRefundDiamond;
    }

    private async Task RefundMonthPass(short? shardId, UserAssets userAsset)
    {
        var monthPassInfo = await dbCtx.GetMonthPassInfo(userAsset.PlayerId, shardId);
        if (monthPassInfo is null)
        {
            throw _monthPassDataNotFoundException;
        }

        if (!serverConfigService.TryGetParameterInt(Params.MonthPassFirstDayDiamond, out int firstDayDiamond))
            throw _paramOrConfigNotFoundException;

        int refundDiamondCount = firstDayDiamond;
        // 查剩余天数
        var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), monthPassInfo.PassAcquireTime,
            userAsset.TimeZoneOffset, 0);
        int dayLeft = monthPassInfo.PassDayLength - dayDiff - 1;
        // 剩余天数>30, 直接扣除剩余天数，扣购买时发的玉璧
        if (dayLeft > 30)
        {
            monthPassInfo.PassDayLength -= 30;
        }
        else if (dayLeft > 0)
        {
            // 剩余天数> 0 < 30, 直接扣除剩余天数，扣购买时发的玉璧,扣已领取的玉璧
            if (!serverConfigService.TryGetParameterInt(Params.MonthPassDailyDiamond, out int dailyDiamond))
                throw _paramOrConfigNotFoundException;
            // 扣除已领取的玉璧数量
            int numOfOne = monthPassInfo.RewardClaimStatus.NumOfOne();
            refundDiamondCount += numOfOne * dailyDiamond;
            monthPassInfo.PassDayLength -= 30;
        }

        userAsset.DiamondCount -= refundDiamondCount;
    }

    private async Task RefundSlotMachineDoubleUp(short shardId, UserAssets userAsset, DateTime orderCreateTime, IapCommodityConfig iapConfig)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivitySlotMachine, "100.0.0", orderCreateTime);
        if (activity is null)
        {
            throw _activityNotFoundException;
        }

        var slotMachineData = await activityService.GetSlotMachineDataAsync(userAsset.PlayerId, shardId, activity.id, TrackingOptions.Tracking);
        if (slotMachineData is null)
        {
            throw _slotMachineDataNotFoundException;
        }

        var doubleUpLevel = serverConfigService.GetSlotMachineDoubleUpItemLevel(iapConfig.item_id);
        if (doubleUpLevel < 0)
        {
            throw _slotMachineConfigNotFoundException;
        }

        bool haveUnusedDoubleUpItem = slotMachineData.RewardDoubledUpItemCount.Count > doubleUpLevel &&
                                      slotMachineData.RewardDoubledUpItemCount[doubleUpLevel] > 0;
        if (haveUnusedDoubleUpItem)
            slotMachineData.RewardDoubledUpItemCount[doubleUpLevel] -= 1;
        else
        {
            var drawConfig = serverConfigService.GetActivitySlotMachineDrawConfigByActivityIdAndDrawId(
                activity.id, doubleUpLevel);
            if (drawConfig == null)
                throw _slotMachineConfigNotFoundException;
            userAsset.DiamondCount -= drawConfig.double_up_diamond_price;
        }
    }
    
    private async Task RefundTreasureHuntKey(
        short shardId,
        UserAssets userAsset,
        DateTime orderCreateTime,
        IapCommodityConfig iapConfig)
    {
        var activity
            = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, "100.0.0", orderCreateTime);
        if (activity is null)
        {
            throw _activityNotFoundException;
        }

        var treasureHuntData
            = await activityService.GetTreasureHuntDataAsync(userAsset.PlayerId, shardId, activity.id,
                TrackingOptions.Tracking);
        if (treasureHuntData is null)
        {
            throw _treasureHuntDataNotFoundException;
        }

        int refundKeyCount = iapConfig.item_count;
        if (treasureHuntData.KeyCount >= refundKeyCount)
        {
            treasureHuntData.KeyCount -= refundKeyCount;
        }
        else
        {
            int extraKeyCount = refundKeyCount - treasureHuntData.KeyCount;
            treasureHuntData.KeyCount = 0;
            const int diamondPerKey = 60;
            userAsset.DiamondCount -= extraKeyCount * diamondPerKey;
        }
    }

    private async Task RefundCsgoLotteryPass(
        short shardId,
        UserAssets userAsset,
        DateTime orderCreateTime,
        IapCommodityConfig iapConfig)
    {
        var activity
            = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, "100.0.0", orderCreateTime);
        if (activity is null)
        {
            throw _activityNotFoundException;
        }
        
        // 获取通行证配置
        var passConfig = serverConfigService.GetCsgoLotteryPassConfigByItemId(activity.id, iapConfig.item_id);
        if (passConfig == null)
        {
            throw _activityNotFoundException;
        }
        
        // 获取活动数据
        var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(
            userAsset.PlayerId, shardId, activity.id, TrackingOptions.Tracking);
        if (lotteryData == null)
        {
            throw _activityNotFoundException;
        }
        
        // 检查这个通行证是不是已经被退掉了
        if ((lotteryData.ActivityPremiumPassStatus & (1 << passConfig.level)) == 0)
        {
            throw _orderAlreadyRevokedException;
        }
        
        // 检查是不是有错误的重复购买
        var playerId = userAsset.PlayerId;
        int passOrderCount = await GetClaimedOrderCountAsync(shardId, playerId, 
            TimeUtils.ParseDateTimeStr(activity.start_time), TimeUtils.ParseDateTimeStr(activity.end_time), 
            iapConfig.prop_id);
        bool noDuplicateOrder = passOrderCount <= 1;

        List<int> itemIds = new(), itemCounts = new();
        int keyToRefund = 0;
        // 不论是不是唯一的通行证，都需要退掉一次性购买奖励
        for ( int i = 0; i < passConfig.reward_item.Count; i++ )
        {
            int itemId = passConfig.reward_item[i];
            int itemCount = passConfig.reward_count[i];
            if (itemId == (int)MoneyType.CsgoStyleLottery)
            {
                keyToRefund += itemCount;
                continue;
            }
            itemIds.Add(itemId);
            itemCounts.Add(itemCount);
        }
        
        // 如果是唯一的通行证，再执行日常奖励部分的扣款
        if (noDuplicateOrder)
        {
            // 计算已领取的每日奖励数量
            int dailyRewardsReceived = 0;
            var activityStartTime = TimeUtils.ParseDateTimeStrToUnixSecond(activity.start_time);
            if (lotteryData.PremiumPassDailyRewardClaimStatus.Count > passConfig.level)
            {
                dailyRewardsReceived = TimeUtils.GetDayDiffBetween(
                    lotteryData.PremiumPassDailyRewardClaimStatus[passConfig.level],
                    activityStartTime, userAsset.TimeZoneOffset, 0);
            }
        
            // 退掉每日奖励
            for ( int i = 0; i < passConfig.daily_reward_item.Count; i++ )
            {
                int itemId = passConfig.daily_reward_item[i];
                int itemCount = passConfig.daily_reward_count[i] * dailyRewardsReceived;
                if (itemId == (int)MoneyType.CsgoStyleLottery)
                {
                    keyToRefund += itemCount;
                    continue;
                }
                itemIds.Add(itemId);
                itemCounts.Add(itemCount);
            }
            
            // 重置通行证状态
            lotteryData.ActivityPremiumPassStatus &= ~(1L << passConfig.level);
            if (lotteryData.PremiumPassDailyRewardClaimStatus.Count > passConfig.level)
            {
                lotteryData.PremiumPassDailyRewardClaimStatus[passConfig.level] = activityStartTime;
            }
        }
        
        // 如果玩家的活动数据里有尚未用完的钥匙，那优先扣钥匙
        int oldKeyCount = lotteryData.KeyCount;
        lotteryData.KeyCount -= Math.Min(oldKeyCount, keyToRefund);
        keyToRefund -= oldKeyCount;
        if (keyToRefund > 0)
        {
            itemIds.Add((int)MoneyType.CsgoStyleLottery);
            itemCounts.Add(keyToRefund);
        }
        
        var includeOption = userItemService.CalculateUserAssetIncludeOptionsForRefund(itemIds);
        var detailedUserAsset = userAsset;
        if (includeOption != UserAssetIncludeOptions.NoInclude)
            detailedUserAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(shardId, userAsset.PlayerId, includeOption);
        if (detailedUserAsset is null)
            throw _userAssetNullException;
        int refundDiamondCount = 0;
        for (int i = 0; i < itemIds.Count; i++)
        {
            int itemId = itemIds[i];
            int itemCount = itemCounts[i];
            int diamondValue = await RefundGenericItem(shardId, userAsset.PlayerId, itemId, itemCount, userAsset);
            refundDiamondCount += diamondValue;
        }
        userAsset.DiamondCount -= refundDiamondCount;
    }

    private void RefundPackage(ItemPackageConfig packageConfig, UserAssets userAsset)
    {
        int refundDiamondCount = packageConfig.refund_diamond;
        userAsset.DiamondCount -= refundDiamondCount;
    }

    private async Task<int> RefundGenericItem(
        short shardId, long playerId, int itemId, int itemCount, UserAssets userAsset)
    {
        var itemConfig = serverConfigService.GetItemConfigById(itemId);
        if (itemConfig == null) throw _paramOrConfigNotFoundException;
        UserInfo? userInfo = null;
        switch (itemConfig.type)
        {
            // 消耗品，退款的时候可以直接扣除玉璧价值
            case ItemType.Currency:
            case ItemType.SoldierCard:
            case ItemType.BuildingCard:
            case ItemType.TowerCard:
            case ItemType.TreasureBox:
            case ItemType.MagicCard:
                return (int)(itemConfig.diamond_value * itemCount);
            // 这些物品类型不应该在这里被处理，他们有独立的退款逻辑
            case ItemType.BattlePass:
            case ItemType.MonthPass:
            case ItemType.ItemPackage:
            case ItemType.FortuneBag:
            case ItemType.PiggyBank:
            case ItemType.SlotMachineDoubleUp:
            case ItemType.CsgoLotteryPass:
                throw _refundBehaviorUndefinedForThisItemType;
            // 这些物品类型没有被实装
            case ItemType.Advice:
            case ItemType.Common:
                return 0;
            // 未实装，但应该是消耗品
            case ItemType.ArenaTicket:
            case ItemType.OneTimeBooster:
                return (int)(itemConfig.diamond_value * itemCount);
            // 以下都是非消耗品，退款时需要真正从玩家仓库中删除他们
            case ItemType.Hero:
                var heroConfig = serverConfigService.GetHeroConfigByKey(itemConfig.detailed_key);
                if (heroConfig == null) throw _paramOrConfigNotFoundException;
                userAsset.Heroes.Remove(heroConfig.id);
                // 删掉卡组信息会导致玩家损失已经解锁的卡槽，这里就不删了
                break;
            case ItemType.NameCard:
                userAsset.UserItems.RemoveAll((userItem) => userItem.ItemId == itemConfig.id);
                userInfo = await userInfoService.GetUserInfoAsync(shardId, playerId);
                if (userInfo == null) throw _userDataNotFound;
                if (userInfo.NameCardItemID == itemId)
                    userInfo.NameCardItemID = 0;
                break;
            case ItemType.Avatar:
                userAsset.UserItems.RemoveAll((userItem) => userItem.ItemId == itemConfig.id);
                // 头像没有存在这个服务上
                break;
            case ItemType.AvatarFrame:
                userAsset.UserItems.RemoveAll((userItem) => userItem.ItemId == itemConfig.id);
                userInfo = await userInfoService.GetUserInfoAsync(shardId, playerId);
                if (userInfo == null) throw _userDataNotFound;
                if (userInfo.AvatarFrameItemID == itemId)
                    userInfo.AvatarFrameItemID = 0;
                break;
            case ItemType.IdleRewardBox:
                userAsset.UserItems.RemoveAll((userItem) => userItem.ItemId == itemConfig.id);
                var userIdleData = await dbCtx.GetUserIdleRewardInfo(shardId, playerId);
                if (userIdleData == null) throw _userDataNotFound;
                if (userIdleData.IdleRewardId == itemId)
                    userIdleData.IdleRewardId = 0;
                break;
            case ItemType.MainPageModel:
                // 这里只在items表里删除，客户端检测到物品消失后自动换回默认模型
                var modelItem = userAsset.UserItems.FirstOrDefault((userItem) => userItem.ItemId == itemConfig.id);
                if (modelItem != null)
                {
                    modelItem.ItemCount -= itemCount;
                    if (modelItem.ItemCount <= 0)
                        userAsset.UserItems.RemoveAll((userItem) => userItem.ItemId == itemConfig.id);
                }
                break;
        }

        return 0;
    }

    // 筛选已发货订单
    private async Task<int> GetClaimedOrderCountAsync(short shardId, long playerId, DateTime? startTime, DateTime? endTime, string? propId)
    {
        return await GetClaimedOrderCountAsync(shardId, playerId, startTime, endTime, 
            (string orderPropId) => propId is not null && orderPropId == propId );
    }

    // 筛选已发货订单
    private async Task<int> GetClaimedOrderCountAsync(short shardId, long playerId, DateTime? startTime, DateTime? endTime, Func<string, bool> orderFilter)
    {
        var claimedOrdersWithShard = await paidOrderWithShardRepository.GetPaidOrdersAsync(
            playerId, shardId, TrackingOptions.NoTracking, StaleReadOptions.NoStaleRead, [GeneralClaimStatus.Claimed]);
        return claimedOrdersWithShard.Where(order =>
            {
                var payload = JsonSerializer.Deserialize<SkuPayload>(order.Payload, DEFAULT_JSON_SERIALIZER_OPTIONS);
                if (payload is null)
                    return false;
                if ((startTime is not null && order.UpdatedAt < startTime) ||
                    (endTime is not null && order.UpdatedAt >= endTime))
                    return false;

                if (orderFilter(payload.PropId))
                    return true;
                return false;
            })
            .Count();
    }

    private async Task<int> GetClaimedAttachmentCountAsync(
        short? shardId,
        long playerId,
        ItemType itemType,
        string detailedKey,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        // 筛选已发货邮件
        var claimedAttachments
            = await platformItemRepository.GetNotifiesAsync(shardId, playerId, ClaimStatus.Claimed, false,
                [NotifyType.Mail, NotifyType.GiftCode]);
        return claimedAttachments.Where(attachment =>
            {
                if (startTime is not null && endTime is not null)
                {
                    if (attachment.UpdatedAt < startTime || attachment.UpdatedAt >= endTime)
                        return false;
                }
                var rewardList = JsonSerializer.Deserialize<List<RewardItemData>>(attachment.Payload);
                if (rewardList == null)
                    return false;
                // 这里假定商品只会发一个，不然就是运营失误了
                foreach (var reward in rewardList)
                {
                    var itemConfig = serverConfigService.GetItemConfigById(reward.Id);
                    if (itemConfig == null)
                        continue;
                    if (itemConfig.type == itemType && itemConfig.detailed_key == detailedKey)
                        return true;
                }

                return false;
            })
            .Count();
    }

    // 支付异常
    private readonly PayException _shardIdNullException =
        new(PayErrorCode.ShardIdNull, "shardId is null");
    private readonly PayException _payloadNullException =
        new(PayErrorCode.PayloadNull, "order payload is null");
    private readonly PayException _propIdNotValidException =
        new(PayErrorCode.PropIdNotValidInt, "propId is not valid");

    // 支付退款异常
    private readonly PayException _promotionStatusNullException =
        new(PayErrorCode.PromotionStatusNull, "promotion status is null");

    private readonly PayException _orderAlreadyRevokedException =
        new(PayErrorCode.OrderAlreadyRevoked, "order already revoked");

    private readonly PayException _orderNotFoundException = new(PayErrorCode.OrderNotFound, "order not found");

    private readonly PayException _activityNotFoundException = new(PayErrorCode.ActivityNotFound, "activity not found");

    private readonly PayException _userDataNotFound = new(PayErrorCode.UserDataNotFound, "user data not found");

    private readonly PayException _battlePassConfigNotFoundException
        = new(PayErrorCode.BattlePassConfigNotFound, "battle pass config not found");

    private readonly PayException _piggyBandDataNotFoundException
        = new(PayErrorCode.PiggyBankDataNotFound, "piggy band data not found");

    private readonly PayException _monthPassDataNotFoundException
        = new(PayErrorCode.MonthPassDataNotFound, "month pass data not found");

    private readonly PayException _slotMachineDataNotFoundException
        = new(PayErrorCode.SlotMachineDataNotFound, "slot machine data not found");

    private readonly PayException _slotMachineConfigNotFoundException
        = new(PayErrorCode.SlotMachineConfigNotFound, "slot machine config not found");

    private readonly PayException _treasureHuntDataNotFoundException
        = new(PayErrorCode.TreasureHuntDataNotFound, "treasure hunt data not found");

    private readonly PayException _refundBehaviorUndefinedForThisItemType =
        new(PayErrorCode.RefundBehaviorNotDefined, "cannot refund item of this type because refund behavior is not defined");
}

public sealed class PayException(PayErrorCode errorCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public PayErrorCode ErrorCode { get; } = errorCode;
}

public enum PayErrorCode
{
    None = 0,
    ShardIdNull = 1,
    PayloadNull = 2,
    PropIdNotValidInt = 3,
    OrderAlreadyRevoked = 4,
    ErrorSendRefundNotifyMail = 5,
    PromotionStatusNull = 6,
    OrderNotFound = 7,
    ActivityNotFound = 8,
    BattlePassConfigNotFound = 9,
    PiggyBankDataNotFound = 10,
    MonthPassDataNotFound = 11,
    SlotMachineDataNotFound = 12,
    SlotMachineConfigNotFound = 13,
    TreasureHuntDataNotFound = 14,
    RefundBehaviorNotDefined = 15,
    UserDataNotFound = 16,
}
