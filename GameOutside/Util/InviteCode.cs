using Sqids;
using System.Security.Cryptography;

public static class InviteCode
{
    private static readonly SqidsEncoder<long> _encoder = new SqidsEncoder<long>(new SqidsOptions
    {
        Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
        MinLength = 10
    });

    /// <summary>
    /// 校验邀请码与 PlayerId, DistroId 是否匹配
    /// </summary>
    /// <param name="code">邀请码</param>
    /// <param name="playerId">玩家ID</param>
    /// <param name="distroId">分发ID</param>
    /// <returns>是否匹配</returns>
    public static bool IsCodeMatch(string code, long playerId, Guid distroId)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var decoded = _encoder.Decode(code);
        if (decoded.Count < 2)
            return false;

        var parsedCode = ParseCode(code);
        if (!parsedCode.HasValue)
            return false;

        return parsedCode.Value.PlayerId == playerId
            && parsedCode.Value.DistroIdHash == ComputeCompactGuidHash(distroId);
    }

    /// <summary>
    /// 生成包含 playerId, distroId 的邀请码
    /// </summary>
    /// <param name="playerId">玩家ID</param>
    /// <param name="distroId">分发ID</param>
    /// <returns>编码后的邀请码</returns>
    public static string GenerateCode(long playerId, Guid distroId, int counter = 0)
    {
        // 使用哈希策略将GUID转换为单个哈希值
        var guidHash = ComputeCompactGuidHash(distroId);

        var unsignedPlayerId = Math.Abs(playerId);

        if (counter == 0)
        {
            return _encoder.Encode(unsignedPlayerId, guidHash);
        }

        // 编码三个数字：playerId, guidHash, counter
        return _encoder.Encode(unsignedPlayerId, guidHash, counter);
    }

    /// <summary>
    /// 解析邀请码，提取 playerId, distroId
    /// </summary>
    /// <param name="code">邀请码</param>
    /// <returns>解码后的数据，如果解码失败返回 null</returns>
    public static (long PlayerId, long DistroIdHash, int counter)? ParseCode(string code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var decoded = _encoder.Decode(code);

            // 验证解码结果应该包含2个数字
            if (decoded.Count < 2)
                return null;

            var playerId = decoded[0];
            var guidHash = decoded[1];
            var counter = decoded.Count > 2 ? (int)decoded[2] : 0;

            return (playerId, guidHash, counter);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 验证邀请码格式
    /// </summary>
    /// <param name="code">邀请码</param>
    /// <returns>是否有效</returns>
    public static bool IsValidInviteCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 10 || code.Length > 20)
            return false;

        return ParseCode(code).HasValue;
    }

    /// <summary>
    /// 从邀请码中提取玩家ID
    /// </summary>
    /// <param name="code">邀请码</param>
    /// <returns>玩家ID，如果解析失败返回null</returns>
    public static long? GetPlayerIdFromCode(string code)
    {
        var result = ParseCode(code);
        return result?.PlayerId;
    }

    /// <summary>
    /// 从邀请码中提取分发ID
    /// </summary>
    /// <param name="code">邀请码</param>
    /// <returns>分发ID，如果解析失败返回null</returns>
    public static long? GetDistroIdHashFromCode(string code)
    {
        var result = ParseCode(code);
        return result?.DistroIdHash;
    }

    /// <summary>
    /// 计算GUID的压缩哈希值（32位）以生成更短的邀请码
    /// </summary>
    /// <param name="guid">要哈希的GUID</param>
    /// <returns>32位哈希值</returns>
    public static long ComputeCompactGuidHash(Guid guid)
    {
        var guidBytes = guid.ToByteArray();
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(guidBytes);
            // 取哈希值的前4个字节作为32位整数，转换为long避免负数问题
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
