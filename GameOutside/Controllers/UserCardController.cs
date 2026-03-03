using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class UserCardController(
    IConfiguration configuration,
    ILogger<UserCardController> logger,
    UserItemService userItemService,
    ServerConfigService serverConfigService,
    BuildingGameDB context,
    UserAssetService userAssetService,
    UserAchievementService userAchievementService)
    : BaseApiController(configuration)
{

    public record struct UpgradeCardReply(CardChange CardChange, UserAchievement Achievement, int NewCoinCount);

    public record struct UseMagicCardReply(UserAssetsChange AssetChance);

    [HttpPost]
    public async Task<ActionResult<UpgradeCardReply>> UpgradeCard(int cardId, bool oneKey)
    {
        try
        {
            var (reply, errorKind) = await context.WithRCUDefaultRetry<(UpgradeCardReply?, ErrorKind)>(async _ =>
            {
                // 这里只获取物品信息
                // 卡牌数据走另一个接口
                var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
                if (userAsset == null)
                {
                    return (null, ErrorKind.NO_USER_RECORDS);
                }
                var cardData = await userAssetService.GetUserCardAsync(PlayerShard, PlayerId, cardId);
                if (cardData == null)
                {
                    return (null, ErrorKind.NO_USER_ASSET);
                }

                var itemConfig = userItemService.GetItemConfig(cardData);
                if (itemConfig == null)
                {
                    return (null, ErrorKind.NO_ITEM_CONFIG);
                }

                bool upgradeSuccess = false;
                switch (itemConfig.type)
                {
                    case ItemType.BuildingCard:
                    {
                        var config = userItemService.GetBuildingCardConfig(cardData);
                        if (config == null)
                        {
                            return (null, ErrorKind.NO_BUILDING_CARD_CONFIG);
                        }
                        while (true)
                        {
                            if (cardData.CardLevel >= config.level_exp_list.Count - 1)
                                break;
                            int needExp = config.level_exp_list[cardData.CardLevel];
                            if (cardData.CardExp < needExp)
                                break;
                            var needCoin = config.level_coin_list[cardData.CardLevel];
                            if (userAsset.CoinCount < needCoin)
                                break;
                            cardData.CardExp -= needExp;
                            userAsset.CoinCount -= needCoin;
                            cardData.CardLevel++;
                            upgradeSuccess = true;
                            if (!oneKey)
                                break;
                        }

                        break;
                    }
                    case ItemType.SoldierCard:
                    case ItemType.TowerCard:
                    {
                        while (true)
                        {
                            var config = userItemService.GetFightCardConfig(cardData);
                            if (config == null)
                            {
                                return (null, ErrorKind.NO_SOLDIER_CARD_CONFIG);
                            }
                            if (config.IsMaxLevel)
                                break;
                            if (cardData.CardExp < config.upgrade_need_exp)
                                break;
                            if (userAsset.CoinCount < config.upgrade_need_coin)
                                break;
                            cardData.CardExp -= config.upgrade_need_exp;
                            userAsset.CoinCount -= config.upgrade_need_coin;
                            cardData.CardLevel++;
                            upgradeSuccess = true;
                            if (!oneKey)
                                break;
                        }

                        break;
                    }
                    default:
                        return (null, ErrorKind.NO_ITEM_TYPE_FOUND);
                }

                if (!upgradeSuccess)
                {
                    return (null, ErrorKind.CARD_UPGRADE_FAILED);
                }

                // 记录每日任务进度
                await context.AddDailyTaskProgress(serverConfigService, DailyTaskType.UPGRADE_CARD, 1,
                    PlayerShard, PlayerId, userAsset.TimeZoneOffset);

                // 卡牌升级成就
                var (key, target) = userAchievementService.GetAchievementKeyAndTarget(cardData);
                if (key == null || target == null)
                {
                    return (null, ErrorKind.WRONG_ITEM_CONFIG);
                }
                var record = new AchievementRecord() { Key = key, Target = target, Count = cardData.CardLevel };

                await using var transaction = await context.Database.BeginTransactionAsync();

                await context.SaveChangesWithDefaultRetryAsync(false);
                var achievements = await userAchievementService.UpdateAchievementProgressAsync(
                    [record], PlayerShard, PlayerId);

                await transaction.CommitAsync();
                context.ChangeTracker.AcceptAllChanges();

                // 通知成功
                var cardChange = new CardChange();
                cardChange.ModifyList.Add(cardData);
                return (new UpgradeCardReply(cardChange, achievements[0], userAsset.CoinCount), ErrorKind.SUCCESS);
            });

            if (errorKind != ErrorKind.SUCCESS)
            {
                return BadRequest(errorKind.Response());
            }
            else
            {
                return Ok(reply);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "升级卡牌失败");
            return BadRequest(ErrorKind.COMMON_INTERNAL_SAVE_ERROR.Response());
        }
    }

    [HttpPost]
    public async Task<ActionResult<UseMagicCardReply>> UseMagicCard(int cardId)
    {
        return await context.WithRCUDefaultRetry<ActionResult<UseMagicCardReply>>(async _ =>
        {
            // 这里只获取物品信息
            // 卡牌数据走另一个接口
            var userAsset = await userAssetService.GetUserAssetsWithItemsAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.NO_USER_RECORDS});
            var cardData = await userAssetService.GetUserCardAsync(PlayerShard, PlayerId, cardId);
            if (cardData == null)
                return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.NO_USER_ASSET});
            var cardConfig = serverConfigService.GetItemConfigById(cardId);
            if (cardConfig == null)
                return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG});
            var items = userAsset.UserItems;
            var magicCardData = items?.FirstOrDefault(c =>
            {
                var itemConfig = serverConfigService.GetItemConfigById(c.ItemId);
                return itemConfig != null && itemConfig.type == ItemType.MagicCard &&
                       itemConfig.quality == cardConfig.quality;
            });
            if (magicCardData == null)
                return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.NO_USER_ASSET});
            var expToNextLevel = userItemService.GetCardExpToNextLevel(cardData);
            if (expToNextLevel <= 0)
                return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.CARD_EXP_FULL});
            var magicCardUseCount = expToNextLevel > magicCardData.ItemCount ? magicCardData.ItemCount : expToNextLevel;
            // 前提条件已经满足，接下来执行操作
            var assetChange = new UserAssetsChange();
            assetChange.CardChange.ModifyList.Add(cardData);
            cardData.CardExp += magicCardUseCount;
            magicCardData.ItemCount -= magicCardUseCount;
            if (magicCardData.ItemCount <= 0)
            {
                userAsset.UserItems.Remove(magicCardData);
                assetChange.ItemChange.RemoveList.Add(magicCardData.ItemId);
            }
            else
                assetChange.ItemChange.ModifyList.Add(magicCardData);
            
            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new UseMagicCardReply() {AssetChance = assetChange,});
        });
    }
}