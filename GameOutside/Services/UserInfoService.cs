using System.Runtime.CompilerServices;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Games.BuildingGame.Services;
using ChillyRoom.Infra.CensorService.v1;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Util;

namespace GameOutside.Services;

public class UserInfoService(
    CacheManager cacheManager,
    BuildingGameDB dbCtx,
    IUserInfoRepository userInfoRepository,
    ServerConfigService serverConfigService,
    ILogger<UserInfoService> logger,
    CensorAPI.CensorAPIClient censor)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> IsUserInfoExistsAsync(short? shardId, long playerId)
    {
        return dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.IsUserInfoExistsAsync(shardId, playerId));
    }

    /// <summary>
    /// 创建新的用户信息
    /// </summary>
    /// <returns>被跟踪的 UserInfo 实体</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UserInfo CreateNewUserInfo(UserInfo userInfo)
    {
        return userInfoRepository.CreateNewUserInfo(userInfo);
    }

    /// <summary>
    /// 不脏读，不 include histories，不跟踪
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<UserInfo?> GetUserInfoAsync(short shardId, long playerId)
    {
        return await dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.GetUserInfoAsync(shardId, playerId,
                TrackingOptions.NoTracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.NoInclude));
    }

    /// <summary>
    /// 较久的脏读，include histories，不跟踪
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<UserInfo?> GetUserInfoWithHistoriesAsync(short? shardId, long playerId, string gameVersion)
    {
        shardId ??= await cacheManager.GetPlayerGameDataShard(playerId);
        var userInfo = await dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.GetUserInfoAsync(shardId, playerId,
                TrackingOptions.NoTracking, Repositories.StaleReadOptions.Allow15sStaleRead, UserInfoIncludeOptions.IncludeHistories));
        // 需要判断下版本，不然如果旧版本越界会报错
        if (userInfo != null && gameVersion.CompareVersionStrServer("1.1.0") < 0)
        {
            foreach (var history in userInfo.Histories)
            {
                history.Score = Math.Min(history.Score, int.MaxValue);
                if (history.Score < 0)
                    history.Score = Math.Max(int.MinValue, history.Score);
            }
        }
        return userInfo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<int?> GetAvatarFrameItemIDAsync(short? shardId, long playerId)
    {
        shardId ??= await cacheManager.GetPlayerGameDataShard(playerId);
        return await dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.GetAvatarFrameItemIDAsync(shardId, playerId));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<int?> GetNameCardItemIDAsync(short? shardId, long playerId)
    {
        shardId ??= await cacheManager.GetPlayerGameDataShard(playerId);
        return await dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.GetNameCardItemIDAsync(shardId, playerId));
    }

    public async ValueTask<ErrorKind> UpdateAvatarFrameItemIDAsync(short shardId, long playerId, int frameItemId)
    {
        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var userInfo = await dbCtx.WithDefaultRetry(_ =>
                userInfoRepository.GetUserInfoAsync(shardId, playerId,
                    TrackingOptions.Tracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.NoInclude));
            if (userInfo is null)
            {
                return ErrorKind.NO_USER_RECORDS;
            }

            if (userInfo.AvatarFrameItemID == frameItemId)
            {
                return ErrorKind.SUCCESS;
            }

            userInfo.AvatarFrameItemID = frameItemId;
            await dbCtx.SaveChangesWithDefaultRetryAsync();
            return ErrorKind.SUCCESS;
        });
    }

    /// <summary>
    /// 函数内部已SaveChange 
    /// </summary>
    public async ValueTask<ErrorKind> UpdateNameCardItemIDAsync(short shardId, long playerId, int nameCardItemId)
    {
        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var userInfo = await dbCtx.WithDefaultRetry(_ =>
                userInfoRepository.GetUserInfoAsync(shardId, playerId,
                    TrackingOptions.Tracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.NoInclude));
            if (userInfo is null)
            {
                return ErrorKind.NO_USER_RECORDS;
            }

            if (userInfo.NameCardItemID == nameCardItemId)
            {
                return ErrorKind.SUCCESS;
            }

            userInfo.NameCardItemID = nameCardItemId;
            await dbCtx.SaveChangesWithDefaultRetryAsync();
            return ErrorKind.SUCCESS;
        });
    }

    public async ValueTask<ErrorKind> UpdateHideHistoryAsync(short shardId, long playerId, bool hideHistory)
    {
        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var userInfo = await dbCtx.WithDefaultRetry(_ =>
                userInfoRepository.GetUserInfoAsync(shardId, playerId,
                    TrackingOptions.Tracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.NoInclude));
            if (userInfo is null)
            {
                return ErrorKind.NO_USER_RECORDS;
            }

            if (userInfo.HideHistory == hideHistory)
            {
                return ErrorKind.SUCCESS;
            }

            userInfo.HideHistory = hideHistory;

            await dbCtx.SaveChangesWithDefaultRetryAsync();
            return ErrorKind.SUCCESS;
        });
    }

    /// <summary>
    /// 更新用户签名
    /// </summary>
    /// <returns>是否包含敏感词，屏蔽后的签名，是否超出字数限制，错误码</returns>
    public async ValueTask<(bool, string, bool, ErrorKind)> UpdateSignatureAsync(short shardId, long playerId, string signature)
    {
        // 检查字数超出限制
        var (exceedLengthLimit, maskedSignature) = LimitStringWithinLength(signature, 100);
        // 检查屏蔽字
        var censored = await censor.CensorTextAsync(new()
        {
            Text = maskedSignature,
            UsageHint = TextUsageHint.Chat
        });
        if (censored.Result != TextValidationResult.ContainsSensitivePhrase &&
            censored.Result != TextValidationResult.Pass)
        {
            logger.LogError("CensorAPI 返回异常结果: {Result}", censored.Result);
            return (false, string.Empty, false, ErrorKind.COMMON_INTERNAL_CENSOR_ERROR);
        }

        var isSensitive = censored.Result == TextValidationResult.ContainsSensitivePhrase;
        maskedSignature = censored.Result == TextValidationResult.ContainsSensitivePhrase ? censored.MaskedText : maskedSignature;

        try
        {
            return await dbCtx.WithRCUDefaultRetry(async _ =>
            {
                var userInfo = await dbCtx.WithDefaultRetry(_ =>
                    userInfoRepository.GetUserInfoAsync(shardId, playerId,
                        TrackingOptions.Tracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.NoInclude));
                if (userInfo is null)
                {
                    return (false, string.Empty, false, ErrorKind.NO_USER_RECORDS);
                }

                if (userInfo.Signature == maskedSignature)
                {
                    return (isSensitive, maskedSignature, exceedLengthLimit, ErrorKind.SUCCESS);
                }

                userInfo.Signature = maskedSignature;
                await dbCtx.SaveChangesWithDefaultRetryAsync();
                return (isSensitive, maskedSignature, exceedLengthLimit, ErrorKind.SUCCESS);
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return (false, string.Empty, false, ErrorKind.COMMON_INTERNAL_SAVE_ERROR);
        }
    }

    private static (bool exceedLengthLimit, string result) LimitStringWithinLength(string targetStr, int targetLength)
    {
        if (string.IsNullOrEmpty(targetStr) || targetLength <= 0)
            return (false, targetStr);

        int maxLength = targetLength * 2;

        //字符串长度 * 2 <= 目标长度，即使是全中文也在长度范围内
        if (targetStr.Length * 2 <= maxLength)
            return (false, targetStr);

        //遍历字符
        char[] chars = targetStr.ToCharArray();
        int length = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            // 英文占一个，其他占两个
            if (chars[i] < 255)
                length += 1;
            else
                length += 2;

            if (length > maxLength)
                return (true, targetStr.Substring(0, i));
        }

        return (false, targetStr);
    }

    /// <summary>
    /// 最大的用户历史记录数量
    /// </summary>
    public const int MaxUserHistoryCount = 20;

    /// <summary>
    /// 记录游戏历史，需检查重复和容量限制
    /// </summary>
    public async ValueTask<ErrorKind> RecordGameHistoryAsync(short shardId, long playerId, long? gameStartTime, Func<UserHistory> createHistoryFunc)
    {
        var userInfo = await dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.GetUserInfoAsync(shardId, playerId,
                TrackingOptions.Tracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.IncludeHistories));

        if (userInfo == null)
            return ErrorKind.NO_USER_RECORDS;

        // 检查重复时间戳
        var histories = userInfo.Histories;
        if (gameStartTime.HasValue && histories.Any(h => h.GameStartTime == gameStartTime))
            return ErrorKind.DUPLICATE_GAME_END_MESSAGE;

        // 检查历史记录数量限制
        if (histories.Count >= MaxUserHistoryCount)
        {
            var farthest = histories.MinBy(h => h.Timestamp);
            if (farthest != null)
                histories.Remove(farthest);
        }

        // 添加新的历史记录
        histories.Add(createHistoryFunc());
        return (int)ErrorKind.SUCCESS;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<Dictionary<long, UserInfo>> GetUserInfosByPlayerIdsAsync(short shardId, IEnumerable<long> playerIds)
    {
        return dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.GetUserInfosByPlayerIdsAsync(shardId, playerIds,
                TrackingOptions.NoTracking, Repositories.StaleReadOptions.AllowStaleRead, UserInfoIncludeOptions.NoInclude));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<Dictionary<long, UserInfo>> BatchGetUserInfosByPlayerIdsAsync(IEnumerable<long> playerIds)
    {
        return dbCtx.WithDefaultRetry(_ =>
            userInfoRepository.BatchGetUserInfosByPlayerIdsAsync(playerIds,
                TrackingOptions.NoTracking, Repositories.StaleReadOptions.AllowStaleRead, UserInfoIncludeOptions.NoInclude));
    }

    /// <summary>
    /// 函数内部已SaveChange 
    /// </summary>
    public async ValueTask<ErrorKind> UpdateWorldRankAsync(long playerId, int worldRank, int seasonToBeRefreshed)
    {
        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var shardId = await cacheManager.GetPlayerGameDataShard(playerId);
            var userInfo = await dbCtx.WithDefaultRetry(_ =>
                userInfoRepository.GetUserInfoAsync(shardId, playerId,
                    TrackingOptions.Tracking, Repositories.StaleReadOptions.NoStaleRead, UserInfoIncludeOptions.NoInclude));
            if (userInfo is null)
            {
                return ErrorKind.NO_USER_RECORDS;
            }

            // 已经刷新过排名了，就不管了
            if (userInfo.WorldRankSeasonHistories.Contains(seasonToBeRefreshed))
                return ErrorKind.SUCCESS;

            userInfo.WorldRankHistories.Add(worldRank);
            userInfo.WorldRankSeasonHistories.Add(seasonToBeRefreshed);

            // 根据保留时间清理旧的数据
            if (!serverConfigService.TryGetParameterInt(Params.KeepWorldRankSeasonCount,
                    out var keepWorldRankSeasonCount))
            {
                return ErrorKind.NO_PARAM_CONFIG;
            }

            for (int i = userInfo.WorldRankSeasonHistories.Count - 1; i >= 0; i--)
            {
                var seasonNumber = userInfo.WorldRankSeasonHistories[i];
                if (seasonNumber + keepWorldRankSeasonCount <= seasonToBeRefreshed)
                {
                    userInfo.WorldRankHistories.RemoveAt(i);
                    userInfo.WorldRankSeasonHistories.RemoveAt(i);
                }
            }

            await dbCtx.SaveChangesWithDefaultRetryAsync();
            return ErrorKind.SUCCESS;
        });
    }
}