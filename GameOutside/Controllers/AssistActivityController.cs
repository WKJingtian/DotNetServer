using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AssistActivity.Models;
using System.Text.Json.Serialization;
using ChillyRoom.BuildingGame.Models;
using GameOutside;
using Serilog;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Repositories;

namespace AssistActivity.Controllers;

public struct AssistActivityInfoResponse
{
    public long PlayerId { get; set; }
    public Guid DistroId { get; set; }
    public string ActivityName { get; set; }
    public int BeAssistedCount { get; set; }
    public int MaxAssistCount { get; set; } // 每人助力次数上限
    public string MyInviteCode { get; set; }
    public string GameDownloadUrl { get; set; }
    public string ShareLink { get; set; }
    public AssistStatus AssistStatus { get; set; }
}

public enum ClaimedRewardStatus
{
    UnSpeacified = 0, // 未指定状态
    NotAvailable = 1, // 不可领取
    Availble = 2, // 可领取
    Claimed = 3 // 已领取
}

// 助力成功 ｜ 已助力过 ｜ 达到助力上限
public enum AssistStatus
{
    UnSpeacified = 0, // 未指定状态
    AssistSuccess = 1, // 助力成功
    AlreadyAssisted = 2, // 已助力过
    TargetLimitReached = 3 // 达到助力上限
}

public static class AssistActivityInfoExtensions
{
    public static AssistActivityInfoResponse ToResponse(
        this PlayerAssistActivityInfo player,
        AssistActivityConfig activityConfig,
        string shareLink = null)
    {
        return new AssistActivityInfoResponse
        {
            PlayerId = player.PlayerId,
            DistroId = player.DistroId,
            ActivityName = player.ActivityName,
            BeAssistedCount = player.GetPlayerBeAssistedCount(),
            MyInviteCode = activityConfig.InviteCodePrefix + player.Payload.InviteCode,
            MaxAssistCount = activityConfig.MaxInviteAssists,
            GameDownloadUrl =
                activityConfig.GameDownloadUrls.TryGetValue(player.DistroId.ToString(), out var url)
                    ? url
                    : string.Empty,
            ShareLink = shareLink,
        };
    }

    public static AssistActivityInfoResponse ToResponse(
        this PlayerAssistActivityInfo player,
        AssistActivityConfig activityConfig,
        AssistStatus status)
    {
        return new AssistActivityInfoResponse
        {
            PlayerId = player.PlayerId,
            DistroId = player.DistroId,
            ActivityName = player.ActivityName,
            BeAssistedCount = player.GetPlayerBeAssistedCount(),
            MyInviteCode = activityConfig.InviteCodePrefix + player.Payload.InviteCode,
            MaxAssistCount = activityConfig.MaxInviteAssists,
            GameDownloadUrl =
                activityConfig.GameDownloadUrls.TryGetValue(player.DistroId.ToString(), out var url)
                    ? url
                    : string.Empty,
            AssistStatus = status
        };
    }
}

[EnableCors("AllowAll")]
public class AssistActivityController(
    IConfiguration configuration,
    ILogger<AssistActivityController> logger,
    BuildingGameDB db,
    IDiagnosticContext diagnosticContext,
    ServerConfigService serverConfigService,
    UserAssetService userAssetService,
    IOptionsMonitor<AssistActivityConfig> _assistActivityConfig,
    IAssistActivityRepository assistActivityRepository) : BaseApiController(configuration)
{
    private AssistActivityConfig _activityConfig => _assistActivityConfig.CurrentValue;
    private int MAX_INVITE_ASSISTS => _activityConfig.MaxInviteAssists; // 每人被助力次数上限

    public struct InviteCodeAssistRequest
    {
        [JsonPropertyName("inviteCode")]
        public string InviteCode { get; set; }

        [JsonPropertyName("uniqueIdentifier")]
        public string UniqueIdentifier { get; set; }
    }

    /// <summary>
    /// 邀请码助力
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssistActivityInfoResponse>> Assist([FromBody] InviteCodeAssistRequest request)
    {
        diagnosticContext.Set("AssistArgs", request, true);

        var inviteCode = request.InviteCode.Trim()[_activityConfig.InviteCodePrefix.Length..];

        logger.LogInformation("Assist: InviteCode={Code}", inviteCode);

        // 验证邀请码格式并解析信息
        if (!InviteCode.IsValidInviteCode(inviteCode))
        {
            logger.LogWarning("Invalid invite code format: {Code}", inviteCode);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = (int)ActivityErrorCode.INVITE_CODE_INVALID_FORMAT,
                Message = "邀请码格式无效"
            });
        }

        try
        {
            var response = await db.WithRCUDefaultRetry<ActionResult<AssistActivityInfoResponse>>(async (_) =>
            {
                var ctx = await db.Database.BeginTransactionAsync();

                // 使用完整的PlayerId和DistroId查找目标玩家
                var targetPlayer = await assistActivityRepository.GetPlayerAssistActivityInfoByInviteCode(inviteCode);

                if (targetPlayer == null)
                {
                    logger.LogWarning("Target player not found: InviteCode={InviteCode}", inviteCode);
                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = (int)ActivityErrorCode.PLAYER_NOT_FOUND,
                        Message = "目标玩家不存在"
                    });
                }

                if (targetPlayer.Payload.GetHistoryInviteCodesOrEmpty().Contains(inviteCode))
                {
                    logger.LogWarning("Target player invite code expired: {TargetId}", targetPlayer.PlayerId);
                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = (int)ActivityErrorCode.INVITE_CODE_EXPIRED,
                        Message = "邀请码已过期"
                    });
                }

                if (targetPlayer.GetPlayerBeAssistedCount() >= MAX_INVITE_ASSISTS)
                {
                    logger.LogWarning("Target player assist limit reached: {TargetId}", targetPlayer.PlayerId);
                    return Ok(targetPlayer.ToResponse(_activityConfig, AssistStatus.TargetLimitReached));
                }

                if (targetPlayer.HasAssistedFromUniqueIdentifier(request.UniqueIdentifier))
                    return Ok(targetPlayer.ToResponse(_activityConfig, AssistStatus.AlreadyAssisted));

                targetPlayer.Payload.BeAssistedCount++;
                targetPlayer.Payload.AssistFromUniqueIdentifier.Add(request.UniqueIdentifier);

                db.Entry(targetPlayer).Property(p => p.Payload).IsModified = true;

                await db.SaveChangesWithDefaultRetryAsync(false);
                await ctx.CommitAsync();

                logger.LogInformation("Assist success: {TargetId} ⬆️{BeAssistedCount}", targetPlayer.PlayerId,
                    targetPlayer.Payload.BeAssistedCount);

                return Ok(targetPlayer.ToResponse(_activityConfig, AssistStatus.AssistSuccess));
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Assist failed");
            return BadRequest(new ErrorResponse { ErrorCode = (int)CommonErrorCodes.INTERNAL_ERROR, Message = "助力失败" });
        }
    }

    public record struct GetAssistActivityInfoResponseWrapper(
        AssistActivityInfoResponse? AssistActivityInfo,
        UserH5FriendActivityInfo UserH5FriendActivityInfo);

    /// <summary>
    /// 查询活动信息
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GetAssistActivityInfoResponseWrapper>> GetInfo()
    {
        try
        {
            var currentPlayerId = this.PlayerId;
            logger.LogInformation("GetInfo for player: {PlayerId}", currentPlayerId);
            // 先检查配置是不是就绪
            if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityUnlockLevel,
                    out var activityUnlockLevel))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
            var userLevelData = await userAssetService.GetLevelDataAsync(PlayerShard, currentPlayerId);
            if (userLevelData == null)
                return BadRequest(ErrorKind.NO_USER_RECORDS.Response());

            // 事务封装
            return await db.WithRCUDefaultRetry<ActionResult<GetAssistActivityInfoResponseWrapper>>(async (_) =>
            {
                // 检查是否达到暴露链接的条件
                var (created, activityInfo) =
                    await db.GetOrCreateUserH5FriendActivityInfo(PlayerId, PlayerShard, activityUnlockLevel);
                if (userLevelData.Level < activityInfo.NextShowingLevelV1)
                {
                    // 还没达到暴露分享链接的条件，只返回activityInfo
                    if (created)
                        await db.SaveChangesWithDefaultRetryAsync();
                    return Ok(new GetAssistActivityInfoResponseWrapper(null, activityInfo));
                }
                var player = await assistActivityRepository.GetPlayerAssistActivityInfo(currentPlayerId, PlayerShard, DistroId);

                var dbDirty = created;
                if (player == null)
                {
                    player = new PlayerAssistActivityInfo
                    {
                        PlayerId = currentPlayerId,
                        DistroId = this.DistroId,
                        ShardId = this.PlayerShard,
                        ActivityName = _activityConfig.Name,
                        Payload = new PlayerAssistActivityInfo.AssistActivityPayload
                        {
                            InviteCode = InviteCode.GenerateCode(
                                currentPlayerId,
                                DistroId),
                            BeAssistedCount = 0,
                            HistoryInviteCodes = Array.Empty<string>(),
                            AssistFromUniqueIdentifier = [],
                            InviteCodeRedeemedPlayerIds = []
                        }
                    };

                    await db.AddAsync(player);
                    dbDirty = true;
                }

                if (dbDirty)
                {
                    var ctx = await db.Database.BeginTransactionAsync();
                    await db.SaveChangesWithDefaultRetryAsync(false);
                    await ctx.CommitAsync();
                }
                var query = $"key={_activityConfig.InviteCodePrefix}{player.Payload.InviteCode}";
                var playerInfo = player.ToResponse(_activityConfig, $"{_activityConfig.ShareLink}?{query}");
                return Ok(new GetAssistActivityInfoResponseWrapper(playerInfo, activityInfo));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetInfo failed");
            return BadRequest(new ErrorResponse { ErrorCode = (int)CommonErrorCodes.INTERNAL_ERROR, Message = "查询失败" });
        }
    }
}