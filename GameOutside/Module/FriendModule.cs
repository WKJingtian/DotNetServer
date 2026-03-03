using ChillyRoom.Games.BuildingGame.Services;
using ChillyRoom.GenericPlayerService.v1;
using ChillyRoom.ImService;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Util;

namespace GameOutside;

public class FriendModule
{
    private readonly ILogger<FriendModule> _logger;
    private readonly GenericPlayerAPI.GenericPlayerAPIClient _playerClient;
    private readonly MessagingAPI.MessagingAPIClient _imClient;

    private readonly PlayerModule _playerModule;
    private readonly BuildingGameDB _context;

    public FriendModule(
        ILogger<FriendModule> logger,
        GenericPlayerAPI.GenericPlayerAPIClient playerClient,
        MessagingAPI.MessagingAPIClient imClient,
        BuildingGameDB context,
        PlayerModule playerModule)
    {
        _logger = logger;
        _playerClient = playerClient;
        _imClient = imClient;
        _context = context;
        _playerModule = playerModule;
    }

    public ValueTask<long?> GetUserIdByPlayerId(long playerId, long selfPlayerId = 0, long selfUserId = 0)
    {
        if (selfPlayerId != 0 && playerId == selfPlayerId)
            return ValueTask.FromResult<long?>(selfUserId);
        return _playerModule.GetPlayerUserId(playerId);
    }

    public async Task<(long, UserIdleRewardInfo)[]> BatchGetPlayerIdleRewardData(List<long> playerIds)
    {
        var infos = await _context.BatchGetUserIdleRewardInfo(playerIds);
        return infos.Select(asset => (asset.PlayerId, asset)).ToArray();
    }

    // 好友相关
    public async Task<bool> CheckIsFriend(long userId1, long userId2)
    {
        var request = new QueryUserRelationshipsRequest() { UserId = userId1, QueryUserIds = { userId2 } };
        var result = await GrpcExtensions.GrpcDefaultRetryPolicy.ExecuteAsync(async () => await _imClient.QueryUserRelationshipsAsync(request));
        return result.RelatedUsers.Any(relatedUser =>
            relatedUser.Relationship == UserRelationship.Friend && relatedUser.UserId == userId2);
    }
}