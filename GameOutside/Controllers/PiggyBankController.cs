using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;
using GameOutside;
using GameOutside.DBContext;
using GameOutside.Services;

public class PiggyBankController(IConfiguration configuration,
    ILogger<PiggyBankController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    ActivityService activityService,
    UserAssetService userAssetService,
    BuildingGameDB context,
    UserAchievementService userAchievementService) : BaseApiController(configuration)
{
    public record struct ClaimPiggyBankNodeReply(TakeRewardResult Reward, PiggyBankDataClient PiggyBankDataClient);

    [HttpPost]
    public async Task<ActionResult<ClaimPiggyBankNodeReply>> ClaimPiggyBankNode(int level, int paidLevel)
    {
        var (reply, errorKind) = await context.WithRCUDefaultRetry<(
            ClaimPiggyBankNodeReply?, ErrorKind
        )>(async _ =>
        {
            // 检查是否有状态
            var piggyBankStatus = await activityService.GetPiggyBankStatusAsync(PlayerId, PlayerShard);
            if (piggyBankStatus == null)
                return (null, ErrorKind.NO_USER_RECORDS);

            // 检查付费等级够不够
            if (piggyBankStatus.PaidLevel < paidLevel)
                return (null, ErrorKind.PIGGY_BANK_PAID_LEVEL_NOT_ENOUGH);

            // 检查经验够不够
            var piggyBankConfigList = serverConfigService.GetPiggyBankConfigList();
            if (level < 0 || level >= piggyBankConfigList.Count)
                return (null, ErrorKind.WRONG_PARAM);
            if (piggyBankStatus.Exp < level + 1)
                return (null, ErrorKind.PIGGY_BANK_GAME_COUNT_NOT_ENOUGH);

            // 检查是不是领取过了
            var claimState = piggyBankStatus.ClaimStatus[paidLevel];
            var claimed = claimState.GetNthBits(level);
            if (claimed)
                return (null, ErrorKind.REWARD_CLAIMED);
            var piggyBankConfig = piggyBankConfigList[level];

            // 发放奖励
            var reward = new GeneralReward()
            {
                ItemList = [piggyBankConfig.item_list[paidLevel]],
                CountList = [piggyBankConfig.count_list[paidLevel]]
            };
            if (reward.ItemList[0] < 0)
                reward.ItemList = new List<int>();
            if (reward.CountList[0] <= 0)
                reward.CountList = new List<int>();

            // 貔貅翁只会发放玉璧
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return (null, ErrorKind.NO_USER_ASSET);

            // 发放物品
            var (newCardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
            if (result == null)
                return (null, ErrorKind.NO_ITEM_CONFIG);

            // 修改领取状态
            piggyBankStatus.ClaimStatus[paidLevel] = claimState.SetNthBits(level, true);

            // 保存修改
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return (new ClaimPiggyBankNodeReply(result, piggyBankStatus.ToClientApi()), ErrorKind.SUCCESS);
        });

        if (errorKind == ErrorKind.SUCCESS)
        {
            return Ok(reply);
        }
        else
        {
            return BadRequest(errorKind.Response());
        }
    }

    private (GeneralReward, ErrorKind) ClaimAllPiggyBankRewardsByActivityId(
        ActivityPiggyBank piggyBankStatus, Dictionary<int, int> rewardDic)
    {
        // 获取配置
        var piggyBankConfigList = serverConfigService.GetPiggyBankConfigList();
        if (piggyBankConfigList == null)
            return (new GeneralReward { ItemList = [], CountList = [] }, ErrorKind.NO_PIGGY_BANK_CONFIG);
        // 从前到后过一遍，把能领的都领了. 如果经验规则改了，这个算法行不通，不能按经验遍历level了。预计不会有改变
        for (int level = 0; level < piggyBankStatus.Exp && level < piggyBankConfigList.Count; ++level)
        {
            for (int paidLevel = 0; paidLevel <= piggyBankStatus.PaidLevel; ++paidLevel)
            {
                var claimState = piggyBankStatus.ClaimStatus[paidLevel];
                var claimed = claimState.GetNthBits(level);
                if (claimed)
                    continue;
                // 合并奖励
                var piggyBankConfig = piggyBankConfigList[level];
                var itemId = piggyBankConfig.item_list[paidLevel];
                var itemCount = piggyBankConfig.count_list[paidLevel];
                if (!rewardDic.TryAdd(itemId, itemCount))
                    rewardDic[itemId] += itemCount;
                // 修改领取状态
                piggyBankStatus.ClaimStatus[paidLevel] = claimState.SetNthBits(level, true);
            }
        }

        if (rewardDic.Count <= 0)
            return (new GeneralReward { ItemList = [], CountList = [] }, ErrorKind.NO_CLAIMABLE_REWARD);

        var reward = new GeneralReward() { ItemList = rewardDic.Keys.ToList(), CountList = rewardDic.Values.ToList() };
        if (reward.ItemList[0] < 0)
            reward.ItemList = new List<int>();
        if (reward.CountList[0] <= 0)
            reward.CountList = new List<int>();
        return (reward, ErrorKind.SUCCESS);
    }

    /*
    public async Task<(bool, TakeRewardResult?)> ClaimExpiredPiggyBankReward()
    {
        var activityConfigList = serverConfig.GetActivityConfigList();
        var rewardDic = new Dictionary<int, int>();
        GeneralReward? totalReward = null;
        List<int> allFinishedPiggyBankActivityIds = new List<int>();
        bool dbChanged = false;
        foreach (var activityConfig in activityConfigList)
        {
            if (activityConfig.activity_type != ActivityType.ActivityPiggyBank)
                continue;
            if (!GameOutside.ActivityModule.IsAlreadyClosed(activityConfig))
                continue;
            allFinishedPiggyBankActivityIds.Add(activityConfig.id);
        }
        var piggyBankStatusList = await context.GetAllPiggyBankStatusWithIds(PlayerId, PlayerShard, allFinishedPiggyBankActivityIds.ToArray());
        foreach (var status in piggyBankStatusList)
        {
            var (reward, error) = ClaimAllPiggyBankRewardsByActivityId(status.ActivityId, status, rewardDic);
            if (error != ErrorKind.SUCCESS)
                continue;
            totalReward = reward;
        }
        
        TakeRewardResult? result = null;
        var userAsset = await context.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
        if (userAsset == null)
            return (dbChanged, result);
        if (totalReward != null)
        {
            result = await userItemService.TakeReward(userAsset, totalReward);
            dbChanged = true;
        }
        return (dbChanged, result); 
    }
    */

    [HttpPost]
    public async Task<ActionResult<ClaimPiggyBankNodeReply>> ClaimAllPiggyBankNodeOneTime()
    {
        var (reply, errorKind) = await context.WithRCUDefaultRetry<(ClaimPiggyBankNodeReply?, ErrorKind)>(async _ =>
        {
            // 检查是否有状态
            var piggyBankStatus = await activityService.GetPiggyBankStatusAsync(PlayerId, PlayerShard);
            if (piggyBankStatus == null)
                return (null, ErrorKind.NO_USER_RECORDS);

            // 计算奖励
            var (reward, error) = ClaimAllPiggyBankRewardsByActivityId(piggyBankStatus, new Dictionary<int, int>());
            if (error != ErrorKind.SUCCESS)
                return (null, error);

            // 发放奖励
            // 貔貅翁只会发放玉璧
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return (null, ErrorKind.NO_USER_ASSET);

            // 发放物品
            var (newCardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
            if (result == null)
                return (null, ErrorKind.NO_ITEM_CONFIG);

            // 保存修改
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return (new ClaimPiggyBankNodeReply(result, piggyBankStatus.ToClientApi()), ErrorKind.SUCCESS);
        });

        if (errorKind == ErrorKind.SUCCESS)
        {
            return Ok(reply);
        }
        else
        {
            return BadRequest(errorKind.Response());
        }
    }
}