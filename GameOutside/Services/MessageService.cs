using ChillyRoom.Infra.LiveMessage;
using GameOutside.Controllers;
using Grpc.Core;

namespace GameOutside;

public class MessageService
{
    private readonly ILogger<MessageService> _logger;
    private readonly ILiveMessageController _liveMessageController;

    private const string _notificationTopic = "building-game-server-push";

    public MessageService(ILogger<MessageService> logger, ILiveMessageController liveMessageController)
    {
        _logger = logger;
        _liveMessageController = liveMessageController;
        _ = Init();
    }

    private async Task Init()
    {
        try
        {
            await _liveMessageController.SetupBroadcastChannel(ChannelPattern.UserTopic,
                "building-game/server-push/{uid}", _notificationTopic, ChannelAuthorizationMode.GrantToUser, null,
                null);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogError("无法注册消息网关，通知可能无法发送");
        }
    }

    public enum MessageType
    {
        NoticeFriendIdleRewardChanged = 1,
        InviteFriendToRoom = 11,
        PlayerBanned = 21,
    }

    private class CommonMessage<T>
    {
        public MessageType Type { get; set; }
        public T? Payload { get; set; }
    }

    public void SendMessage<T>(long userId, MessageType type, T message)
    {
        Task.Run(async () =>
        {
            try
            {
                var cm = new CommonMessage<T> { Type = type, Payload = message };

                await _liveMessageController.SendJsonMessage(_notificationTopic, cm,
                    userId: userId,
                    messageGroup: userId.ToString());
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "发送消息失败 领取者: {UserId}", userId);
            }
        });
    }


    private class IdleRewardMessage
    {
        public required long ChangedUserId { get; set; }
    }

    public void NoticeFriendIdleRewardChanged(long userId, long changedUserId)
    {
        var message = new IdleRewardMessage { ChangedUserId = changedUserId, };
        SendMessage(userId, MessageType.NoticeFriendIdleRewardChanged, message);
    }


    public class InviteFriendToRoomMessage
    {
        public long FromUserId { get; set; }
        public string RoomId { get; set; }
        public int GameType { get; set; }
        // 活动用的
        public int ActivityId { get; set; }
        public int IntArg0 { get; set; }
    }

    public void InviteFriendToRoom(InviteFriendRequest request, long fromUserId)
    {
        SendMessage(request.UserId, MessageType.InviteFriendToRoom, new InviteFriendToRoomMessage
        {
            FromUserId = fromUserId,
            RoomId = request.RoomId,
            GameType = request.InviteGameType,
            ActivityId = request.ActivityId,
            IntArg0 = request.IntArg0
        });
    }
}