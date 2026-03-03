using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

public class InviteFriendRequest
{
    public long UserId { get; set; }
    public string RoomId { get; set; }
    public int InviteGameType { get; set; }
    public int ActivityId { get; set; }
    public int IntArg0 { get; set; }
}

[Authorize]
public class GameMatchController(
    IConfiguration configuration,
    MessageService messageService,
    FriendModule friendModule)
    : BaseApiController(configuration)
{
    [HttpPost]
    public async Task<ActionResult<bool>> InviteFriendIntoRoom(InviteFriendRequest request)
    {
        // 好友关系才可邀请
        if (!await friendModule.CheckIsFriend(UserId, request.UserId))
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.INVALID_CONTACT });
        // 通过长连接发送消息给被邀请者
        messageService.InviteFriendToRoom(request, UserId);
        return Ok(true);
    }
}
