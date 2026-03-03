using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using GameOutside.DBContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class DeviceCollectionController : BaseApiController
{
    private readonly BuildingGameDB _context;
    private readonly ILogger<DeviceCollectionController> _logger;
    private readonly ServerConfigService _serverConfigService;

    public DeviceCollectionController(
        IConfiguration configuration,
        ILogger<DeviceCollectionController> logger,
        ServerConfigService serverConfigService,
        BuildingGameDB context) : base(configuration)
    {
        _context = context;
        _logger = logger;
        _serverConfigService = serverConfigService;
    }

    [HttpPost]
    public async Task<ActionResult<int>> GetDeviceScore(string socName, string gpuName)
    {
        socName = socName.ToLower();
        gpuName = gpuName.ToLower();
        var score = _serverConfigService.GetDeviceScoreByName(socName);
        if (score == -1)
            score = _serverConfigService.GetDeviceScoreByName(gpuName);
        
        if (score == -1)
        {
            await _context.WithRCUDefaultRetry(async _ =>
            {
                await _context.SaveDeviceName(socName + "---" + gpuName);
                // 单表插入
                await _context.SaveChangesWithDefaultRetryAsync();
                return ValueTask.FromResult(true);
            });
        }

        return Ok(score);
    }
}