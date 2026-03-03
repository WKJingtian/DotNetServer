using ChillyRoom.Infra.ApiController;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

public class ClientVersionController(IConfiguration configuration) : BaseApiController(configuration)
{
    [HttpPost]
    public Task<ActionResult<long>> GetTimeStamp()
    {
        return Task.FromResult<ActionResult<long>>(Ok(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    [HttpPost]
    public Task<ActionResult<long>> GetTimeStampMilliseconds()
    {
        return Task.FromResult<ActionResult<long>>(Ok(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

}