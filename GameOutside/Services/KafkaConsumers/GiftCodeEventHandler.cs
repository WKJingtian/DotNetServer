using System.Text.Json;
using System.Text.Json.Serialization;
using ChillyRoom.Infra.LiveMessage;
using ChillyRoom.Infra.PlatformDef.Config;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.Controllers;
using GameOutside.Services.PlatformItemsService;
using GameOutside.Util;
using GiftCodeApiService;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameOutside.Services.KafkaConsumers;

public class GiftCodeEventHandler : GiftCodeExchangeHandler
{
    private readonly ILogger<GiftCodeEventHandler> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILiveMessageController _liveMessageController;

    private const string NotificationTopic = "building-game-gift-code";

    public GiftCodeEventHandler(
        IOptionsMonitor<PlatformKafkaConfig> config,
        ILogger<GiftCodeEventHandler> logger,
        IServiceScopeFactory serviceScopeFactory,
        ILiveMessageController liveMessageController) : base(logger, config.CurrentValue.GiftEventHandlerGroup)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _liveMessageController = liveMessageController;

        UpdateConsumerConfig(config.CurrentValue.Brokers, config.CurrentValue.GiftCodeTopic,
            config.CurrentValue.GiftCodeDlqTopic);
        config.OnChange(
            (c, _) => UpdateConsumerConfig(c.Brokers, c.GiftCodeTopic,
                c.GiftCodeDlqTopic));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _liveMessageController.SetupBroadcastChannel(ChannelPattern.UserTopic,
                "building-game/gift-code/{uid}", NotificationTopic, ChannelAuthorizationMode.GrantToUser, null, null);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogError("无法注册消息网关，通知可能无法发送");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async ValueTask OnGiftCodeExchanged(GiftExchangedSuccessEvent msg, CancellationToken stoppingToken)
    {
        await HandleMsgWrapAsync(async () =>
        {
            var rewardList = ParseGiftCodeContent(msg);
            var payload = JsonSerializer.Serialize(rewardList);

            if (!Guid.TryParse(msg.GiftCodeId, out var giftCodeId))
            {
                throw _giftCodeIdNotValidGuidException;
            }

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var platformItemsService = scope.ServiceProvider.GetRequiredService<PlatformItemsService.PlatformItemsService>();

            var result = await platformItemsService.ClaimPlatformNotifyAttachmentsAsync(rewardList,
                new PlatformNotifyAttachmentsMetadata(msg.PlayerId, msg.ShardId, NotifyType.GiftCode, giftCodeId));
            var cacheValue = new ClaimAttachmentMessage(giftCodeId, result);
            await platformItemsService.SaveGiftCodeItemsCacheAsync(msg.PlayerId, JsonSerializer.Serialize(cacheValue));

            // 发送到账通知到客户端，若客户端不在线则丢弃消息
            _ = Task.Run(async () =>
            {
                try
                {
                    await _liveMessageController.SendJsonMessage(NotificationTopic, msg.GiftCodeId, userId: msg.UserId,
                        messageGroup: msg.UserId.ToString());
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "玩家 {UserId}-{PlayerId} 礼包码 {GiftCodeId} 附件领取通知发送失败",
                        msg.UserId, msg.PlayerId, msg.GiftCodeId);
                }
            });
        }, msg);
    }

    private List<RewardItemData> ParseGiftCodeContent(GiftExchangedSuccessEvent msg)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<GiftCodeItem>>(msg.Content)!;
            return items.Select(item => new RewardItemData { Id = int.Parse(item.Id), Count = item.Count }).ToList();
        }
        catch (Exception e)
        {
            throw new GiftCodeException(GiftCodeErrorCode.ErrorParseGiftCodeContent, "parse GiftCode Content error", e);
        }
    }

    private async Task HandleMsgWrapAsync(Func<Task> func, GiftExchangedSuccessEvent msg)
    {
        try
        {
            await func.Invoke();
        }
        // 重复领取礼包码消息，不打印 error 日志，也不用抛出异常
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            _logger.LogWarning(e,
                "{PlayerId} finish handle duplicated GiftCodeEventHandler {Content}",
                msg.PlayerId, JsonSerializer.Serialize(msg));
        }
    }

    public class GiftCodeItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private readonly GiftCodeException _giftCodeIdNotValidGuidException = new(GiftCodeErrorCode.GiftCodeIdNotValidGuid, "GiftCodeId is not valid Guid");

    protected override bool IsUnprocessableException(Exception e)
    {
        return e is GiftCodeException
        {
            ErrorCode: GiftCodeErrorCode.GiftCodeIdNotValidGuid or GiftCodeErrorCode.ErrorParseGiftCodeContent
        };
    }
}

internal sealed class GiftCodeException(GiftCodeErrorCode errorCode, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public GiftCodeErrorCode ErrorCode { get; } = errorCode;
}

internal enum GiftCodeErrorCode
{
    None = 0,
    GiftCodeIdNotValidGuid = 1,
    ErrorParseGiftCodeContent = 2
}
