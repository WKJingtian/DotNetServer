using ChillyRoom.Games.BuildingGame.Services;
using ChillyRoom.GenericPlayerService.v1;
using GameOutside.Util;
using Google.Protobuf.Collections;

public class PlayerModule(
    CacheManager cacheManager,
    GenericPlayerAPI.GenericPlayerAPIClient playerClient)
{
    public async ValueTask<long?> GetPlayerUserId(long playerId)
    {
        var playerGameDataUid = await cacheManager.GetPlayerGameDataUid(playerId);
        if (playerGameDataUid.HasValue)
        {
            return playerGameDataUid.Value;
        }

        var getPlayerRequest = new GetPlayerByPlayerIdRequest() { Pid = playerId };
        var reply = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(
            async () => await playerClient.GetPlayerByPlayerIdAsync(getPlayerRequest));
        if (reply is null || reply.Uid <= 0)
        {
            return null;
        }

        await cacheManager.SetPlayerGameDataUid(playerId, reply.Uid);
        return reply.Uid;
    }

    public async ValueTask<short?> GetPlayerShardId(long playerId)
    {
        var playerGameDataShard = await cacheManager.GetPlayerGameDataShard(playerId);
        if (playerGameDataShard.HasValue)
        {
            return playerGameDataShard.Value;
        }

        var getPlayerRequest = new GetPlayerByPlayerIdRequest() { Pid = playerId };
        var reply = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(
            async () => await playerClient.GetPlayerByPlayerIdAsync(getPlayerRequest));
        if (reply is null || reply.ShardId <= 0)
        {
            return null;
        }

        await cacheManager.SetPlayerGameDataShard(playerId, (short)reply.ShardId);
        return (short)reply.ShardId;
    }

    public async ValueTask<long?> GetLastLoginPlayerIdByUserId(long uid)
    {
        var pid = await cacheManager.GetPidByUidAsync(uid);
        if (pid.HasValue)
            return pid.Value;

        var getPlayersRequest = new FindOtherPlayersRequest() {Uid = uid};
        var reply = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(async () =>
            await playerClient.FindOtherPlayersAsync(getPlayersRequest));

        var latestPlayer = reply.Players.Where(p => p.Pid > 0).MaxBy(player => player.LastLoginAt);
        if (latestPlayer == null)
        {
            return null;
        }

        await cacheManager.SetPidByUidAsync(uid, latestPlayer.Pid);
        return latestPlayer.Pid;
    }

    public async ValueTask<Dictionary<long, long>> BatchGetLastLoginPlayerIdsByUserIds(List<long> uids)
    {
        var result = new Dictionary<long, long>();
        var needQueryUidList = new List<long>();
        foreach (var uid in uids)
        {
            var pid = await cacheManager.GetPidByUidAsync(uid);
            if (pid.HasValue)
                result.TryAdd(uid, pid.Value);
            else
                needQueryUidList.Add(uid);
        }

        if (needQueryUidList.Count > 0)
        {
            var batchFindPlayersRequest = new BatchFindOtherPlayersRequest();
            batchFindPlayersRequest.Uids.AddRange(needQueryUidList);
            var reply = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(async () =>
                await playerClient.BatchFindOtherPlayersAsync(batchFindPlayersRequest));

            var playersGroup = reply.Players.GroupBy(p => p.Uid);
            foreach (var players in playersGroup)
            {
                var latestPlayer = players.Where(p => p.Pid > 0).MaxBy(player => player.LastLoginAt);
                if (latestPlayer == null)
                    continue;

                await cacheManager.SetPidByUidAsync(latestPlayer.Uid, latestPlayer.Pid);
                result.TryAdd(latestPlayer.Uid, latestPlayer.Pid);
            }
        }

        return result;
    }
}