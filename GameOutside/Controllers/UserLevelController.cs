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
public class UserLevelController : BaseApiController
{
    private readonly BuildingGameDB _context;
    private readonly ILogger<UserLevelController> _logger;
    private readonly ServerConfigService _serverConfigService;
    private readonly UserItemService _userItemService;
    private readonly UserAssetService _userAssetService;
    private readonly UserAchievementService _userAchievementService;

    public UserLevelController(
        IConfiguration configuration,
        ILogger<UserLevelController> logger,
        ServerConfigService serverConfigService,
        UserItemService userItemService,
        BuildingGameDB context,
        UserAssetService userAssetService,
        UserAchievementService userAchievementService) : base(configuration)
    {
        _context = context;
        _logger = logger;
        _serverConfigService = serverConfigService;
        _userItemService = userItemService;
        _userAssetService = userAssetService;
        _userAchievementService = userAchievementService;
    }

    [HttpPost]
    public async Task<ActionResult<UserLevelData>> GetUserLevelInfo()
    {
        var levelData = await _userAssetService.GetLevelDataAsync(PlayerShard, PlayerId);
        if (levelData is null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        return Ok(levelData);
    }

    public record struct ClaimLevelRewardReply(TakeRewardResult RewardResult, UserLevelData LevelInfo);

    [HttpPost]
    public async Task<ActionResult<ClaimLevelRewardReply>> ClaimLevelReward(int level)
    {
        var levelConfig = _serverConfigService.GetUserLevelConfig(level);
        if (levelConfig == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.WRONG_USER_LEVEL });
        var generalReward = new GeneralReward()
        {
            ItemList = levelConfig.item_list.ToList(),
            CountList = levelConfig.count_list.ToList()
        };

        return await _context.WithRCUDefaultRetry<ActionResult<ClaimLevelRewardReply>>(async _ =>
        {
            var includeOption = _userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await _userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });

            var levelData = userAsset.LevelData;

            if (levelData.Level < level)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.USER_LEVEL_NOT_ENOUGH });

            var rewardStatus = levelData.RewardStatusList;
            bool received = rewardStatus.GetNthBits(level);
            if (received)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.LEVEL_REWARD_ALREADY_CLAIMED });

            // 设置被领取标记
            userAsset.LevelData.RewardStatusList.SetNthBits(level, true);

            var (newCardList, takeRewardResult) = await _userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_ITEM_CONFIG });

            await using var t = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await _userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult.AssetsChange is not null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            _context.ChangeTracker.AcceptAllChanges();
            var levelUpReply = new ClaimLevelRewardReply()
            {
                RewardResult = takeRewardResult,
                LevelInfo = userAsset.LevelData,
            };

            return Ok(levelUpReply);
        });
    }
}