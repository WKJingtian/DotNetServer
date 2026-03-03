using AssistActivity.Models;
using GameOutside.DBContext;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IAssistActivityRepository
{
    public Task<PlayerAssistActivityInfo?> GetPlayerAssistActivityInfoByInviteCode(string inviteCode);

    public Task<(PlayerAssistActivityInfo? AssistActivityInfo, int Round)> GetPlayerAssistActivityInfoByInviteCodeWithRound(string inviteCode);

    public Task<PlayerAssistActivityInfo?> GetPlayerAssistActivityInfo(long playerId, short playerShard, Guid distroId);
}

public class AssistActivityRepository(
    BuildingGameDB dbCtx,
    PlayerModule playerModule) : IAssistActivityRepository
{
    public async Task<PlayerAssistActivityInfo?> GetPlayerAssistActivityInfoByInviteCode(string inviteCode)
    {
        var inviteCodeResult = InviteCode.ParseCode(inviteCode);

        if (!inviteCodeResult.HasValue)
        {
            return null;
        }

        var (parsedPlayerId, parsedDistroIdHash, _) = inviteCodeResult.Value;
        var shardId = await playerModule.GetPlayerShardId(parsedPlayerId);
        var activityInfoes = await dbCtx.PlayerAssistActivityInfos
            .Where(m => shardId.HasValue ? m.ShardId == shardId.Value && m.PlayerId == parsedPlayerId : m.PlayerId == parsedPlayerId)
            .ToListAsync();

        return activityInfoes.FirstOrDefault(m => InviteCode.IsCodeMatch(inviteCode, m.PlayerId, m.DistroId));
    }

    public async Task<(PlayerAssistActivityInfo? AssistActivityInfo, int Round)> GetPlayerAssistActivityInfoByInviteCodeWithRound(string inviteCode)
    {
        var inviteCodeResult = InviteCode.ParseCode(inviteCode);

        if (!inviteCodeResult.HasValue)
        {
            return (null, 0);
        }

        var (parsedPlayerId, parsedDistroIdHash, round) = inviteCodeResult.Value;
        var shardId = await playerModule.GetPlayerShardId(parsedPlayerId);
        var activityInfoes = await dbCtx.PlayerAssistActivityInfos
            .Where(m => shardId.HasValue ? m.ShardId == shardId.Value && m.PlayerId == parsedPlayerId : m.PlayerId == parsedPlayerId)
            .ToListAsync();
        var playerActivityInfo = activityInfoes.FirstOrDefault(m =>
            m.Payload.InviteCode == inviteCode || m.Payload.HistoryInviteCodes.Contains(inviteCode));

        if (playerActivityInfo is null)
        {
            return (null, 0);
        }

        return (playerActivityInfo, round);
    }

    public async Task<PlayerAssistActivityInfo?> GetPlayerAssistActivityInfo(
        long playerId, short playerShard, Guid distroId)
        => await dbCtx.PlayerAssistActivityInfos.FirstOrDefaultAsync(m => m.PlayerId == playerId && m.ShardId == playerShard && m.DistroId == distroId);
}