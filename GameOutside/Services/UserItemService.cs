using ChillyRoom.BuildingGame.Models;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Util;
using ChillyRoom.Functions.DBModel;
using GameOutside.Repositories;
using GameOutside.Services;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;

namespace GameOutside;

public class UserItemService(
    BuildingGameDB dbCtx,
    IUserItemRepository userItemRepository,
    BattlePassService battlePassService,
    ActivityService activityService,
    ServerDataService serverDataService,
    UserAchievementService userAchievementService,
    ServerConfigService serverConfigService,
    ILogger<UserItemService> logger)
{
    /// <summary>
    /// 获取玩家物品列表
    /// </summary>
    public ValueTask<List<UserItem>> GetReadonlyUserItemsAsync(short shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ =>
            userItemRepository.GetUserItemsAsync(shardId, playerId, TrackingOptions.NoTracking));
    }

    public ValueTask<bool> HasItemAsync(short shardId, long playerId, int itemId)
    {
        return dbCtx.WithDefaultRetry(_ => userItemRepository.HasItemAsync(shardId, playerId, itemId));
    }

    public async Task<(UnpackItemListResult?, UserAssets)> CreateDefaultUserAssets(
        short shardId,
        long playerId,
        int timeZoneOffset,
        string gameVersion)
    {
        var userAsset = new UserAssets()
        {
            ShardId = shardId,
            PlayerId = playerId,
            CoinCount = 0,
            DiamondCount = 0,
            Heroes = [],
            LevelData = new UserLevelData() { Level = 0, LevelScore = 0, RewardStatusList = new List<long>() },
            DifficultyData = new UserDifficultyData() { Levels = [], Stars = [] },
            TimeZoneOffset = timeZoneOffset,
            UserItems = [],
            UserCards = [],
            UserTreasureBoxes = [],
        };

        var defaultItems = serverConfigService.GetDefaultItems();
        var itemList = defaultItems.Select(config => config.id).ToList();
        var countList = itemList.Select(id => 1).ToList();
        // 这里走一下统一的接口，因为需要同步处理一些成就的东西
        var result = await UnpackItemList(userAsset, itemList, countList, gameVersion);
        return (result, userAsset);
    }

    public GeneralReward CalculateCommodityReward(CommodityConfig commodityConfig, int buyCount)
    {
        var reward = new GeneralReward();
        var randomKey = Random.Shared.Next();
        if (commodityConfig.weight_list.Count > 0)
        {
            for (int i = 0; i < buyCount; i++)
            {
                // 随机开宝箱给
                var totalWeight = commodityConfig.accu_weight_list[^1];
                var randomWeight = randomKey % totalWeight;

                int CompareFunc(int value0, int value1)
                {
                    if (value0 == value1) return 0;
                    return value0 > value1 ? 1 : -1;
                }

                var index = commodityConfig.accu_weight_list.BinarySearchBiggerOrEqual(randomWeight, CompareFunc);
                var item = commodityConfig.item_list[index];
                reward.ItemList.Add(item);
                reward.CountList.Add(commodityConfig.count_list[index]);
            }
        }
        else
        {
            // 全都给
            for (int i = 0; i < commodityConfig.item_list.Length; i++)
            {
                int itemId = commodityConfig.item_list[i];
                int itemCount = commodityConfig.count_list[i] * buyCount;
                reward.ItemList.Add(itemId);
                reward.CountList.Add(itemCount);
            }
        }

        return reward;
    }

    public async Task<UnpackItemListResult?> UnpackTreasureBoxRewardList(
        UserAssets userAssets,
        List<RewardItemData> rewardList,
        UserAssetsChange? assetsChange,
        string gameVersion)
    {
        var itemList = rewardList.Select(item => item.Id).ToList();
        var countList = rewardList.Select(item => item.Count).ToList();
        var result = await UnpackItemList(userAssets, itemList, countList, gameVersion, assetsChange);
        rewardList.RemoveAll(item => !itemList.Contains(item.Id));
        for (int i = 0; i < itemList.Count; i++)
        {
            var rewardFound = rewardList.Find(item => item.Id == itemList[i]);
            if (rewardFound != null) rewardFound.Count = countList[i];
            else rewardList.Add(new RewardItemData() { Id = itemList[i], Count = countList[i] });
        }

        return result;
    }

    private void ConvertSurplusCardToMagicCardAndRemoveRedundantItem(
        UserAssets userAssets,
        List<int> itemList,
        List<int> countList)
    {
        List<UserCard> cards = userAssets.UserCards;
        Dictionary<ItemQuality, int> magicCardToAdd = new();
        Dictionary<ItemQuality, int> magicCardIdx = new();
        Dictionary<int, int> temporaryCard = new();
        List<int> idxToRemove = new();
        for (var i = 0; i < itemList.Count; ++i)
        {
            var itemId = itemList[i];
            var itemConfig = serverConfigService.GetItemConfigById(itemId);
            if (itemConfig == null) continue;
            switch (itemConfig.type)
            {
                case ItemType.SoldierCard:
                case ItemType.BuildingCard:
                case ItemType.TowerCard:
                    int surplus = 0;
                    var card = cards.FirstOrDefault(c => c.CardId == itemConfig.id);
                    if (card != null)
                    {
                        temporaryCard.TryGetValue(itemConfig.id, out var tempCount);
                        var cardNeedExp = GetCardLeftExp(card) - tempCount;
                        if (cardNeedExp < countList[i])
                        {
                            surplus = countList[i] - cardNeedExp;
                            countList[i] = cardNeedExp;
                            if (cardNeedExp <= 0) idxToRemove.Add(i);
                            magicCardToAdd.TryAdd(itemConfig.quality, 0);
                            magicCardToAdd[itemConfig.quality] += surplus;
                        }
                    }
                    temporaryCard.TryAdd(itemConfig.id, 0);
                    temporaryCard[itemConfig.id] += countList[i];
                    break;
                case ItemType.MagicCard:
                    magicCardIdx[itemConfig.quality] = i;
                    break;
                case ItemType.Hero:
                    var heroConfig = serverConfigService.GetHeroConfigByKey(itemConfig.detailed_key);
                    if (heroConfig == null)
                        break;
                    // 这里防御性处理一下领袖的重复发放，发放多次视为发放成功, 不然一旦重复发放领袖，各种包含该领袖的一键领取功能就全部失败了
                    if (userAssets.Heroes.Contains(heroConfig.id))
                        idxToRemove.Add(i);
                    break;
                default: continue;
            }
        }

        foreach (var iter in magicCardToAdd)
        {
            var quality = iter.Key;
            if (magicCardIdx.ContainsKey(quality))
                countList[magicCardIdx[quality]] += iter.Value;
            else
            {
                var conf = serverConfigService.GetMagicCardConfigByQuality(iter.Key);
                if (conf != null)
                {
                    itemList.Add(conf.id);
                    countList.Add(iter.Value);
                }
            }
        }

        for (int i = idxToRemove.Count - 1; i >= 0; i--)
        {
            itemList.RemoveAt(idxToRemove[i]);
            countList.RemoveAt(idxToRemove[i]);
        }
    }

    private async Task ConvertItemPackageToItems(
        long playerId,
        short shardId,
        List<int> itemList,
        List<int> countList,
        string gameVersion,
        long timezoneOffset,
        ActivityCsgoStyleLottery? activityCsgoLottery = null)
    {
        List<int> idxToRemove = new();
        Dictionary<int, int> itemFromPackage = new();
        Dictionary<int, int> itemIdToIdx = new();
        int coinAdd = 0;
        int diamondAdd = 0;
        for (var i = 0; i < itemList.Count; ++i)
        {
            var itemId = itemList[i];
            var itemConfig = serverConfigService.GetItemConfigById(itemId);
            if (itemConfig == null) continue;
            switch (itemConfig.type)
            {
                case ItemType.ItemPackage:
                    var config = serverConfigService.GetPackageConfigById(itemId);
                    if (config == null) continue;
                    for (int ii = 0; ii < config.item_list.Length; ++ii)
                    {
                        if (!itemFromPackage.ContainsKey(config.item_list[ii]))
                            itemFromPackage[config.item_list[ii]] = 0;
                        itemFromPackage[config.item_list[ii]] += config.item_count[ii];
                    }

                    coinAdd += config.coin_count;
                    diamondAdd += config.diamond_count;
                    idxToRemove.Add(i);
                    break;
                case ItemType.CsgoLotteryPass:
                    // CSGO风格抽奖通行证
                    var csgoTimeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, gameVersion);
                    if (csgoTimeConfig == null) continue;
                    
                    // 获取通行证配置
                    var passConfig = serverConfigService.GetCsgoLotteryPassConfigByItemId(csgoTimeConfig.id, itemId);
                    if (passConfig == null) continue;

                    // 获取或创建活动数据
                    if (activityCsgoLottery == null)
                    {
                        activityCsgoLottery = await activityService.GetCsgoStyleLotteryDataAsync(playerId, shardId, csgoTimeConfig.id, TrackingOptions.Tracking);
                        activityCsgoLottery ??= activityService.CreateDefaultCsgoStyleLotteryData(playerId, shardId, csgoTimeConfig.id);
                    }

                    // 检查是否已购买
                    if ((activityCsgoLottery.ActivityPremiumPassStatus & (1L << passConfig.level)) > 0)
                    {
                        // 已经购买了这个等级，不能重复购买
                        continue;
                    }

                    // 激活通行证
                    activityCsgoLottery.ActivityPremiumPassStatus |= (1L << passConfig.level);
                    
                    // 发放一次性奖励
                    for (int ri = 0; ri < passConfig.reward_item.Count; ri++)
                    {
                        itemFromPackage.TryAdd(passConfig.reward_item[ri], 0);
                        itemFromPackage[passConfig.reward_item[ri]] += passConfig.reward_count[ri];
                    }
                    
                    // 每日奖励会由玩家检测到通行证状态更新后再自动请求发放

                    break;
                default:
                    itemIdToIdx[itemId] = i;
                    break;
            }
        }

        foreach (var iter in itemFromPackage)
        {
            var itemId = iter.Key;
            var itemCount = iter.Value;
            if (itemIdToIdx.ContainsKey(itemId))
                countList[itemIdToIdx[itemId]] += itemCount;
            else
            {
                itemList.Add(itemId);
                countList.Add(itemCount);
            }
        }

        if (diamondAdd > 0)
        {
            if (itemIdToIdx.ContainsKey(0))
                countList[itemIdToIdx[0]] += diamondAdd;
            else
            {
                itemList.Add(0);
                countList.Add(diamondAdd);
            }
        }

        if (coinAdd > 0)
        {
            if (itemIdToIdx.ContainsKey(1))
                countList[itemIdToIdx[1]] += coinAdd;
            else
            {
                itemList.Add(1);
                countList.Add(coinAdd);
            }
        }

        for (int i = idxToRemove.Count - 1; i >= 0; i--)
        {
            itemList.RemoveAt(idxToRemove[i]);
            countList.RemoveAt(idxToRemove[i]);
        }
    }

    /// <summary>
    /// 提前计算物品列表中包含的用户资产类型
    /// 用于优化UserAsset的联表查询
    /// </summary>
    /// <returns>UserAssetIncludeOptions</returns>
    public UserAssetIncludeOptions CalculateUserAssetIncludeOptions(IEnumerable<int> itemList)
    {
        var includeOption = UserAssetIncludeOptions.NoInclude;
        foreach (var itemId in itemList)
        {
            if (includeOption == UserAssetIncludeOptions.IncludeAll)
                return UserAssetIncludeOptions.IncludeAll;
            var itemConfig = serverConfigService.GetItemConfigById(itemId);
            if (itemConfig == null) continue;
            switch (itemConfig.type)
            {
                case ItemType.Currency:
                case ItemType.Hero:
                case ItemType.FortuneBag:
                case ItemType.PiggyBank:
                case ItemType.MonthPass:
                    break;
                case ItemType.ItemPackage:
                    // 礼包需要看礼包里的内容
                    var packageConfig = serverConfigService.GetPackageConfigById(itemId);
                    if (packageConfig == null) continue;
                    includeOption |= CalculateUserAssetIncludeOptions(packageConfig.item_list);
                    break;
                case ItemType.SoldierCard:
                case ItemType.BuildingCard:
                case ItemType.TowerCard:
                case ItemType.MagicCard:
                    // 卡牌比较特殊，因为有可能会把多余的卡牌转成魔法卡
                    includeOption |= UserAssetIncludeOptions.IncludeCards |
                                     UserAssetIncludeOptions.IncludeItems;
                    break;
                case ItemType.TreasureBox:
                    includeOption |= UserAssetIncludeOptions.IncludeTreasureBoxes;
                    break;
                default:
                    includeOption |= UserAssetIncludeOptions.IncludeItems;
                    break;
            }
        }
        return includeOption;
    }

    /// <summary>
    /// 提前计算物品列表中包含的用户资产类型（退款专用）
    /// 因为对消耗品进行退货的时候不需要改动这些消耗品的数量而是直接扣玉璧
    /// </summary>
    /// <returns>UserAssetIncludeOptions</returns>
    public UserAssetIncludeOptions CalculateUserAssetIncludeOptionsForRefund(IEnumerable<int> itemList)
    {
        var includeOption = UserAssetIncludeOptions.NoInclude;
        foreach (var itemId in itemList)
        {
            if (includeOption == UserAssetIncludeOptions.IncludeAll)
                return UserAssetIncludeOptions.IncludeAll;
            var itemConfig = serverConfigService.GetItemConfigById(itemId);
            if (itemConfig == null) continue;
            switch (itemConfig.type)
            {
                case ItemType.NameCard:
                case ItemType.Avatar:
                case ItemType.AvatarFrame:
                case ItemType.IdleRewardBox:
                case ItemType.MainPageModel:
                    includeOption |= UserAssetIncludeOptions.IncludeItems;
                    break;
                default:
                    break;
            }
        }
        return includeOption;
    }
    
    /// <summary>
    /// 发放物品
    /// 除非特殊需求，否则尽量使用 TakeReward 接口
    /// 注意所有变化的卡牌和物品需要添加到返回值的CardChangeList和ItemChangeList中
    /// </summary>
    /// <returns></returns>
    public async Task<UnpackItemListResult?> UnpackItemList(
        UserAssets userAssets,
        List<int> itemList,
        List<int> countList,
        string gameVersion,
        UserAssetsChange? assetsChange = null,
        int explorePoint = 0,
        ActivityUnrivaledGod? activityUnrivaledGod = null,
        ActivityTreasureHunt? activityTreasureHunt = null,
        ActivityCsgoStyleLottery? activityCsgoLottery = null)
    {
        short shardId = userAssets.ShardId;
        long playerId = userAssets.PlayerId;
        
        var result = new UnpackItemListResult();
        List<UserItem> items = userAssets.UserItems;
        List<UserCard> cards = userAssets.UserCards;
        List<UserTreasureBox> boxes = userAssets.UserTreasureBoxes;
        await ConvertItemPackageToItems(playerId, shardId, itemList, countList, gameVersion, userAssets.TimeZoneOffset, activityCsgoLottery);
        ConvertSurplusCardToMagicCardAndRemoveRedundantItem(userAssets, itemList, countList);

        var itemChange = assetsChange?.ItemChange;
        var cardChange = assetsChange?.CardChange;
        var treasureBoxChange = assetsChange?.TreasureBoxChange;
        var heroAddition = assetsChange?.HeroAdditions;

        for (var i = 0; i < itemList.Count; ++i)
        {
            var itemId = itemList[i];
            var itemConfig = serverConfigService.GetItemConfigById(itemId);
            if (itemConfig == null)
                return null;
            switch (itemConfig.type)
            {
                case ItemType.Currency:
                    await AddCurrency(userAssets, itemId, countList[i], gameVersion, assetsChange, 
                        activityUnrivaledGod, activityTreasureHunt, activityCsgoLottery);
                    break;
                case ItemType.SoldierCard:
                case ItemType.BuildingCard:
                case ItemType.TowerCard:
                    var card = cards.FirstOrDefault(c => c.CardId == itemConfig.id);
                    if (card != null)
                    {
                        card.CardExp += countList[i];
                        card.UpdatedAt = DateTime.UtcNow;
                        cardChange?.ModifyList.Add(card);
                        result.CardChangeSet.Add(card);
                    }
                    else
                    {
                        var newCard = new UserCard()
                        {
                            ShardId = shardId,
                            CardId = itemConfig.id,
                            CardExp = countList[i],
                            PlayerId = playerId,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            CardArenaDifficultyReached = -1,
                            CardArenaLevelReached = -1,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        };
                        cards.Add(newCard);
                        cardChange?.AddList.Add(newCard);
                        result.NewCardList.Add(newCard);
                        result.CardChangeSet.Add(newCard);
                    }

                    break;
                case ItemType.TreasureBox:
                    var treasureBox
                        = CreateUserTreasureBoxData(itemId, shardId, playerId, countList[i], explorePoint);
                    if (treasureBox != null)
                    {
                        boxes.Add(treasureBox);
                        treasureBoxChange?.AddList.Add(treasureBox);
                    }
                    break;
                case ItemType.BattlePass:
                    // 新手战令
                    if (itemConfig.detailed_key.Equals(Consts.FirstPassItemKey))
                    {
                        var firstPassInfo
                            = await battlePassService.GetUserBattlePassInfoByPassIdAsync(shardId, playerId, -1);
                        firstPassInfo ??= battlePassService.AddUserBattlePassInfo(shardId, playerId, -1);

                        // 这里使用++SuperPassLevel原本是为了可能有多档付费预留的，但实操下来不会有多档付费了，直接写成1
                        firstPassInfo.SuperPassLevel = 1;
                        itemChange?.SpecialItemList.Add(new UserItem()
                        {
                            PlayerId = playerId,
                            ShardId = shardId,
                            ItemId = itemId,
                            ItemCount = firstPassInfo.SuperPassLevel,
                        });
                    }
                    // 赛季战令
                    else if (itemConfig.detailed_key.Equals(Consts.BattlePassItemKey))
                    {
                        var currentPassId = serverConfigService.GetActiveBattlePassId();
                        if (currentPassId == -1)
                            return null;
                        var battlePassInfo =
                            await battlePassService.GetUserBattlePassInfoByPassIdAsync(shardId, playerId,
                                currentPassId);
                        battlePassInfo ??= battlePassService.AddUserBattlePassInfo(shardId, playerId, currentPassId);

                        // 这里使用++SuperPassLevel原本是为了可能有多档付费预留的，但实操下来不会有多档付费了，直接写成1
                        battlePassInfo.SuperPassLevel = 1;
                        itemChange?.SpecialItemList.Add(new UserItem()
                        {
                            PlayerId = playerId,
                            ShardId = shardId,
                            ItemId = itemId,
                            ItemCount = battlePassInfo.SuperPassLevel,
                        });
                    }

                    break;
                case ItemType.Hero:
                    var heroConfig = serverConfigService.GetHeroConfigByKey(itemConfig.detailed_key);
                    if (heroConfig == null)
                        return null;
                    // 这里防御性处理一下领袖的重复发放，发放多次视为发放成功, 不然一旦重复发放领袖，各种包含该领袖的一键领取功能就全部失败了
                    if (userAssets.Heroes.Contains(heroConfig.id))
                        break;
                    userAssets.Heroes.Add(heroConfig.id);
                    heroAddition?.Add(heroConfig.id);
                    break;
                case ItemType.FortuneBag:
                {
                    // 到了物品发放这个环节了，直接就给最大值。
                    var timeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivityFortuneBag, gameVersion);
                    if (timeConfig == null)
                        return null;
                    var fortuneBagConfig = serverConfigService.GetActivityFortuneBagConfigByActivityId(timeConfig.id);
                    if (fortuneBagConfig == null)
                        return null;
                    bool fortuneBagFull = await OnFortuneBagAdded(playerId, shardId, countList[i], fortuneBagConfig);
                    if (fortuneBagFull)
                    {
                        itemList.Add(fortuneBagConfig.max_fortune_bag_reward);
                        countList.Add(1);
                    }

                    // 购买的福袋不会被存在UserAsset里，但需要通知一下客户端
                    itemChange?.SpecialItemList.Add(new UserItem()
                    {
                        ShardId = shardId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        ItemCount = countList[i],
                    });
                    break;
                }
                case ItemType.PiggyBank:
                {
                    var piggyBankInfo = await activityService.GetPiggyBankStatusAsync(playerId, shardId);
                    if (piggyBankInfo == null)
                        piggyBankInfo = activityService.CreateDefaultPiggyBank(playerId, shardId);
                    // 这里原本是++PaidLevel, 但最终实操下来不会再有新的付费等级了，多发了还会出问题。所以直接写死1就行了
                    piggyBankInfo.PaidLevel = 1;
                    itemChange?.SpecialItemList.Add(new UserItem()
                    {
                        ShardId = shardId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        ItemCount = countList[i],
                    });
                    break;
                }
                case ItemType.MonthPass:
                {
                    if (!serverConfigService.TryGetParameterInt(Params.MonthPassFirstDayDiamond,
                            out var diamondRewardAmount))
                        return null;
                    diamondRewardAmount *= countList[i];
                    var passInfo = await dbCtx.GetMonthPassInfo(playerId, shardId);
                    if (passInfo == null)
                        passInfo = await dbCtx.AddMonthPassInfo(playerId, shardId);
                    var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), passInfo.PassAcquireTime,
                        userAssets.TimeZoneOffset, 0);
                    if (dayDiff >= passInfo.PassDayLength)
                    {
                        passInfo.PassAcquireTime = TimeUtils.GetCurrentTime();
                        passInfo.PassDayLength = countList[i] * 30;
                        //如果不是给已经激活的月卡延期，那么就自动帮玩家做第一天的签到
                        passInfo.LastRewardClaimDay = 0;
                        passInfo.RewardClaimStatus = 1;
                        if (!serverConfigService.TryGetParameterInt(Params.MonthPassDailyDiamond,
                                out var extraDiamond))
                            return null;
                        diamondRewardAmount += extraDiamond;
                    }
                    else
                        passInfo.PassDayLength += countList[i] * 30;

                    // 发放月卡的购买奖励
                    itemList.Add(0);
                    countList.Add(diamondRewardAmount);
                    // 在reward fly view展示一下玩家获得的月卡
                    itemChange?.SpecialItemList.Add(new UserItem()
                    {
                        ShardId = shardId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        ItemCount = countList[i],
                    });
                    break;
                }
                case ItemType.SlotMachineDoubleUp:
                {
                    var timeConfig = activityService.GetOpeningActivityByType(ActivityType.ActivitySlotMachine, gameVersion);
                    if (timeConfig == null)
                        return null;
                    var doubleUpSuccess =
                        await OnSlotMachineRewardDoubleUp(dbCtx, playerId, shardId, timeConfig.id, itemId);
                    if (!doubleUpSuccess)
                        return null;
                    itemChange?.SpecialItemList.Add(new UserItem()
                    {
                        ShardId = shardId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        ItemCount = countList[i],
                    });
                    break;
                }
                case ItemType.CsgoLotteryPass:
                {
                    // 通知客户端
                    itemChange?.SpecialItemList.Add(new UserItem()
                    {
                        ShardId = shardId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        ItemCount = countList[i],
                    });
                    break;
                }
                default:
                    var item = items.FirstOrDefault((item => item.ItemId == itemId));
                    if (item == null)
                    {
                        var newItem = new UserItem()
                        {
                            ShardId = shardId,
                            PlayerId = playerId,
                            ItemId = itemId,
                            ItemCount = countList[i],
                        };
                        items.Add(newItem);
                        itemChange?.AddList.Add(newItem);
                        result.ItemChangeSet.Add(newItem);
                    }
                    else
                    {
                        item.ItemCount += countList[i];
                        itemChange?.ModifyList.Add(item);
                        result.ItemChangeSet.Add(item);
                    }

                    break;
            }
        }

        return result;
    }

    public async Task<(List<UserCard>, TakeRewardResult?)> TakeReward(
        UserAssets userAssets,
        GeneralReward reward,
        string gameVersion,
        ActivityUnrivaledGod? activityUnrivaledGod = null,
        ActivityTreasureHunt? activityTreasureHunt = null,
        ActivityCsgoStyleLottery? activityCsgoLottery = null)
    {
        var assetChangeRecorder = new UserAssetsChange();
        var rewardItemList = reward.ItemList;
        var rewardCountList = reward.CountList;
        var unpackItemListResult = await UnpackItemList(userAssets, rewardItemList, rewardCountList, gameVersion,
            assetChangeRecorder,
            activityUnrivaledGod: activityUnrivaledGod,
            activityTreasureHunt: activityTreasureHunt,
            activityCsgoLottery : activityCsgoLottery);
        if (unpackItemListResult == null)
            return ([], null);

        assetChangeRecorder.FillAssetInfo(userAssets);
        return (unpackItemListResult.NewCardList, new TakeRewardResult
        {
            Reward = new GeneralReward() { ItemList = rewardItemList, CountList = rewardCountList },
            AssetsChange = assetChangeRecorder,
        });
    }

    private async Task AddCurrency(
        UserAssets userAssets,
        int itemId,
        int count,
        string gameVersion,
        UserAssetsChange? assetsChange = null,
        ActivityUnrivaledGod? activityUnrivaledGod = null,
        ActivityTreasureHunt? activityTreasureHunt = null,
        ActivityCsgoStyleLottery? activityCsgoLottery = null)
    {
        if (itemId == (int)MoneyType.Coin)
            userAssets.CoinCount += count;
        else if (itemId == (int)MoneyType.Diamond)
            userAssets.DiamondCount += count;
        else if (itemId == (int)MoneyType.Exp)
            await AddExpAsync(userAssets, count, gameVersion);
        else if (itemId == (int)MoneyType.UnrivaledKey || itemId == (int)MoneyType.UnrivaledScore)
        {
            if (activityUnrivaledGod == null)
            {
                // 查询一下无双神将的数据
                var unrivaledGodTimeConfig
                    = activityService.GetOpeningActivityByType(ActivityType.ActivityUnrivaledGod, gameVersion);
                if (unrivaledGodTimeConfig == null)
                    return;
                activityUnrivaledGod = await activityService.GetUnrivaledGodDataAsync(userAssets.PlayerId,
                    userAssets.ShardId,
                    unrivaledGodTimeConfig.id, TrackingOptions.Tracking);
                if (activityUnrivaledGod == null)
                    return;
            }
            if (itemId == (int)MoneyType.UnrivaledKey)
            {
                activityUnrivaledGod.KeyCount += count;
                assetsChange?.ItemChange.SpecialItemList.Add(new UserItem()
                {
                    ShardId = userAssets.ShardId,
                    PlayerId = userAssets.PlayerId,
                    ItemId = itemId,
                    ItemCount = activityUnrivaledGod.KeyCount
                });
            }
            else
            {
                activityUnrivaledGod.ScorePoint += count;
                assetsChange?.ItemChange.SpecialItemList.Add(new UserItem()
                {
                    ShardId = userAssets.ShardId,
                    PlayerId = userAssets.PlayerId,
                    ItemId = itemId,
                    ItemCount = activityUnrivaledGod.ScorePoint
                });
            }
        }
        else if (itemId == (int)MoneyType.SlotMachineScore)
            return;
        else if (itemId == (int)MoneyType.TreasureHuntKey || itemId == (int)MoneyType.TreasureHuntScore)
        {
            // 灵犀探宝钥匙/积分
            var treasureHuntTimeConfig
                = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, gameVersion);
            if (treasureHuntTimeConfig == null)
                return;
            if (activityTreasureHunt == null)
            {
                activityTreasureHunt = await activityService.GetTreasureHuntDataAsync(userAssets.PlayerId,
                    userAssets.ShardId, treasureHuntTimeConfig.id, TrackingOptions.Tracking);
                if (activityTreasureHunt == null)
                    return;
            }
            
            if (itemId == (int)MoneyType.TreasureHuntKey)
            {
                activityTreasureHunt.KeyCount += count;
                assetsChange?.ItemChange.SpecialItemList.Add(new UserItem()
                {
                    ShardId = userAssets.ShardId,
                    PlayerId = userAssets.PlayerId,
                    ItemId = itemId,
                    ItemCount = activityTreasureHunt.KeyCount
                });
            }
            else
            {
                activityTreasureHunt.ScorePoints += count;
                assetsChange?.ItemChange.SpecialItemList.Add(new UserItem()
                {
                    ShardId = userAssets.ShardId,
                    PlayerId = userAssets.PlayerId,
                    ItemId = itemId,
                    ItemCount = activityTreasureHunt.ScorePoints
                });
            }
        }
        else if (itemId == (int)MoneyType.CsgoStyleLottery || itemId == (int)MoneyType.CsgoLotteryPoint)
        {
            // CSGO风格抽奖钥匙
            var csgoLotteryTimeConfig
                = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, gameVersion);
            if (csgoLotteryTimeConfig == null)
                return;
            if (activityCsgoLottery == null)
            {
                activityCsgoLottery = await activityService.GetCsgoStyleLotteryDataAsync(userAssets.PlayerId,
                    userAssets.ShardId, csgoLotteryTimeConfig.id, TrackingOptions.Tracking);
                if (activityCsgoLottery == null)
                    return;
            }

            if (itemId == (int)MoneyType.CsgoStyleLottery)
                activityCsgoLottery.KeyCount += count;
            else
                activityCsgoLottery.ActivityPoint += count;
            
            assetsChange?.ItemChange.SpecialItemList.Add(new UserItem()
            {
                ShardId = userAssets.ShardId,
                PlayerId = userAssets.PlayerId,
                ItemId = itemId,
                ItemCount = count,
            });
        }
        else
            throw new Exception($"not a currency type : itemId {itemId}");
    }

    public async Task AddExpAsync(UserAssets userAssets, int exp, string gameVersion)
    {
        var levelData = userAssets.LevelData;
        int oldLevel = levelData.Level;
        levelData.LevelScore += exp;
        while (true)
        {
            var currentLevel = levelData.Level;
            // 已经到满级了
            if (serverConfigService.IsLevelMaxLevel(currentLevel, gameVersion))
                break;
            var currentConfig = serverConfigService.GetUserLevelConfig(currentLevel);
            if (currentConfig == null)
                break;
            var nextConfig = serverConfigService.GetUserLevelConfig(currentLevel + 1);
            if (nextConfig == null)
                break;
            if (gameVersion.CompareVersionStrServer(nextConfig.game_version) < 0)
                break;
            if (levelData.LevelScore < currentConfig.level_score)
                break;
            levelData.Level++;
            levelData.LevelScore -= currentConfig.level_score;
        }

        // 升级时立刻触发的事件
        if (oldLevel != levelData.Level)
        {
            var treasureMazeActivityConfig =
                activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureMaze, gameVersion);
            if (treasureMazeActivityConfig != null &&
                oldLevel < treasureMazeActivityConfig.unlock_user_level &&
                levelData.Level >= treasureMazeActivityConfig.unlock_user_level)
            {
                // 这里应该永远取不到 treasureMazeActivityData
                var treasureMazeActivityData = await activityService.GetTreasureMazeDataAsync(userAssets.PlayerId, userAssets.ShardId, treasureMazeActivityConfig.id);
                if (treasureMazeActivityData == null)
                    activityService.CreateTreasureMazeData(userAssets.PlayerId, userAssets.ShardId, treasureMazeActivityConfig.id, TimeUtils.GetCurrentTime());
            }
        }
    }

    public UserTreasureBox? CreateUserTreasureBoxData(
        int itemId,
        short shardId,
        long playerId,
        int itemCount,
        int starCount)
    {
        var config = serverConfigService.GetTreasureBoxConfigById(itemId);
        if (config == null)
            return null;
        return new UserTreasureBox()
        {
            ShardId = shardId,
            ItemId = itemId,
            PlayerId = playerId,
            ItemCount = itemCount,
            StarCount = starCount,
        };
    }

    public ItemConfig? GetItemConfig(UserCard card)
    {
        return serverConfigService.GetItemConfigById(card.CardId);
    }

    public BuildingCardConfig? GetBuildingCardConfig(UserCard card)
    {
        return serverConfigService.GetBuildingCardConfig(card);
    }

    public FightCardConfig? GetFightCardConfig(UserCard card)
    {
        return serverConfigService.GetFightCardConfig(card);
    }

    private int CalculateCardTotalExp(ItemConfig itemConfig)
    {
        switch (itemConfig.type)
        {
            case ItemType.BuildingCard:
            {
                var config = serverConfigService.GetBuildingCardConfigByKey(itemConfig.detailed_key);
                if (config == null)
                    throw new Exception($"no building card config {itemConfig.detailed_key}");
                return config.level_exp_list.Sum();
            }
            case ItemType.SoldierCard:
            case ItemType.TowerCard:
            {
                var configList = serverConfigService.GetFightCardConfigList(itemConfig.detailed_key);
                if (configList == null)
                    throw new Exception($"no fight card config {itemConfig.detailed_key}");
                return configList.Where(config => config.upgrade_need_exp > 0).Sum(config => config.upgrade_need_exp);
            }
            default: return 0;
        }
    }

    public int GetCardExpToNextLevel(UserCard card)
    {
        var itemConfig = GetItemConfig(card);
        if (itemConfig == null)
            throw new Exception($"no item config {card.CardId}");
        switch (itemConfig.type)
        {
            case ItemType.BuildingCard:
            {
                var config = GetBuildingCardConfig(card);
                if (config == null)
                    throw new Exception($"no building card config {card.CardId}");
                return Math.Max(0, config.level_exp_list[card.CardLevel] - card.CardExp);
            }
            case ItemType.SoldierCard:
            case ItemType.TowerCard:
            {
                var configList = serverConfigService.GetFightCardConfigList(itemConfig.detailed_key);
                if (configList == null)
                    throw new Exception($"no building card config {card.CardId}");
                return Math.Max(0, configList[card.CardLevel].upgrade_need_exp - card.CardExp);
            }
            default:
                throw new Exception($"no card item type {itemConfig.type}");
        }

        return 0;
    }

    /// <summary>
    /// 不考虑玩家持有的卡牌数量，仅计算当前卡牌等级升到满还需要多少卡牌
    /// </summary>
    private int GetCardNeedExp(UserCard card)
    {
        var itemConfig = GetItemConfig(card);
        if (itemConfig == null)
            throw new Exception($"no item config {card.CardId}");
        int needExp = 0;
        switch (itemConfig.type)
        {
            case ItemType.BuildingCard:
            {
                var config = GetBuildingCardConfig(card);
                if (config == null)
                    throw new Exception($"no building card config {card.CardId}");
                for (int i = card.CardLevel; i < config.level_exp_list.Count - 1; i++)
                    needExp += config.level_exp_list[i];
                break;
            }
            case ItemType.SoldierCard:
            case ItemType.TowerCard:
            {
                var configList = serverConfigService.GetFightCardConfigList(itemConfig.detailed_key);
                if (configList == null)
                    throw new Exception($"no building card config {card.CardId}");
                for (int i = card.CardLevel; i < configList.Count - 1; i++)
                    needExp += configList[i].upgrade_need_exp;
                break;
            }
            default:
                throw new Exception($"no card item type {itemConfig.type}");
        }

        return needExp;
    }

    /// <summary>
    /// 当前卡牌等级升到满还需要多少卡牌 减去 玩家持有的卡牌数量
    /// 相当于计算玩家最多还需要获得多少张本卡牌，返回0则意味着该玩家的本卡牌已满级
    /// </summary>
    public int GetCardLeftExp(UserCard card)
    {
        var needExp = GetCardNeedExp(card);
        return Math.Max(0, needExp - card.CardExp);
    }

    /// <summary>
    /// 计算多余的卡牌数量，如果账号没有触发bug，这里应该永远返回0
    /// </summary>
    public int GetCardExpSurplus(UserCard card)
    {
        var needExp = GetCardNeedExp(card);
        return Math.Max(0, card.CardExp - needExp);
    }
    

    public async Task<bool> AutoConvertSurplusCardsToMagicCard(UserAssets userAssets, string gameVersion)
    {
        bool needUpdateDatabase = false;
        Dictionary<ItemQuality, int> magicCardCompensation = new();
        foreach (var card in userAssets.UserCards)
        {
            var surplus = GetCardExpSurplus(card);
            if (surplus > 0)
            {
                card.CardExp -= surplus;
                var itemConfig = serverConfigService.GetItemConfigById(card.CardId);
                if (itemConfig == null)
                {
                    logger.LogError($"missing card item config. Card ID: {card.CardId}");
                    continue;
                }
                magicCardCompensation.TryAdd(itemConfig.quality, 0);
                magicCardCompensation[itemConfig.quality] += surplus;
                needUpdateDatabase = true;
            }
        }

        GeneralReward compensationReward = new();
        foreach (var magicCardToAdd in magicCardCompensation)
        {
            var itemConfig = serverConfigService.GetMagicCardConfigByQuality(magicCardToAdd.Key);
            if (itemConfig != null)
                compensationReward.AddReward(itemConfig.id, magicCardToAdd.Value);
        }

        if (compensationReward.ItemList.Count > 0)
        {
            await UnpackItemList(userAssets,
                compensationReward.ItemList, compensationReward.CountList,
                gameVersion);
            needUpdateDatabase = true;
        }

        return needUpdateDatabase;
    }

    private List<int> RandomQualityCountList(TreasureBoxConfig config)
    {
        var random = new Random();
        var qualityCardCountList = Enumerable.Repeat(0, config.weight_list.Count).ToList();
        var accumulateWeightList = Enumerable.Repeat(0, config.weight_list.Count).ToList();
        for (int i = 0; i < config.weight_list.Count; i++)
            for (int j = 0; j <= i; j++)
                accumulateWeightList[i] += config.weight_list[j];
        var weightSum = config.weight_list.Sum();
        for (int i = 0; i < config.card_count; ++i)
        {
            var randomValue = random.Next(0, weightSum);
            ++qualityCardCountList[accumulateWeightList.UpperBound(randomValue, (a, b) => a - b)];
        }

        return qualityCardCountList;
    }

    // 宝箱
    public class TreasureBoxReward
    {
        public readonly List<RewardItemData> CurrencyList = [];
        public readonly List<RewardItemData> CardList = [];
    }

    public async Task<(TreasureBoxReward reward, ErrorKind errorKind)> RandomTreasureBoxReward(
        UserGameInfo userGameInfo,
        UserAssets userAsset,
        short playerShard,
        long playerId,
        TreasureBoxConfig config,
        string gameVersion)
    {
        var reward = new TreasureBoxReward();
        var random = Random.Shared;
        List<int> rewardCardIdList = new();
        List<int> rewardCardCountList = new();

        var coinCount = random.Next(config.coin_min, config.coin_max + 1);
        if (coinCount > 0)
        {
            reward.CurrencyList.Add(new RewardItemData() { Id = (int)MoneyType.Coin, Count = coinCount });
        }

        var diamondCount = random.Next(config.diamond_min, config.diamond_max);
        if (diamondCount > 0)
        {
            reward.CurrencyList.Add(new RewardItemData() { Id = (int)MoneyType.Diamond, Count = diamondCount });
        }

        // 固定卡池
        if (config.box_type == TreasureBoxType.FixedCardAndCount)
        {
            if (config.fixed_card_pool.Count != config.fixed_card_count_list.Count)
                return (null!, ErrorKind.INVALID_CONFIG);
            for (int i = 0; i < config.fixed_card_pool.Count; i++)
            {
                int id = config.fixed_card_pool[i];
                int count = config.fixed_card_count_list[i];
                reward.CardList.Add(new RewardItemData() { Id = id, Count = count });
            }

            return (reward, ErrorKind.SUCCESS);
        }

        // 计算不同品质的卡牌数量
        var qualityCardCountList = RandomQualityCountList(config);
        // 计算保底
        var totalAddedGuaranteeCount = 0;
        for (int i = config.guarantee_count_list.Count - 1; i > 0; --i)
        {
            if (qualityCardCountList[i] >= config.guarantee_count_list[i])
                continue;
            var diff = config.guarantee_count_list[i] - qualityCardCountList[i];
            totalAddedGuaranteeCount += diff;
            qualityCardCountList[i] = config.guarantee_count_list[i];
        }

        qualityCardCountList[0] -= totalAddedGuaranteeCount;
        qualityCardCountList[0] = qualityCardCountList[0] < 0 ? 0 : qualityCardCountList[0];
        // 分配到不同的卡牌数量上
        var differentCardCountByQuality = new List<List<int>>();
        var differentCardRatio = config.different_card_count / (double)config.card_count;
        var left = config.different_card_count;
        for (int quality = qualityCardCountList.Count - 1; quality >= 0; --quality)
        {
            var totalCardCount = qualityCardCountList[quality];
            if (totalCardCount <= 0)
            {
                differentCardCountByQuality.Add([]);
                continue;
            }

            var differentCardCount = (int)Math.Ceiling(totalCardCount * differentCardRatio);
            differentCardCount = differentCardCount > left ? left : differentCardCount;
            if (differentCardCount <= 0 && totalCardCount > 0)
                differentCardCount = 1;
            left -= differentCardCount;
            var cardCountList = new List<int>();
            var standardDistance = totalCardCount / differentCardCount;
            var lastSplitterIndex = 0;
            var splitTotal = 0;
            for (int i = 1; i < differentCardCount; ++i)
            {
                var shiftMax = (int)MathF.Round(standardDistance * 0.3f);
                shiftMax = shiftMax >= standardDistance ? 0 : shiftMax;
                var splitterIndex = i * standardDistance + random.Next(-shiftMax, shiftMax);
                var addingCount = splitterIndex - lastSplitterIndex;
                cardCountList.Add(addingCount);
                splitTotal += addingCount;
                lastSplitterIndex = splitterIndex;
            }

            cardCountList.Add(totalCardCount - splitTotal);
            differentCardCountByQuality.Add(cardCountList);
        }

        differentCardCountByQuality.Reverse();
        var drewNewCardCountList = userGameInfo.DrawNewCardCountList;
        var drewCardCountList = userGameInfo.DrawCardCountList;
        var cardPoolDic = new Dictionary<int, List<RewardItemData>>();
        var qualityDiff = differentCardCountByQuality.Count - drewCardCountList.Count;
        for (int i = 0; i < qualityDiff; i++)
            drewCardCountList.Add(0);
        for (int i = 0; i < qualityDiff; i++)
            drewNewCardCountList.Add(0);

        // 确认卡牌
        var drewCardSet = new HashSet<int>();
        for (int quality = 0; quality < differentCardCountByQuality.Count; ++quality)
        {
            var cardCountList = differentCardCountByQuality[quality];
            foreach (var cardCount in cardCountList)
            {
                if (cardCount <= 0)
                    continue;
                // 判断要不要给新卡
                var newCardAccumulate =
                    serverConfigService.GetNewCardAccumulate(quality, drewNewCardCountList[quality]);
                var currentCardAccumulate = drewCardCountList[quality];
                var newCard = currentCardAccumulate >= newCardAccumulate;
                newCard = CheckForceNewCard(userAsset, config, newCard, quality);

                if (newCard)
                {
                    drewNewCardCountList[quality] += 1;
                    drewCardCountList[quality] = Math.Max(0, drewCardCountList[quality] - newCardAccumulate);
                }
                else
                {
                    drewCardCountList[quality] += cardCount;
                }

                var cardPoolKey = quality * 10 + (newCard ? 1 : 0);
                // 挑选卡池
                if (!cardPoolDic.TryGetValue(cardPoolKey, out var pool))
                {
                    pool = await GetTreasureBoxCardPool(config, newCard, userAsset, quality, gameVersion);
                    cardPoolDic.Add(cardPoolKey, pool);
                }

                var poolNotDrew = pool.Where(cardInPool => !drewCardSet.Contains(cardInPool.Id)).ToList();
                if (poolNotDrew.IsNullOrEmpty())
                {
                    // 卡池空了，直接给外卡
                    var magicCardConfig = serverConfigService.GetMagicCardConfigByQuality((ItemQuality)quality);
                    if (magicCardConfig != null)
                    {
                        rewardCardIdList.Add(magicCardConfig.id);
                        rewardCardCountList.Add(cardCount);
                        continue;
                    }
                }

                // 随机一张卡
                var cardIndexInPool = random.Next(0, poolNotDrew.Count);
                var cardSelected = poolNotDrew[cardIndexInPool];
                cardIndexInPool = pool.FindIndex(data => data.Id == cardSelected.Id);
                if (cardSelected.Count > cardCount)
                {
                    cardSelected.Count -= cardCount;
                    rewardCardIdList.Add(cardSelected.Id);
                    rewardCardCountList.Add(cardCount);
                }
                else
                {
                    // 多出来的*自动*替换成对应品质的外卡
                    pool.RemoveAt(cardIndexInPool);
                    rewardCardIdList.Add(cardSelected.Id);
                    rewardCardCountList.Add(cardCount);
                }

                drewCardSet.Add(cardSelected.Id);
            }
        }

        for (int i = 0; i < rewardCardIdList.Count; i++)
            reward.CardList.Add(new RewardItemData() { Id = rewardCardIdList[i], Count = rewardCardCountList[i] });
        reward.CardList.Shuffle();

        // 装饰品
        int decorationIdObtained = serverConfigService.GetDecorationItemFromPool(config.deco_item_pool, random);
        if (decorationIdObtained != -1)
        {
            if (userAsset.UserItems.Find((UserItem item) => { return item.ItemId == decorationIdObtained; }) == null)
                reward.CardList.Add(new RewardItemData() { Id = decorationIdObtained, Count = 1 });
        }

        return (reward, ErrorKind.SUCCESS);
    }

    private bool CheckForceNewCard(UserAssets userAsset, TreasureBoxConfig config, bool newCard, int quality)
    {
        var userCards = userAsset.UserCards;
        if (newCard || (config.box_type != TreasureBoxType.FightCard &&
                        config.box_type != TreasureBoxType.BuildingCard))
        {
            return newCard;
        }

        bool hasTargetCard = false;
        foreach (var card in userCards)
        {
            var itemConfig = serverConfigService.GetItemConfigById(card.CardId);
            if (itemConfig == null)
                continue;

            if (itemConfig.quality != (ItemQuality)quality)
                continue;
            if (config.box_type == TreasureBoxType.FightCard &&
                itemConfig is { type: ItemType.TowerCard or ItemType.SoldierCard })
            {
                hasTargetCard = true;
                break;
            }

            if (config.box_type == TreasureBoxType.BuildingCard && itemConfig.type == ItemType.BuildingCard)
            {
                hasTargetCard = true;
                break;
            }
        }

        newCard = newCard || !hasTargetCard;
        return newCard;
    }

    private async Task<List<RewardItemData>> GetTreasureBoxCardPool(
        TreasureBoxConfig config,
        bool newCard,
        UserAssets userAsset,
        int quality,
        string gameVersion)
    {
        var selectionPool = new List<ItemConfig>();
        switch (config.box_type)
        {
            case TreasureBoxType.FightCard:
                selectionPool.AddRange(serverConfigService.GetFightCardItemConfigList());
                break;
            case TreasureBoxType.BuildingCard:
                selectionPool.AddRange(serverConfigService.GetBuildingCardItemConfigList());
                break;
            case TreasureBoxType.AllCard:
                selectionPool.AddRange(serverConfigService.GetFightCardItemConfigList());
                selectionPool.AddRange(serverConfigService.GetBuildingCardItemConfigList());
                break;
            case TreasureBoxType.FixedCardPool:
                foreach (var itemId in config.fixed_card_pool)
                {
                    var itemConfig = serverConfigService.GetItemConfigById(itemId);
                    if (itemConfig == null)
                        continue;
                    selectionPool.Add(itemConfig);
                }
                break;
        }
        var realSelectionPool = new List<ItemConfig>();
        foreach (var itemConfig in selectionPool)
        {
            // 版本不符合要求的卡牌不进卡池
            // 正常情况下这个不会是null，但是先更服务器再更配置的话会是null，这里做一下处理。
            if (itemConfig.min_version != null &&
                gameVersion.CompareVersionStrServer(itemConfig.min_version) < 0)
                continue;
            realSelectionPool.Add(itemConfig);
        }

        var userCards = userAsset.UserCards;
        var userCardDic = userCards.ToDictionary(card => card.CardId);
        var result = new List<RewardItemData>();

        // 固定卡池的过滤
        if (config.box_type == TreasureBoxType.FixedCardPool)
        {
            // 固定卡池不做新卡旧卡的检查
            foreach (var cardConfig in realSelectionPool)
            {
                if (cardConfig.quality != (ItemQuality)quality)
                    continue;
                if (userCardDic.TryGetValue(cardConfig.id, out var card))
                {
                    var leftExp = GetCardLeftExp(card);
                    if (leftExp <= 0)
                        continue;
                    result.Add(new RewardItemData { Id = cardConfig.id, Count = leftExp });
                    continue;
                }

                result.Add(new RewardItemData { Id = cardConfig.id, Count = CalculateCardTotalExp(cardConfig) });
            }

            return result;
        }

        // 其他卡池的过滤
        foreach (var cardConfig in realSelectionPool)
        {
            if (cardConfig.quality != (ItemQuality)quality)
                continue;
            var userCardContains = userCardDic.TryGetValue(cardConfig.id, out var card);
            if (newCard)
            {
                if (serverConfigService.GetBasicCardsThatCannotBeObtainedByTreasureBox().Contains(cardConfig.id))
                    continue;
                if (!userCardContains)
                    result.Add(new RewardItemData { Id = cardConfig.id, Count = CalculateCardTotalExp(cardConfig) });
                continue;
            }

            if (!userCardContains || card == null)
                continue;
            var leftExp = GetCardLeftExp(card);
            if (leftExp <= 0)
                continue;
            result.Add(new RewardItemData() { Id = cardConfig.id, Count = leftExp });
        }

        return result;
    }

    // 返回的值为true则需要发放满福袋奖励
    private async Task<bool> OnFortuneBagAdded(long playerId, short shardId, int count, ActivityFortuneBagConfig config)
    {
        var activityId = config.activityId;
        var fortuneBagInfo = await activityService.GetUserFortuneBagInfoAsync(playerId, shardId, TrackingOptions.Tracking);
        if (fortuneBagInfo == null) // 这个情况不应该出现，仅为了防止福袋被吞
            fortuneBagInfo = activityService.AddUserFortuneBagInfo(playerId, shardId, activityId);
        fortuneBagInfo.FortuneBags.Add(new FortuneBagAcquireInfo()
        {
            AcquireTime = TimeUtils.GetCurrentTime(),
            BagCount = count,
            ClaimStatus = 0,
        });
        int totalBagCount = 0;
        foreach (var bag in fortuneBagInfo.FortuneBags)
            totalBagCount += bag.BagCount;
        dbCtx.Entry(fortuneBagInfo).Property(t => t.FortuneBags).IsModified = true;
        await activityService.AddFortuneBagLevelAsync(count, activityId);
        return totalBagCount >= config.fortune_bag_max_stack;
    }

    private async Task<bool> OnSlotMachineRewardDoubleUp(
        BuildingGameDB context,
        long playerId,
        short shardId,
        int activityId,
        int itemId)
    {
        var slotMachineData = await activityService.GetSlotMachineDataAsync(playerId, shardId, activityId, TrackingOptions.Tracking);
        if (slotMachineData == null)
            return false;
        var level = serverConfigService.GetSlotMachineDoubleUpItemLevel(itemId);
        if (level < 0)
            return false;
        if (slotMachineData.RewardDoubledUpItemCount.Count <= level)
            slotMachineData.RewardDoubledUpItemCount.AddRange(Enumerable.Repeat(0,
                level + 1 - slotMachineData.RewardDoubledUpItemCount.Count));
        slotMachineData.RewardDoubledUpItemCount[level] += 1;
        return true;
    }
}