using System.Diagnostics;
using System.Text.Json;
using ChillyRoom.MailService;
using ChillyRoom.MailService.v1;
using Microsoft.Extensions.Options;
using ChillyRoom.Infra.LiveMessage;
using Confluent.Kafka;
using GameOutside.Controllers;
using GameOutside.Services.PlatformItemsService;
using GameOutside.Util;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ChillyRoom.Infra.PlatformDef.Config;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;

namespace GameOutside.Services.KafkaConsumers;

public class AcceptAttachmentsHandler(ILogger<AcceptAttachmentsHandler> logger,
    IOptionsMonitor<PlatformKafkaConfig> kafkaConfigMonitor,
    ILiveMessageController liveMessageController,
    IServiceScopeFactory serviceScopeFactory) : AcceptAttachmentsHandlerBaseV2(kafkaConfigMonitor, logger)
{
    private const string _notificationTopic = "building-game-mailbox-attachments";

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await liveMessageController.SetupBroadcastChannel(ChannelPattern.UserTopic,
                "building-game/mailbox-attachments/{uid}", _notificationTopic,
                ChannelAuthorizationMode.GrantToUser, null, null);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            logger.LogError("无法注册消息网关，通知可能无法发送");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task HandleAttachments(ConsumeResult<long, string> cr, AcceptAttachmentsMsg msg,
        List<Attachment> attachments, CancellationToken stoppingToken)
    {
        await HandleMsgWrapAsync(async () =>
        {
            var rewardList = ParseAttachment(attachments);
            var payload = JsonSerializer.Serialize(rewardList);

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var platformItemsService = scope.ServiceProvider.GetRequiredService<PlatformItemsService.PlatformItemsService>();

            var result = await platformItemsService.ClaimPlatformNotifyAttachmentsAsync(rewardList,
                new PlatformNotifyAttachmentsMetadata(msg.PlayerId, msg.ShardId, NotifyType.Mail, msg.MailBoxId));
            var cacheValue = new ClaimAttachmentMessage(msg.MailBoxId, result);
            await platformItemsService.SaveMailItemsCacheAsync(msg.PlayerId, JsonSerializer.Serialize(cacheValue));

            // 发送到账通知到客户端，若客户端不在线则丢弃消息
            _ = Task.Run(async () =>
            {
                try
                {
                    await liveMessageController.SendJsonMessage(_notificationTopic, msg.MailBoxId, userId: msg.UserId,
                        messageGroup: msg.UserId.ToString());
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "玩家 {UserId}-{PlayerId} 邮件 {MailBoxId} 附件领取通知发送失败",
                        msg.UserId, msg.PlayerId, msg.MailBoxId);
                }
            });
        }, msg, cr.Message.Value);
    }

    private List<RewardItemData> ParseAttachment(List<Attachment> attachments)
    {
        var rewardList = new List<RewardItemData>();
        foreach (var attachment in attachments)
        {
            try
            {
                rewardList.Add(new RewardItemData
                {
                    Id = int.Parse(attachment.Id),
                    Count = (int)attachment.Count,
                });
            }
            catch (Exception e)
            {
                throw new MailAcceptAttachmentsException(MailAcceptAttachmentsErrorCode.ErrorParseAttachments,
                    $"附件 {attachment.Id} 解析 extra 失败，extra 内容：{attachment.Extra}", e);
            }
        }

        return rewardList;
    }

    private async Task HandleMsgWrapAsync(Func<Task> func, AcceptAttachmentsMsg msg, string rawMsg)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await func.Invoke();
            sw.Stop();
            logger.LogInformation(
                "{PlayerId} finish handle AcceptAttachmentsHandler {Content} in {Elapsed} ms",
                msg.PlayerId, rawMsg, sw.ElapsedMilliseconds);
        }
        // 重复领取附件消息，不打印 error 日志，也不用抛出异常
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            sw.Stop();
            logger.LogWarning(e,
                "{PlayerId} finish handle duplicated AcceptAttachmentsHandler {Content} in {Elapsed} ms",
                msg.PlayerId, rawMsg, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogError(e,
                "{PlayerId} error handle AcceptAttachmentsHandler {Content} in {Elapsed} ms",
                msg.PlayerId, rawMsg, sw.ElapsedMilliseconds);
            throw;
        }
    }

    protected override bool IsUnprocessableException(Exception e)
    {
        return e is ArgumentException or MailAcceptAttachmentsException
        {
            ErrorCode: MailAcceptAttachmentsErrorCode.ErrorParseAttachments
        };
    }
}

internal sealed class MailAcceptAttachmentsException(MailAcceptAttachmentsErrorCode errorCode, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public MailAcceptAttachmentsErrorCode ErrorCode { get; } = errorCode;
}

internal enum MailAcceptAttachmentsErrorCode
{
    None = 0,
    ErrorParseAttachments = 1,
}
