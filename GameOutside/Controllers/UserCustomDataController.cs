using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameOutside.Util;

namespace GameOutside.Controllers;

[Authorize]
public class UserCustomDataController : BaseApiController
{
    private readonly BuildingGameDB _context;
    private readonly ILogger<UserCustomDataController> _logger;

    public UserCustomDataController(
        IConfiguration configuration,
        ILogger<UserCustomDataController> logger,
        BuildingGameDB context) : base(configuration)
    {
        _context = context;
        _logger = logger;
    }

    // TODO: 危险，参数无任何校验
    [HttpPost]
    public async Task<ActionResult<int>> SetUserIntData(string userData)
    {
        if (userData.Length > Consts.MaxCustomUserIntDataLength)
            return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.CUSTOM_DATA_TOO_LONG});
        return await _context.WithRCUDefaultRetry<ActionResult<int>>(async _ =>
        {
            await _context.SetUserIntData(PlayerShard, PlayerId, userData);

            await _context.SaveChangesWithDefaultRetryAsync();
            return Ok(0);
        });
    }

    // TODO: 危险，参数无任何校验
    [HttpPost]
    public async Task<ActionResult<int>> SetUserStringData(string userData)
    {
        if (userData.Length > Consts.MaxCustomUserStringDataLength)
            return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.CUSTOM_DATA_TOO_LONG});
        return await _context.WithRCUDefaultRetry<ActionResult<int>>(async _ =>
        {
            await _context.SetUserStrData(PlayerShard, PlayerId, userData);
            await _context.SaveChangesWithDefaultRetryAsync();
            return Ok(0);
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserCustomData>> FetchUserData()
    {
        var userData = await _context.GetUserDataAsync(PlayerShard, PlayerId);
        if (userData == null)
            return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.NO_USER_RECORDS});
        return Ok(userData);
    }
}