using ChillyRoom.BuildingGame.Models;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Util;

namespace GameOutside.Services;

public class InvitationCodeService(
    IInvitationCodeRepository invitationCodeRepository,
    UserItemService userItemService,
    UserAssetService userAssetService,
    ServerConfigService serverConfigService
    )
{
    [Obsolete]
    public async Task<(List<UserCard>, TakeRewardResult?, ErrorKind)> ClaimInvitationCode(string invitationCode, long playerId, short playerShard, string gameVersion)
    {
        var claimed = await invitationCodeRepository.IsInvitationCodeClaimed(invitationCode);
        if (claimed)
            return ([], null, ErrorKind.REWARD_CLAIMED);

        var rewardItemList = serverConfigService.GetParameterIntList(Params.H5FriendActivityInvitationCodeItemList);
        if (rewardItemList == null)
            return ([], null, ErrorKind.NO_PARAM_CONFIG);
        var rewardCountList = serverConfigService.GetParameterIntList(Params.H5FriendActivityInvitationCodeItemCount);
        if (rewardCountList == null)
            return ([], null, ErrorKind.NO_PARAM_CONFIG);
        var generalReward = new GeneralReward();
        generalReward.ItemList = rewardItemList;
        generalReward.CountList = rewardCountList;
        // 发放奖励
        var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
        var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(playerShard, playerId, includeOption);
        if (userAsset == null)
            return ([], null, ErrorKind.NO_USER_ASSET);

        var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, gameVersion);
        if (result == null)
            return ([], null, ErrorKind.NO_ITEM_CONFIG);
        // 标记领取
        invitationCodeRepository.MarkInvitationCodeClaimed(invitationCode);
        return (newCardList, result, ErrorKind.SUCCESS);
    }
}