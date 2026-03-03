using System.Text.Json;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.MailService;
using ChillyRoom.MailService.v1;
using GameExternal;
using GameOutside;
using Microsoft.AspNetCore.Mvc;

[ApiExplorerSettings(IgnoreApi = true)]
public class WeeklyMailController(
    IConfiguration configuration,
    ILogger<WeeklyMailController> logger,
    ServerConfigService serverConfigService,
    MailSender mailSender) : BaseApiController(configuration)
{
    
    [HttpGet]
    public async Task<ActionResult<int>> SendWeeklyMail()
    {
        // 找到合适的配置
        var weeklyMailConfig = FindCurrentTimeFirstMatchConfig();
        if (weeklyMailConfig == null)
        {
            // 没找到当天的配置，也无问题，可以直接返回
            logger.LogInformation("no corresponding weekly mail config found for {Now}", DateTimeOffset.Now);
            return Ok(-1);
        }
        
        // 填充附件内容
        var attachments = new List<AttachmentWithStringCount>();
        for (int i = 0; i < weeklyMailConfig.item_list.Length; i++)
        {
            int itemId = weeklyMailConfig.item_list[i];
            int itemCount = weeklyMailConfig.count_list[i];
            var itemConfig = serverConfigService.GetItemConfigById(itemId);
            if (itemConfig == null)
            {
                logger.LogError("item config not found for item: {ItemId}", itemId);
                return BadRequest(ErrorKind.NO_ITEM_CONFIG);
            }

            attachments.Add(new()
            {
                Id = itemId.ToString(),              // 必填
                Count = itemCount.ToString(),        // 必填 
                MinVersion = itemConfig.min_version, // 必填
                Extra = "{}",                        // 可选
                Properties = { }                     // 可选
            });
        }

        var templateVarValues = new Dictionary<string, string> {{"attachments", JsonSerializer.Serialize(attachments)}};
        var sendTime = TimeUtils.ParseDateTimeStr(weeklyMailConfig.send_time, true);
        var expiredTime = TimeUtils.ParseDateTimeStr(weeklyMailConfig.expired_time, true);
        try
        {
            // 发送邮件
            await mailSender.SendMailWithWildcardLocalizedVariablesAsync(
                new SendMailWithWildcardLocalizedVariablesRequest(
                    TemplateId: weeklyMailConfig.template_id,             // 模板Id
                    FilterBuilder: null,                                  // null表示给全服发邮件                                   
                    ExpiredAt: expiredTime,                               // 过期时间
                    SendTime: sendTime,                                   // 填 null 立刻发送，如需定时发送，填写要定时发送的时间
                    VariableValues: templateVarValues)                    // 模板变量的值
            );
        }
        catch (Exception e)
        {
            logger.LogError("Send Weekly Mail Id {Id} Error: {EMessage}", weeklyMailConfig.id, e.Message);
            throw;
        }
        
        logger.LogInformation("[{Now}] Weekly Mail Sent, Id: {Id}", DateTimeOffset.Now, weeklyMailConfig.id);
        return Ok(1);
    }

    /// <summary>
    /// 找到第一个匹配的配置，目前按照当前日期匹配
    /// </summary>
    /// <returns></returns>
    private WeeklyMailConfig? FindCurrentTimeFirstMatchConfig()
    {
        var currentGmt8Time = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        var configList = serverConfigService.GetWeeklyMailConfigList();
        // 找到匹配的配置
        foreach (var config in configList)
        {
            // gmt+8时间
            DateTime sendTime = TimeUtils.ParseDateTimeStr(config.send_time, true);
            // 匹配日期，不需要精准匹配时间
            if (currentGmt8Time.Year == sendTime.Year && currentGmt8Time.Month == sendTime.Month &&
                currentGmt8Time.Day == sendTime.Day)
            {
                return config;
            }
        }

        return null;
    }
};