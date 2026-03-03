using ChillyRoom.Infra.ApiController;
using GameOutside.Services.KafkaConsumers;
using GameOutside.Services.PlatformItemsService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class PayController(
    IConfiguration configuration,
    ILogger<PayController> logger,
    PlatformItemsService platformItemsService) : BaseApiController(configuration)
{
    /// <summary>
    /// 轮询 PaidOrders 表中未通知客户端已发货的订单，发货后更新状态为已通知
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PayEventMessage>> ClaimAttachments()
    {
        try
        {
            var payEventMessage = await platformItemsService.ClaimPaidOrderAttachmentsAsync(PlayerShard, PlayerId, GameVersion);
            return Ok(payEventMessage);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ClaimAttachments error");
            return BadRequest(PlatformItemsService.ClaimCacheErrorResponse);
        }
    }
}