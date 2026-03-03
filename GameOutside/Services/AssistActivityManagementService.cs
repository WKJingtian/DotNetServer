using AssistActivity.Controllers;
using AssistActivity.Models;
using GameOutside.DBContext;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Serilog;
using GameOutside.Repositories;

namespace AssistActivity.Services;

/// <summary>
/// gRPC service implementation for Activity Management operations
/// Provides validation and information retrieval for player activities
/// </summary>
public class AssistActivityManagementService(
    ILogger<AssistActivityManagementService> logger,
    BuildingGameDB db,
    IOptionsMonitor<AssistActivityConfig> assistActivityConfig,
    IDiagnosticContext diagnosticContext,
    IAssistActivityRepository assistActivityRepository)
{
    private AssistActivityConfig ActivityConfig => assistActivityConfig.CurrentValue;
    private int MAX_INVITE_ASSISTS => ActivityConfig.MaxInviteAssists;

    public class PlayerActivityInfo
    {
        public long PlayerId { get; set; }
        public string DistroId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public int BeAssistedCount { get; set; }
        public int MaxAssistCount { get; set; }
        public string MyInviteCode { get; set; } = string.Empty;
        public string GameDownloadUrl { get; set; } = string.Empty;
        public string ShareLink { get; set; } = string.Empty;
        public int AssistStatus { get; set; }
        public List<InviteCodeRedeemedInfo> InviteCodeRedeemedPlayerIds { get; set; } = new();
        public int Round { get; set; }
    }

    public class ValidateActivityRequest
    {
        public string InviteCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates an invite code and returns the corresponding player activity information
    /// </summary>
    /// <param name="request">The validation request containing the invite code</param>
    /// <param name="context">The gRPC server call context</param>
    /// <returns>Player activity information if the invite code is valid</returns>
    public async Task<PlayerActivityInfo> Validate(ValidateActivityRequest request)
    {
        try
        {
            logger.LogInformation("ActivityManagement.Validate called with InviteCode: {InviteCode}",
                request.InviteCode?.Substring(0, Math.Min(request.InviteCode.Length, 5)) + "...");

            diagnosticContext.Set("InviteCode", request.InviteCode, true);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(request.InviteCode))
            {
                logger.LogWarning("Invalid request: InviteCode is null or empty");
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invite code cannot be null or empty"));
            }

            // Remove prefix if present and validate format
            var inviteCode = request.InviteCode.Trim();
            if (inviteCode.StartsWith(ActivityConfig.InviteCodePrefix))
            {
                inviteCode = inviteCode[ActivityConfig.InviteCodePrefix.Length..];
            }

            // Validate invite code format
            if (!InviteCode.IsValidInviteCode(inviteCode))
            {
                logger.LogWarning("Invalid invite code format: {InviteCode}", inviteCode);
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid invite code format"));
            }

            // Find the player by invite code
            var (player, round) = await assistActivityRepository.GetPlayerAssistActivityInfoByInviteCodeWithRound(inviteCode);

            if (player == null)
            {
                logger.LogWarning("Player not found for invite code: {InviteCode}", inviteCode);
                throw new RpcException(new Status(StatusCode.NotFound,
                    "Player not found for the provided invite code"));
            }

            // Build the response
            var response = BuildPlayerActivityInfo(player, round);

            logger.LogInformation("ActivityManagement.Validate completed successfully for PlayerId: {PlayerId}",
                player.PlayerId);

            return response;
        }
        catch (RpcException)
        {
            // Re-throw gRPC exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during ActivityManagement.Validate");
            throw new RpcException(new Status(StatusCode.Internal, "An internal error occurred during validation"));
        }
    }

    public class RedeemInviteCodeRequest
    {
        public long PlayerId { get; set; }
        public string InviteCode { get; set; } = string.Empty;
        public short PlayerShard { get; set; }
        public Guid DistroId { get; set; } = Guid.Empty;
    }

    public class InviteCodeRedeemedInfo
    {
        public int Round { get; set; }
        public List<long> PlayerId { get; set; } = new();
    }

    // 领取邀请码
    public async Task<PlayerActivityInfo> RedeemInviteCode(RedeemInviteCodeRequest request)
    {
        var inviteCode = request.InviteCode.Trim();

        if (inviteCode.StartsWith(ActivityConfig.InviteCodePrefix))
        {
            inviteCode = inviteCode[ActivityConfig.InviteCodePrefix.Length..];
        }

        // Implementation for redeeming invite codes would go here
        var (player, round) = await assistActivityRepository.GetPlayerAssistActivityInfoByInviteCodeWithRound(inviteCode);

        // 可能为历史邀请码，历史邀请码也允许领取
        if (player == null)
        {
            logger.LogWarning("Player not found or invite code mismatch for PlayerId: {PlayerId}", request.PlayerId);
            throw new RpcException(new Status(StatusCode.NotFound, "Player not found or invite code mismatch"));
        }

        var redeemedPlayerIds = player.Payload.InviteCodeRedeemedPlayerIds
            .GetValueOrDefault(round, []);

        if (redeemedPlayerIds.Count >= ActivityConfig.MaxInviteCodeRedemptions)
        {
            logger.LogWarning("Max invite assists reached for PlayerId: {PlayerId}", request.PlayerId);
            return BuildPlayerActivityInfo(player, round);
        }

        if (redeemedPlayerIds.Contains(request.PlayerId))
        {
            return BuildPlayerActivityInfo(player, round);
        }

        redeemedPlayerIds.Add(request.PlayerId);

        player.Payload.InviteCodeRedeemedPlayerIds[round] = redeemedPlayerIds;

        db.Entry(player).Property(p => p.Payload).IsModified = true;

        return BuildPlayerActivityInfo(player, round);
    }

    public class ResetInviteCodeRequest
    {
        public long PlayerId { get; set; }
        public short PlayerShard { get; set; }
        public Guid DistroId { get; set; } = Guid.Empty;
    }

    public async Task<PlayerActivityInfo> Reset(ResetInviteCodeRequest request)
    {
        try
        {
            var currentPlayerId = request.PlayerId;
            logger.LogInformation("Reset for player: {PlayerId}", currentPlayerId);

            // 先进行数据验证
            var currentPlayer = await assistActivityRepository.GetPlayerAssistActivityInfo(currentPlayerId, request.PlayerShard, request.DistroId);
            if (currentPlayer == null)
            {
                logger.LogWarning("Player not found for reset: {PlayerId}", currentPlayerId);
                throw new RpcException(new Status(StatusCode.NotFound, "Player not found"));
            }

            if (currentPlayer.GetPlayerBeAssistedCount() < MAX_INVITE_ASSISTS)
            {
                logger.LogWarning(
                    "Player reset not eligible - assist count not reached limit: {PlayerId}, Current: {Count}, Limit: {Limit}",
                    currentPlayerId, currentPlayer.GetPlayerBeAssistedCount(), MAX_INVITE_ASSISTS);

                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "Player assist count has not reached the limit, cannot reset"));
            }

            var oldInviteCodeDecoded = InviteCode.ParseCode(currentPlayer.Payload.InviteCode);

            currentPlayer.Payload.BeAssistedCount = 0;
            currentPlayer.Payload.AssistFromUniqueIdentifier.Clear();
            currentPlayer.Payload.HistoryInviteCodes = currentPlayer.Payload.GetHistoryInviteCodesOrEmpty()
                .Append(currentPlayer.Payload.InviteCode)
                .ToArray();
            currentPlayer.Payload.InviteCode = InviteCode.GenerateCode(
                currentPlayer.PlayerId,
                currentPlayer.DistroId,
                oldInviteCodeDecoded!.Value.counter + 1);
            // 注意：刷新时不清空 InviteCodeRedeemedPlayerIds
            // 旧邀请码保持其历史状态（已满员则仍然满员）

            db.Entry(currentPlayer).Property(p => p.Payload).IsModified = true;
            logger.LogInformation("Reset success: {PlayerId} - assist count reset to 0", currentPlayerId);

            return BuildPlayerActivityInfo(currentPlayer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reset failed");
            throw new RpcException(new Status(StatusCode.Internal, "Reset failed"));
        }
    }

    /// <summary>
    /// Builds a PlayerActivityInfo response from the database entity
    /// </summary>
    /// <param name="player">The player entity from the database</param>
    /// <returns>A properly formatted PlayerActivityInfo response</returns>
    private PlayerActivityInfo BuildPlayerActivityInfo(
        PlayerAssistActivityInfo player, int round = 0)
    {
        // Generate the full invite code with prefix
        var fullInviteCode = ActivityConfig.InviteCodePrefix + player.Payload.InviteCode;

        // Build the share link with the invite code parameter
        var shareLink = string.IsNullOrEmpty(ActivityConfig.ShareLink)
            ? string.Empty
            : $"{ActivityConfig.ShareLink}?key={fullInviteCode}";

        // Get the appropriate download URL for the player's distribution
        var gameDownloadUrl = ActivityConfig.GameDownloadUrls
            .TryGetValue(player.DistroId.ToString(), out var url)
            ? url
            : string.Empty;

        // Determine assist status based on current assist count
        var assistStatus = player.GetPlayerBeAssistedCount() >= ActivityConfig.MaxInviteAssists
            ? (int)AssistStatus.TargetLimitReached
            : (int)AssistStatus.UnSpeacified;

        var inviteCodeRedeemedInfos = player.Payload.InviteCodeRedeemedPlayerIds
            .Select(kv => new InviteCodeRedeemedInfo { Round = kv.Key, PlayerId = kv.Value.ToList() }).ToList();

        return new PlayerActivityInfo
        {
            PlayerId = player.PlayerId,
            DistroId = player.DistroId.ToString(),
            ActivityName = player.ActivityName ?? ActivityConfig.Name,
            BeAssistedCount = player.GetPlayerBeAssistedCount(),
            MaxAssistCount = ActivityConfig.MaxInviteAssists,
            MyInviteCode = fullInviteCode,
            GameDownloadUrl = gameDownloadUrl,
            ShareLink = shareLink,
            AssistStatus = assistStatus,
            InviteCodeRedeemedPlayerIds = inviteCodeRedeemedInfos,
            Round = round
        };
    }
}