using System.Text.Json;
using ChillyRoom.PayService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Grpc.Core;
using ChillyRoom.Infra.LiveMessage;
using ChillyRoom.MailService;
using GameOutside.Services.PlatformItemsService;
using GameOutside.Util;
using MailClient.SceneBuilders;
using Npgsql;
using ChillyRoom.Infra.PlatformDef.Config;

namespace GameOutside.Services.KafkaConsumers;

public class PayEventHandler : OrderStatusNotificationHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PayEventHandler> _logger;
    private readonly ILiveMessageController _liveMessageController;
    private readonly IOptionsMonitor<PlatformKafkaConfig> _platformKafkaConfig;
    private readonly MailSender _mailSender;

    private const string NotificationTopic = "building-game-new-item";

    public PayEventHandler(IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<PlatformKafkaConfig> platformKafkaConfig,
        ILogger<PayEventHandler> logger,
        ILiveMessageController liveMessageController,
        MailSender mailSender) : base(logger,
        platformKafkaConfig.CurrentValue.PayEventHandlerGroup)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _liveMessageController = liveMessageController;
        _platformKafkaConfig = platformKafkaConfig;
        _mailSender = mailSender;

        UpdateConsumerConfig(platformKafkaConfig.CurrentValue.Brokers, platformKafkaConfig.CurrentValue.PaySuccessTopic,
            platformKafkaConfig.CurrentValue.PaySuccessDlqTopic);
        platformKafkaConfig.OnChange((c, _) => UpdateConsumerConfig(
            c.Brokers, c.PaySuccessTopic, c.PaySuccessDlqTopic));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _liveMessageController.SetupBroadcastChannel(ChannelPattern.UserTopic, "building-game/new-item/{uid}",
                NotificationTopic, ChannelAuthorizationMode.GrantToUser, null, null);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogError("无法注册消息网关，通知可能无法发送");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async ValueTask OnOrderPaid(OrderStatusEvent ev, CancellationToken stoppingToken)
    {
        await HandleMsgWrapAsync(async () =>
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var platformItemsService
                = scope.ServiceProvider.GetRequiredService<PlatformItemsService.PlatformItemsService>();
            await platformItemsService.AddPaidOrderWithShard(ev);
            // 发送到账通知到客户端，若客户端不在线则丢弃消息
            _ = Task.Run(async () =>
            {
                try
                {
                    // 只发orderId，节省下带宽
                    await _liveMessageController.SendJsonMessage(NotificationTopic, ev.OrderId, userId: ev.UserId,
                        messageGroup: ev.UserId.ToString());
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "订单 {OrderId} 到货通知发送失败", ev.OrderId);
                }
            });
        }, ev, "OrderPaid");
    }

    protected override async ValueTask OnOrderRefund(OrderStatusEvent ev, CancellationToken stoppingToken)
    {
        await HandleMsgWrapAsync(async () =>
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var platformItemsService = scope.ServiceProvider.GetRequiredService<PlatformItemsService.PlatformItemsService>();

            var refundDiamondCount = await platformItemsService.RefundPaidOrderAttachmentsAsync(ev);
            await SendRefundNotifyMail(ev, refundDiamondCount);
        }, ev, "OrderRefund");
    }

    private Task SendRefundNotifyMail(OrderStatusEvent ev, int refundDiamondCount)
    {
        try
        {
            var request = new SendMailWithWildcardLocalizedVariablesRequest(
                _platformKafkaConfig.CurrentValue.RefundOrderEmailTemplateId,
                new PlayerIdFilterBuilder().Eq(ev.PlayerId),
                DateTime.UtcNow.AddMonths(1),
                null,
                new Dictionary<string, string>
                {
                    {"OrderId", ev.OrderId.ToString()},
                    {"DiamondCount", refundDiamondCount.ToString()}
                }
            );
            return _mailSender.SendMailWithWildcardLocalizedVariablesAsync(request);
        }
        catch (Exception e)
        {
            throw new PayException(PayErrorCode.ErrorSendRefundNotifyMail, "send refund notify mail failed", e);
        }
    }

    private async Task HandleMsgWrapAsync(Func<Task> func, OrderStatusEvent ev, string orderType)
    {
        try
        {
            await func.Invoke();
        }
        // 重复发货订单消息，不打印 error 日志，也不用抛出异常
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            _logger.LogWarning(e,
                "{PlayerId} handled duplicated PayEventHandler {OrderType} {Content}",
                ev.PlayerId, orderType, JsonSerializer.Serialize(ev));
        }
        // 重复退款订单消息，不打印 error 日志，也不用抛出异常
        catch (PayException e) when (e.ErrorCode == PayErrorCode.OrderAlreadyRevoked)
        {
            _logger.LogWarning(e,
                "{PlayerId} handled duplicated PayEventHandler {OrderType} {Content}",
                ev.PlayerId, orderType, JsonSerializer.Serialize(ev));
        }
        // 发送退款通知邮件失败，不打印 error 日志，也不用抛出异常
        catch (PayException e) when (e.ErrorCode == PayErrorCode.ErrorSendRefundNotifyMail)
        {
            _logger.LogWarning(e,
                "{PlayerId} handled PayEventHandler but failed send notify mail {OrderType} {Content}",
                ev.PlayerId, orderType, JsonSerializer.Serialize(ev));
        }
    }

    // 即使重试也无法成功的异常返回 true
    protected override bool IsUnprocessableException(Exception e)
    {
        return e is PayException { ErrorCode: PayErrorCode.PayloadNull or PayErrorCode.PropIdNotValidInt };
    }
}

public class PayEventMessage
{
    public required List<long> OrderIds { get; set; }
    public required TakeRewardResult? Result { get; set; }
}
