using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NuGet.Packaging;

namespace GameOutside.Controllers;

[Authorize]
public class UserPaymentController(
    IConfiguration configuration,
    ILogger<UserPaymentController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    UserAchievementService userAchievementService,
    DivisionService divisionService,
    IapPackageService iapPackageService,
    GameService gameService,
    UserAssetService userAssetService)
    : BaseApiController(configuration)
{
    private const string AdDesKey = "k5&ya;W%";

    public record struct FetchCommodityListReply(
        List<CommodityConfig> commonCommodities,
        List<UserCommodityBoughtRecord> boughtCommodities,
        List<UserDailyStoreItem> dailyStoreItems);

    [HttpPost]
    public async Task<ActionResult<FetchCommodityListReply>> FetchCommodityList()
    {
        return await context.WithRCUDefaultRetry<ActionResult<FetchCommodityListReply>>(async _ =>
        {
            var commodities = serverConfigService.GetCommodityConfigList();
            var division
                = await divisionService.GetDivisionNumberAsync(PlayerShard, PlayerId,
                    CreateOptions.DoNotCreateWhenNotExists);
            var divisionConfig = serverConfigService.GetDivisionConfig(division);
            var category = divisionConfig.category;
            commodities = commodities
                .Where(config => config.require_division < 0 || config.require_division == category)
                .ToList();
            var boughtCommodities = await context.GetAllBoughtCommodityRecords(PlayerShard, PlayerId);
            var dailyStoreIndex = await context.GetUserDailyStoreIndex(PlayerShard, PlayerId);
            var savingChanged = false;
            if (dailyStoreIndex == null)
            {
                dailyStoreIndex = new UserDailyStoreIndex() { ShardId = PlayerShard, PlayerId = PlayerId, Index = 0 };
                context.AddUserDailyStoreIndex(dailyStoreIndex);
                savingChanged = true;
            }

            var userAssets = await userAssetService.GetUserAssetsWithCardsAsync(PlayerShard, PlayerId);
            if (userAssets == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
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
                savingChanged = true;
            }

            if (savingChanged)
            {
                // 使用事务确保一致性
                await using var t = await context.Database.BeginTransactionAsync();
                await context.SaveChangesWithDefaultRetryAsync(false);
                await t.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();
            }

            return Ok(new FetchCommodityListReply()
            {
                commonCommodities = commodities,
                boughtCommodities = boughtCommodities,
                dailyStoreItems = dailyStoreItems
            });
        });
    }

    public record struct BuyCommodityReply(UserAssetsChange AssetsChange, GeneralReward Reward);

    [HttpPost]
    public async Task<ActionResult<BuyCommodityReply>> BuyCommodity(int commodityId, int payType, int buyCount)
    {
        var config = serverConfigService.GetCommodityConfigById(commodityId);
        if (config == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_COMMODITY_CONFIG });

        if ((payType == 0 && config.coin_price <= 0) || (payType != 0 && config.diamond_price <= 0))
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.WRONG_PAYMENT_OPTION });
        if (buyCount > 1 && !config.can_buy_10)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.WRONG_PAYMENT_OPTION });

        if (config.buy_max > 0)
        {
            // 检查是否买过
            var boughtCount = await context.GetCommodityBoughtCount(PlayerShard, PlayerId, commodityId);
            if (boughtCount + buyCount > config.buy_max)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.COMMODITY_BUYING_TOO_MUCH });
        }

        // 先计算出来奖励，以优化查询
        var reward = userItemService.CalculateCommodityReward(config, buyCount);

        return await context.WithRCUDefaultRetry<ActionResult<BuyCommodityReply>>(async _ =>
        {
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(reward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            if (payType == 0)
            {
                int coinPrice = config.coin_price * buyCount;
                if (userAsset.CoinCount < coinPrice)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.COIN_NOT_ENOUGH });
                userAsset.CoinCount -= coinPrice;
            }
            else
            {
                int diamondPrice = config.diamond_price * buyCount;
                if (userAsset.DiamondCount < diamondPrice)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.DIAMOND_NOT_ENOUGH });
                userAsset.DiamondCount -= diamondPrice;

                // 如果玩家的玉璧不够下一次相同的购买，那么触发限时礼包推销
                if (userAsset.DiamondCount < config.diamond_price)
                {
                    var userPromotionStatus = await iapPackageService.GetPromotionData(PlayerId, PlayerShard);
                    if (userPromotionStatus == null)
                        return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
                    if (!serverConfigService.TryGetParameterInt(Params.PromotionShowInterval,
                            out var packagePromotionShowInterval) ||
                        !serverConfigService.TryGetParameterString(Params.PromotedPackageIapIdList,
                            out var promotedPackageIapIds))
                        return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
                    var promotedPackageIapIdList = promotedPackageIapIds.Split('|').ToList();
                    if ((userPromotionStatus.LastPromotedPackage == "" &&
                         userPromotionStatus.PackagePromotionTime == 0) ||
                        // 玩家从未被推销过限时礼包
                        (userPromotionStatus.LastPromotedPackage != "" &&
                         TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(),
                             userPromotionStatus.PackagePromotionTime,
                             userAsset.TimeZoneOffset, 0) >= packagePromotionShowInterval))
                    // 玩家未完成所有限时礼包的购买，且冷却时间到了
                    {
                        if (userPromotionStatus.LastPromotedPackage == "")
                            userPromotionStatus.LastPromotedPackage = promotedPackageIapIdList[0];
                        userPromotionStatus.PackagePromotionTime = TimeUtils.GetCurrentTime();
                    }
                }
            }

            var assetChange = new UserAssetsChange();
            var result = await userItemService.UnpackItemList(userAsset, reward.ItemList, reward.CountList,
                GameVersion, assetChange);
            if (result == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            userAsset.DiamondCount += config.diamond * buyCount;
            userAsset.CoinCount += config.coin * buyCount;
            assetChange.FillAssetInfo(userAsset);

            // 记录一下购买的数量
            await context.RecordCommodityBoughtBy(PlayerShard, PlayerId, commodityId, 1);
            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(result.NewCardList, PlayerShard, PlayerId);
            assetChange.AchievementChanges = achievements;
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new BuyCommodityReply(assetChange, reward));
        });
    }

    [HttpPost]
    public async Task<ActionResult<BuyCommodityReply>> BuyDailyStoreItem(int itemId)
    {
        var dailyStoreItem = await context.GetDailyStoreItem(PlayerShard, PlayerId, itemId);
        if (dailyStoreItem == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_COMMODITY_CONFIG });

        var reward = new GeneralReward()
        {
            ItemList = [dailyStoreItem.ItemId],
            CountList = [dailyStoreItem.ItemCount]
        };

        return await context.WithRCUDefaultRetry<ActionResult<BuyCommodityReply>>(async _ =>
        {
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(reward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            // 检查是否买过
            if (dailyStoreItem.Bought)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.REPEAT_BUYING });

            // 扣费
            var buyingType = (DailyCommodityConfig.BuyingType)dailyStoreItem.PriceType;
            switch (buyingType)
            {
                case DailyCommodityConfig.BuyingType.FREE:
                    break;
                case DailyCommodityConfig.BuyingType.COIN:
                    if (userAsset.CoinCount < dailyStoreItem.Price)
                        return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.COIN_NOT_ENOUGH });
                    userAsset.CoinCount -= dailyStoreItem.Price;
                    break;
                case DailyCommodityConfig.BuyingType.DIAMOND:
                    if (userAsset.DiamondCount < dailyStoreItem.Price)
                        return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.DIAMOND_NOT_ENOUGH });
                    userAsset.DiamondCount -= dailyStoreItem.Price;
                    break;
            }

            //给东西发货
            var assetChange = new UserAssetsChange();
            var result = await userItemService.UnpackItemList(userAsset, reward.ItemList,
                reward.CountList, GameVersion, assetChange);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 记录一下购买过
            dailyStoreItem.Bought = true;

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(result.NewCardList, PlayerShard, PlayerId);
            assetChange.AchievementChanges = achievements;
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            assetChange.FillAssetInfo(userAsset);
            // 返回，成交
            return Ok(new BuyCommodityReply(assetChange, reward));
        });
    }

    [HttpPost]
    public async Task<ActionResult<BuyCommodityReply>> BuyIdleReward(int idleRewardId)
    {

        var idleRewardConf = serverConfigService.GetIdleRewardConfigById(idleRewardId);
        if (idleRewardConf == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

        if (idleRewardConf.acquire_method != IdleRewardAcquireMethod.PURCHASE_BY_DIAMOND &&
            idleRewardConf.acquire_method != IdleRewardAcquireMethod.PURCHASE_BY_COIN)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.IDLE_REWARD_CANNOT_BE_PURCHASED });

        return await context.WithRCUDefaultRetry<ActionResult<BuyCommodityReply>>(async _ =>
        {
            // idle reward 是物品
            var userAsset = await userAssetService.GetUserAssetsWithItemsAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            if (idleRewardConf.acquire_method == IdleRewardAcquireMethod.PURCHASE_BY_DIAMOND &&
                userAsset.DiamondCount < idleRewardConf.price)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.DIAMOND_NOT_ENOUGH });
            if (idleRewardConf.acquire_method == IdleRewardAcquireMethod.PURCHASE_BY_COIN &&
                userAsset.CoinCount < idleRewardConf.price)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.COIN_NOT_ENOUGH });

            if (idleRewardConf.acquire_method == IdleRewardAcquireMethod.PURCHASE_BY_DIAMOND)
                userAsset.DiamondCount -= idleRewardConf.price;
            if (idleRewardConf.acquire_method == IdleRewardAcquireMethod.PURCHASE_BY_COIN)
                userAsset.CoinCount -= idleRewardConf.price;

            var actualItemObtained = new GeneralReward()
            {
                ItemList = new List<int> { idleRewardId },
                CountList = new List<int> { 1 }
            };
            var (newCardList, result) = await userItemService.TakeReward(userAsset, actualItemObtained, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange?.AchievementChanges != null)
            {
                result.AssetsChange.AchievementChanges = achievements;
            }
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new BuyCommodityReply(result.AssetsChange!, actualItemObtained));
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserAssets>> GetUserAssets()
    {
        var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
        if (userAsset == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        return Ok(userAsset);
    }
    
    public record struct UnlockCardPoolSlotReply(int newExtraSlotCount, int newDiamondCount);
    [HttpPost]
    public async Task<ActionResult<UnlockCardPoolSlotReply>> UnlockCustomCardPoolSlot(int heroId)
    {
        // 校验参数合法性
        var heroConfig = serverConfigService.GetHeroConfigById(heroId);
        if (heroConfig == null)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());

        var costList = serverConfigService.GetParameterIntList(Params.ExtraEditableCardSlotPrice);
        if (costList == null ||
            !serverConfigService.TryGetParameterInt(Params.MaxExtraEditableCardCount, out var maxExtraSlot) ||
            !serverConfigService.TryGetParameterInt(Params.ExtraEditableCardSlotUnlockLevel, out var unlockLevel))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        
        return await context.WithRCUDefaultRetry<ActionResult<UnlockCardPoolSlotReply>>(async _ =>
        {
            var cardPool = await context.GetUserCustomCardPoolAsync(PlayerShard, PlayerId, heroId);
            if (cardPool == null)
            {
                cardPool = new UserCustomCardPool()
                {
                    HeroId = heroId,
                    PlayerId = PlayerId,
                    ShardId = PlayerShard,
                    CardList = [],
                    ExtraSlotCount = 0,
                };
                context.AddUserCustomCardPool(cardPool);
            }
            
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            
            if (userAsset.LevelData.Level < unlockLevel)
                return BadRequest(ErrorKind.USER_LEVEL_NOT_ENOUGH.Response());
            
            int cost = costList.Count > cardPool.ExtraSlotCount ?
                costList[cardPool.ExtraSlotCount] : costList[^1];
            if (userAsset.DiamondCount < cost)
                return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());

            if (cardPool.ExtraSlotCount >= maxExtraSlot)
                return BadRequest(ErrorKind.MAX_CARD_POOL_SLOT_REACHED.Response());

            userAsset.DiamondCount -= cost;
            cardPool.ExtraSlotCount += 1;
            
            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new UnlockCardPoolSlotReply() { newExtraSlotCount = cardPool.ExtraSlotCount, newDiamondCount = userAsset.DiamondCount});
        });
    }

    public record struct ItemUseData(int id, int count);

    #region 宝箱相关

    public record struct OpenTreasureBoxReply(
        UserAssetsChange? AssetsChange,
        List<RewardItemData> RewardList,
        List<int> BoxId);

    public record struct OpenTreasureBoxParam(
        Guid boxGuid,
        int count);

    [HttpPost]
    public async Task<ActionResult<OpenTreasureBoxReply>> OpenTreasureBox(List<OpenTreasureBoxParam> boxList)
    {
        Dictionary<Guid, int> guidDict = new();
        foreach (var box in boxList)
            guidDict[box.boxGuid] = box.count;

        return await context.WithRCUDefaultRetry<ActionResult<OpenTreasureBoxReply>>(async _ =>
        {
            var userGameInfo = await gameService.GetUserGameInfoByIdAsync(PlayerShard, PlayerId);
            if (userGameInfo == null)
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            var treasureBoxes = userAsset.UserTreasureBoxes.Where(box => guidDict.ContainsKey(box.Id)).ToList();
            if (treasureBoxes.Count != guidDict.Count)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            var assetChange = new UserAssetsChange();
            var finalReward = new List<RewardItemData>();

            await using var transaction = await context.Database.BeginTransactionAsync();
            var newCardList = new List<UserCard>();
            var cardChangeSet = new HashSet<UserCard>();
            var boxConfigs = new List<TreasureBoxConfig>();
            var boxCounts = new List<int>();
            foreach (var treasureBox in treasureBoxes)
            {
                var itemId = treasureBox.ItemId;
                var config = serverConfigService.GetTreasureBoxConfigById(itemId);
                if (config == null)
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

                // 逻辑走到这里就可以开箱了
                int boxCount = Math.Max(1, treasureBox.ItemCount);
                int openCount = Math.Clamp(guidDict[treasureBox.Id], 1, boxCount);
                boxConfigs.Add(config);
                boxCounts.Add(openCount);

                // 暂时先用for循环，10个应该还行
                for (int i = 0; i < openCount; i++)
                {
                    var (reward, errorKind)
                        = await userItemService.RandomTreasureBoxReward(userGameInfo, userAsset, PlayerShard, PlayerId,
                            config, GameVersion);
                    if (errorKind != ErrorKind.SUCCESS)
                        return BadRequest(new ErrorResponse() { ErrorCode = (int)errorKind });

                    var rewardList = new List<RewardItemData>();
                    reward.CardList.Shuffle();
                    rewardList.AddRange(reward.CurrencyList);
                    rewardList.AddRange(reward.CardList);
                    var result = await userItemService.UnpackTreasureBoxRewardList(userAsset, rewardList, assetChange, GameVersion);
                    if (result == null)
                        return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
                    newCardList.AddRange(result.NewCardList);
                    cardChangeSet.AddRange(result.CardChangeSet);
                    // 合并到最终奖励
                    finalReward.AddRange(rewardList);
                }

                // 扣除宝箱
                if (boxCount - openCount == 0)
                {
                    userAsset.UserTreasureBoxes.Remove(treasureBox);
                    assetChange.TreasureBoxChange.RemoveList.Add(treasureBox.Id);
                }
                else if (boxCount - openCount > 0)
                {
                    treasureBox.ItemCount -= openCount;
                    assetChange.TreasureBoxChange.ModifyList.Add(treasureBox);
                }
                else // 永远不应该出现的情况
                    return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_INPUT });
            }

            finalReward.DistinctAndMerge();

            // 记录每日任务进度
            await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.OPEN_TREASURE_BOX, boxCounts.Sum(),
                PlayerShard, PlayerId, userAsset.TimeZoneOffset);
            userAssetService.DetachUserAssetCards(userAsset);
            await context.SaveChangesWithDefaultRetryAsync(false);
            // upsert
            // 卡牌变更 upsert
            await userAssetService.UpsertUserCardsAsync(cardChangeSet);
            // 开箱成就
            var achievements
                = await userAchievementService.IncreaseTreasureBoxAchievementAsync(boxConfigs, boxCounts, PlayerShard, PlayerId);
            assetChange.AchievementChanges.AddRange(achievements);
            // 卡牌成就
            achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            assetChange.AchievementChanges.AddRange(achievements);
            await transaction.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            assetChange.FillAssetInfo(userAsset);

            return Ok(new OpenTreasureBoxReply()
            {
                AssetsChange = assetChange,
                RewardList = finalReward,
                BoxId = treasureBoxes.Select(box => box.ItemId).ToList()
            });
        });
    }

    #endregion

    #region 广告

    [HttpPost]
    public async Task<List<UserMallAdvertisement>> ListMallAdsStatus()
    {
        return await context.ListUserMallAdStatus(PlayerShard, PlayerId);
    }


    private T? CheckMessageValid<T>(string content, string hash, string desKey) where T : class
    {
        string json;
        try
        {
            json = EncryptHelper.EncryptHelper.DesDecrypt(content, desKey);
        }
        catch (Exception e)
        {
            return null;
        }

        var contentHash = EncryptHelper.EncryptHelper.CustomHash(json);
        if (contentHash != hash)
        {
            return null;
        }

        T? messageObj;
        try
        {
            messageObj = JsonConvert.DeserializeObject<T>(json);
            if (messageObj == null)
                return null;
        }
        catch (Exception e)
        {
            return null;
        }

        return messageObj;
    }

    public record struct GetMallAdsRewardReply(UserMallAdvertisement Ads, TakeRewardResult Result);

    [HttpPost]
    [Obsolete("功能已删除")]
    public async Task<ActionResult<GetMallAdsRewardReply>> GetMallAdsReward(string content, string hash)
    {
        var message = CheckMessageValid<GetMallAdsRewardMessage>(content, hash, AdDesKey);
        if (message == null)
            return BadRequest(ErrorKind.MESSAGE_INVALID.Response());

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (currentTime - message.TimeStamp > 10)
            return BadRequest(ErrorKind.MESSAGE_INVALID.Response());

        var config = serverConfigService.GetMallAdsConfig(message.Id);
        if (config == null)
            return BadRequest(ErrorKind.NO_MALL_ADS_CONFIG.Response());

        var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
        if (userAsset == null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());

        var advertisement = await context.GetUserMallAdvertisement(PlayerShard, PlayerId, message.Id);
        if (advertisement == null)
        {
            advertisement = new UserMallAdvertisement()
            {
                ShardId = PlayerShard,
                PlayerId = PlayerId,
                Id = message.Id,
                Count = config.count,
                LastTime = currentTime,
            };
            await context.AddUserMallAdvertisement(advertisement);
        }

        // 检查还有没有领取的机会
        if (!serverConfigService.TryGetParameterInt(Params.MallAdTimeOffset, out var timeOffset))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        int offsetDay =
            TimeUtils.GetDayDiffBetween(currentTime, advertisement.LastTime, userAsset.TimeZoneOffset, timeOffset);
        // 1. 更新次数
        if (offsetDay > 0)
        {
            advertisement.Count = config.count;
        }

        // 2. 扣除次数
        if (advertisement.Count <= 0)
            return BadRequest(ErrorKind.NO_MALL_ADS_COUNT_LEFT.Response());

        // 更新时间戳并扣除次数
        advertisement.LastTime = currentTime;
        advertisement.Count--;

        // 发奖
        var generalReward
            = new GeneralReward() { ItemList = config.item_list.ToList(), CountList = config.count_list.ToList() };
        var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
        if (result == null)
            return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

        await using var t = await context.Database.BeginTransactionAsync();
        await context.SaveChangesWithDefaultRetryAsync(false);
        var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
        if (result.AssetsChange != null)
            result.AssetsChange.AchievementChanges.AddRange(achievements);
        await t.CommitAsync();
        context.ChangeTracker.AcceptAllChanges();
        return Ok(new GetMallAdsRewardReply() { Ads = advertisement, Result = result, });
    }

    #endregion

    [HttpPost]
    // 接口暂未被使用
    public async Task<ActionResult<bool>> CanPurchaseIap(string propId)
    {
        var iapConfig = serverConfigService.GetSkuItemConfig(propId);
        if (iapConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
        if (iapConfig.buy_limit == 0)
            return Ok(true);
        var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
        if (timeZoneOffset is null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());
        var purchaseCount = 0;
        if (iapConfig.share_limit_with.Count == 0)
            purchaseCount = await iapPackageService.GetIapPurchaseCountWithinTimeAsync(PlayerShard, PlayerId, propId,
                iapConfig, timeZoneOffset.Value);
        else
            foreach (var groupMemberId in iapConfig.share_limit_with)
            {
                var targetConfig = serverConfigService.GetSkuItemConfig(groupMemberId);
                if (targetConfig == null)
                    return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                purchaseCount += await iapPackageService.GetIapPurchaseCountWithinTimeAsync(PlayerShard, PlayerId,
                    groupMemberId, targetConfig, timeZoneOffset.Value);
            }

        return Ok(purchaseCount < iapConfig.buy_limit);
    }

    [HttpPost]
    public async Task<ActionResult<Dictionary<string, int>>> IapPurchaseTimeLeft()
    {
        Dictionary<string, int> purchaseTimeRecord = new();
        Dictionary<string, int> result = new();
        var configList = serverConfigService.GetSkuItemConfigList();
        foreach (var config in configList)
        {
            if (config.buy_limit == 0) continue;
            result.Add(config.prop_id, config.buy_limit);
        }

        var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
        if (timeZoneOffset is null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());

        var purchaseHistory = await iapPackageService.GetFullPurchaseListAsync(PlayerShard, PlayerId);
        foreach (var record in purchaseHistory)
        {
            var config = serverConfigService.GetSkuItemConfig(record.IapItemId);
            if (config == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
            if (!TimeUtils.IfRecordTimeIsInRange(config.limit_refresh_interval, config.sp_limit_refresh_rule,
                    TimeUtils.GetCurrentTime(), record.WhenPurchased, timeZoneOffset.Value))
                continue;
            purchaseTimeRecord.TryAdd(config.prop_id, 0);
            purchaseTimeRecord[record.IapItemId]++;
        }

        foreach (var record in result)
        {
            var config = serverConfigService.GetSkuItemConfig(record.Key);
            if (config == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
            if (config.share_limit_with.Count == 0)
            {
                if (purchaseTimeRecord.ContainsKey(record.Key))
                    result[record.Key] -= purchaseTimeRecord[record.Key];
            }
            else
            {
                foreach (var groupMemberId in config.share_limit_with)
                    if (purchaseTimeRecord.ContainsKey(groupMemberId))
                        result[record.Key] -= purchaseTimeRecord[groupMemberId];
            }
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentAndPromotionStatusReply>> GetPromotedPackageInfo()
    {
        var status = await iapPackageService.GetPromotionData(PlayerId, PlayerShard);
        if (status == null)
            return BadRequest(ErrorKind.NO_USER_RECORDS.Response());
        return Ok(new PaymentAndPromotionStatusReply()
        {
            LastPromotedPackageId = status.LastPromotedPackage,
            WhenPromoted = status.PackagePromotionTime,
            IceBreakingPromotionStatus = status.IceBreakingPayPromotion,
            DoubleDiamondBonusTriggerRecords = status.DoubleDiamondBonusTriggerRecords.Keys.ToHashSet(),
        });
    }
}