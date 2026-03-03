using ChillyRoom.Infra.ApiController;
using GameOutside.Services.PlatformItemsService;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

public record struct ClaimAttachmentMessage(Guid Id, TakeRewardResult Result);

[Authorize]
public class MailBoxController(
    IConfiguration configuration,
    ILogger<MailBoxController> logger,
    PlatformItemsService platformItemsCacheService) : BaseApiController(configuration)
{
    /// <summary>
    /// 获得缓存中的邮箱附件
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClaimAttachmentMessage[]>> ClaimAttachments()
    {
        try
        {
            var rewards = await platformItemsCacheService.ClaimMailItemsCacheAsync(PlayerId);
            return Ok(rewards);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ClaimAttachments error");
            return BadRequest(PlatformItemsService.ClaimCacheErrorResponse);
        }
    }
}