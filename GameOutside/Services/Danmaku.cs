using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChillyRoom.Infra.CensorService.v1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;


// #号前：（1～99）#
// 表情：Emoji_(0~10)
// 游戏固定文本：（0～10）
// 自定义文本：Custom_xxx
public class DanmakuConfig
{
    // 每次获取弹幕的最大数量
    public int MaxDanmakuPerFetch { get; set; } = 50;
    // 场景ID范围
    public (int min, int max) DanmakuSceneId { get; set; } = (0, 99);
    // 表情ID范围
    public (int min, int max) DanmakuEmojiId { get; set; } = (0, 100);
    // 游戏固定文本ID范围
    public (int min, int max) DanmakuFixedTextId { get; set; } = (0, 100);

    // 不允许使用自定义弹幕的发行通道
    public List<Guid> DisallowedCustomDanmakuDistros { get; set; } = new List<Guid>();
}

public class DanmakuService(
    IConnectionMultiplexer connection,
    IOptionsMonitor<DanmakuConfig> gameConfig,
    CensorAPI.CensorAPIClient censorClient,
    ILogger<DanmakuService> logger)
{
    // 单位：秒
    private const int _shangHaiTimeZoneSeconds = 28800;
    
    // Danmaku service methods would be here
    public async Task<List<string>> GetDanmaku(string characterId)
    {
        // Implementation for getting danmaku
        var redisKey = $"danmaku:{characterId}";
        var db = connection.GetDatabase();
        var danmakuList = await db.ListRangeAsync(redisKey, 0, 50);

        return danmakuList.Select(x => x.ToString()).ToList();
    }

    public async Task UpdateDanmaku(string characterId, string danmaku, int dateTimeOffset, Guid distroId)
    {
        var redisKey = $"danmaku:{characterId}";
        var db = connection.GetDatabase();

        // 弹幕可能存在 xx#xxx 的情况，取第一个 # 之前的内容进行校验是否在 1-99，再校验 # 之后的内容是否是 emojiDanmak 或 fixedDanmaku 或 customDanmaku
        // 目前墨守只用到了固定文本，这段代码是支持的，先不动了

        var trimmed = danmaku.Trim();
        var hashIndex = trimmed.IndexOf('#');

        string updateInput;

        async Task<string> ValidateDanmakuSuffixAsync(string suffix, string original)
        {
            return suffix switch
            {
                var emojiDanmakuId when emojiDanmakuId.StartsWith("Emoji_") && int.TryParse(emojiDanmakuId["Emoji_".Length..], out var emojiId) &&
                    emojiId >= gameConfig.CurrentValue.DanmakuEmojiId.min &&
                    emojiId <= gameConfig.CurrentValue.DanmakuEmojiId.max =>
                    original, // 处理表情
                var fixedDanmakuId when int.TryParse(fixedDanmakuId, out var fixedTextId) &&
                    fixedTextId >= gameConfig.CurrentValue.DanmakuFixedTextId.min &&
                    fixedTextId <= gameConfig.CurrentValue.DanmakuFixedTextId.max =>
                    original, // 处理游戏固定文本
                var customDanmaku when customDanmaku.StartsWith("Custom_") => await sanitizeCustomDanmaku(original), // 处理自定义文本
                _ => throw new ArgumentOutOfRangeException(nameof(danmaku), "Invalid danmaku format.")
            };
        }

        if (hashIndex >= 0)
        {
            var head = trimmed[..hashIndex];
            var tail = trimmed[(hashIndex + 1)..];

            if (!int.TryParse(head, out var sceneId) ||
                sceneId < gameConfig.CurrentValue.DanmakuSceneId.min ||
                sceneId > gameConfig.CurrentValue.DanmakuSceneId.max)
            {
                throw new ArgumentOutOfRangeException(nameof(danmaku), "Invalid danmaku format.");
            }

            if (string.IsNullOrWhiteSpace(tail))
            {
                throw new ArgumentOutOfRangeException(nameof(danmaku), "Invalid danmaku format.");
            }

            updateInput = await ValidateDanmakuSuffixAsync(tail, trimmed);
        }
        else
        {
            updateInput = await ValidateDanmakuSuffixAsync(trimmed, trimmed);
        }

        await db.ListLeftPushAsync(redisKey, updateInput);

        return;

        async Task<string> sanitizeCustomDanmaku(string danmaku)
        {
            if (dateTimeOffset != _shangHaiTimeZoneSeconds || gameConfig.CurrentValue.DisallowedCustomDanmakuDistros.Contains(distroId))
            {
                throw new ArgumentOutOfRangeException(nameof(danmaku), "Custom danmaku is not allowed for this distribution channel or timezone.");
            }

            var censorResponse = await censorClient.CensorTextAsync(new CensorTextRequest
            {
                Text = danmaku
            });

            if (censorResponse.Result != TextValidationResult.Pass)
            {
                // 根据返回结果抛出异常
                logger.LogWarning("Danmaku {CustomDanmaku} 审核未通过，结果：{Result}", danmaku, censorResponse.Result);
                throw new ArgumentOutOfRangeException(nameof(danmaku), "Danmaku contains inappropriate content.");
            }

            return danmaku;
        }
    }
}