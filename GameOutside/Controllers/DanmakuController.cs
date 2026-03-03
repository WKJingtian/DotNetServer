using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using GameOutside.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using StackExchange.Redis;

namespace GameOutside.Controllers;

[Authorize]
public class DanmakuController : BaseApiController
{
    private readonly ILogger<DanmakuController> _logger;
    private readonly DanmakuService _danmakuService;
    private readonly UserAssetService _userAssetService;

    public DanmakuController(ILogger<DanmakuController> logger,
        IDiagnosticContext diagnosticContext,
        IConfiguration configuration,
        IConnectionMultiplexer connection,
        DanmakuService danmakuService,
        UserAssetService userAssetService) : base(configuration)
    {
        _logger = logger;
        _danmakuService = danmakuService;
        _userAssetService = userAssetService;
    }

    public class GetDanmakuRequest
    {
        [StringLength(256)]
        [JsonPropertyName("character")]
        public string CharacterId { get; set; }
    }

    public class GetDanmakuResponse
    {
        [JsonPropertyName("danmaku")]
        public List<string> DanmakuId { get; set; }
    }

    public class UpdateDanmakuRequest
    {
        [StringLength(256)]
        [JsonPropertyName("character")]
        public string CharacterId { get; set; }

        [StringLength(1024 * 1024)]
        [JsonPropertyName("danmaku")]
        public string DanmakuId { get; set; }
    }

    public class UpdateDanmakuResponse
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("updateTime")]
        public DateTime UpdateTime { get; set; }

    }

    [HttpPost]
    public async Task<ActionResult<GetDanmakuResponse>> GetDanmaku(GetDanmakuRequest args)
    {
        var danmakuList = await _danmakuService.GetDanmaku(args.CharacterId);

        return Ok(new GetDanmakuResponse
        {
            DanmakuId = danmakuList.Select(x => x.ToString()).ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<UpdateDanmakuResponse>> UpdateDanmaku(UpdateDanmakuRequest args)
    {
        try
        {
            var timeZoneOffset = await _userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
            if (timeZoneOffset == null)
                return BadRequest(new ErrorResponse() {ErrorCode = (int)ErrorKind.NO_USER_RECORDS});

            await _danmakuService.UpdateDanmaku(args.CharacterId, args.DanmakuId, timeZoneOffset.Value, DistroId);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "更新弹幕时发生错误: PlayerId={PlayerId}, args={@args}", this.PlayerId, args);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = (int)CommonErrorCodes.INTERNAL_ERROR,
                Message = "更新弹幕失败"
            });
        }

        return Ok(new UpdateDanmakuResponse
        {
            Value = args.DanmakuId,
            UpdateTime = DateTime.UtcNow,
        });
    }
}
