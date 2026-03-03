using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class UserBattlePassController(
    IConfiguration configuration,
    ILogger<UserBattlePassController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    UserAssetService userAssetService,
    BattlePassService battlePassService,
    BuildingGameDB context,
    UserAchievementService userAchievementService)
    : BaseApiController(configuration)
{


    public record struct FetchBattlePassInfoResult(UserBattlePassInfo FirstPass, UserBattlePassInfo BattlePass);

    [HttpPost]
    public async Task<ActionResult<FetchBattlePassInfoResult>> FetchBattlePassInfo()
    {
        return await context.WithRCUDefaultRetry<ActionResult<FetchBattlePassInfoResult>>(async _ =>
        {
            if (!serverConfigService.TryGetParameterInt(Params.MaxSuperPassLevel, out var superPassLevelCount))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });

            int activeBattlePassId = serverConfigService.GetActiveBattlePassId();
            if (activeBattlePassId == -1)
                return BadRequest(ErrorKind.NO_BATTLE_PASS_CONFIG.Response());

            var currentBattlePassInfo
                = await battlePassService.GetUserBattlePassInfoByPassIdAsync(PlayerShard, PlayerId, activeBattlePassId);
            currentBattlePassInfo ??= battlePassService.AddUserBattlePassInfo(PlayerShard, PlayerId, activeBattlePassId);

            var firstBattlePassInfo
                = await battlePassService.GetUserBattlePassInfoByPassIdAsync(PlayerShard, PlayerId, -1);
            firstBattlePassInfo ??= battlePassService.AddUserBattlePassInfo(PlayerShard, PlayerId, -1);

            // 之前有个bug,有可能ClaimStatus初始化为[]，这里再检测一下
            if (firstBattlePassInfo.ClaimStatus.Count == 0)
            {
                for (int i = 0; i < superPassLevelCount; i++)
                    firstBattlePassInfo.ClaimStatus.Add(0);
            }

            if (currentBattlePassInfo.ClaimStatus.Count == 0)
            {
                for (int i = 0; i < superPassLevelCount; i++)
                    currentBattlePassInfo.ClaimStatus.Add(0);
            }
            
            await context.SaveChangesWithDefaultRetryAsync();

            return Ok(new FetchBattlePassInfoResult()
            {
                BattlePass = currentBattlePassInfo,
                FirstPass = firstBattlePassInfo
            });
        });
    }

    public record struct ClaimBattlePassRewardResult(
        TakeRewardResult RewardResult,
        UserBattlePassInfo NewBattlePassInfo);

    [HttpPost]
    public async Task<ActionResult<ClaimBattlePassRewardResult>> ClaimBattlePassRewards(bool firstPass, int passLevel,
        int superPassLevel)
    {
        // 确认battle pass id
        var requestBattlePassId = -1;
        if (!firstPass)
        {
            requestBattlePassId = serverConfigService.GetActiveBattlePassId();
            if (requestBattlePassId == -1)
                return BadRequest(ErrorKind.NO_BATTLE_PASS_CONFIG.Response());
        }

        return await context.WithRCUDefaultRetry<ActionResult<ClaimBattlePassRewardResult>>(async _ =>
        {
            // 检查能不能领
            var battlePassInfo
                = await battlePassService.GetUserBattlePassInfoByPassIdAsync(PlayerShard, PlayerId,
                    requestBattlePassId);
            if (battlePassInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
            if (superPassLevel > battlePassInfo.SuperPassLevel)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.SUPER_PASS_LEVEL_NOT_ENOUGH });
            var battlePassConfig = serverConfigService.GetBattlePassConfigByLevel(requestBattlePassId, passLevel);
            if (battlePassConfig == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_BATTLE_PASS_CONFIG });
            if (battlePassInfo.Exp < battlePassConfig.exp)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.BATTLE_PASS_EXP_NOT_ENOUGH });
            var claimState = battlePassInfo.ClaimStatus[superPassLevel];
            if (claimState.GetNthBits(passLevel))
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.REWARD_CLAIMED });

            // 发放奖励
            var reward = new GeneralReward()
            {
                ItemList = [battlePassConfig.item_list[superPassLevel]],
                CountList = [battlePassConfig.count_list[superPassLevel]]
            };
            if (reward.ItemList[0] < 0)
                reward.ItemList = new List<int>();
            if (reward.CountList[0] <= 0)
                reward.CountList = new List<int>();

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(reward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var (newCardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
            if (result == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            // 确认领取状态
            battlePassInfo.ClaimStatus[superPassLevel] = claimState.SetNthBits(passLevel, true);

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange is not null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new ClaimBattlePassRewardResult() { NewBattlePassInfo = battlePassInfo, RewardResult = result });
        });
    }

    [HttpPost]
    public async Task<ActionResult<ClaimBattlePassRewardResult>> ClaimAllBattlePassReward(bool firstPass)
    {
        // 确认battle pass id
        var requestBattlePassId = -1;
        if (!firstPass)
        {
            requestBattlePassId = serverConfigService.GetActiveBattlePassId();
            if (requestBattlePassId == -1)
                return BadRequest(ErrorKind.NO_BATTLE_PASS_CONFIG.Response());
        }

        return await context.WithRCUDefaultRetry<ActionResult<ClaimBattlePassRewardResult>>(async _ =>
        {
            // 计算所有能领取的奖励
            var battlePassInfo
                = await battlePassService.GetUserBattlePassInfoByPassIdAsync(PlayerShard, PlayerId,
                    requestBattlePassId);
            if (battlePassInfo == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            var battlePassConfigList = serverConfigService.GetBattlePassConfigListByPassId(requestBattlePassId);
            if (battlePassConfigList == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_BATTLE_PASS_CONFIG });
            var itemDic = new Dictionary<int, int>();
            foreach (var battlePassConfig in battlePassConfigList)
            {
                if (battlePassInfo.Exp < battlePassConfig.exp)
                    break;
                for (int i = 0; i <= battlePassInfo.SuperPassLevel; i++)
                {
                    var claimed = battlePassInfo.ClaimStatus[i].GetNthBits(battlePassConfig.level);
                    if (claimed)
                        continue;
                    itemDic.TryAdd(battlePassConfig.item_list[i], 0);
                    itemDic[battlePassConfig.item_list[i]] += battlePassConfig.count_list[i];
                    // 修改领取状态
                    battlePassInfo.ClaimStatus[i]
                        = battlePassInfo.ClaimStatus[i].SetNthBits(battlePassConfig.level, true);
                }
            }

            if (itemDic.Count <= 0)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.REWARD_CLAIMED });

            // 发放奖励
            var reward = new GeneralReward() { ItemList = [], CountList = [] };
            foreach (var pair in itemDic)
            {
                reward.ItemList.Add(pair.Key);
                reward.CountList.Add(pair.Value);
            }

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(reward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });
            var (newCardList, result) = await userItemService.TakeReward(userAsset, reward, GameVersion);
            if (result == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            // 保存
            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (result.AssetsChange is not null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new ClaimBattlePassRewardResult() { NewBattlePassInfo = battlePassInfo, RewardResult = result });
        });
    }
}