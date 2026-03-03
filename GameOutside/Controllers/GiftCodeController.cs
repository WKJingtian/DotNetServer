using ChillyRoom.Infra.ApiController;
using GameOutside.Services.PlatformItemsService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class GiftCodeController(
    IConfiguration configuration,
    ILogger<GiftCodeController> logger,
    PlatformItemsService platformItemsCacheService)
    : BaseApiController(configuration)
{
    /// <summary>
    /// 获得缓存中的礼包码附件
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClaimAttachmentMessage[]>> ClaimAttachments()
    {
        try
        {
            var rewards = await platformItemsCacheService.ClaimGiftCodeItemsCacheAsync(PlayerId);
            return Ok(rewards);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ClaimAttachments error");
            return BadRequest(PlatformItemsService.ClaimCacheErrorResponse);
        }
    }
}
