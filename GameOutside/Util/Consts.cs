using System.Globalization;

namespace GameOutside.Util;

public static class Consts
{
    /// <summary>
    /// 新手战令通行证物品
    /// </summary>
    public const string FirstPassItemKey = "first_battle_pass";

    /// <summary>
    /// 赛季战令通行证物品
    /// </summary>
    public const string BattlePassItemKey = "season_battle_pass";

    /// <summary>
    /// 貔貅翁
    /// </summary>
    public const string PiggyBankItemKey = "190000";

    public const int DaySeconds = 86400;

    public const string MatchGameClientVersion = "0.9.0";

    public static readonly string Region = GetRequiredEnvironmentVariable("REGION");
    public static readonly string Namespace = GetRequiredEnvironmentVariable("NAMESPACE");

    public static readonly short[] ShardIds = Region == "cn-sz" ? [1051] : [1000, 2000, 3000, 5000];

    public static readonly bool IsConsumer = Environment.GetEnvironmentVariable("IS_CONSUMER") == "true";

    public static readonly short LocalShardId = GetRequiredInt16EnvironmentVariable("LOCAL_SHARD_ID");

    private static string GetRequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Environment variable '{name}' is required but was not found or is empty.");
        }

        return value;
    }

    private static short GetRequiredInt16EnvironmentVariable(string name)
    {
        var value = GetRequiredEnvironmentVariable(name);
        if (!short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException($"Environment variable '{name}' must be a valid Int16 value, but the current value is '{value}'.");
        }

        return result;
    }

    private static int GetRequiredInt32EnvironmentVariable(string name)
    {
        var value = GetRequiredEnvironmentVariable(name);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException($"Environment variable '{name}' must be a valid Int32 value, but the current value is '{value}'.");
        }

        return result;
    }
    
    public const int MaxCustomUserIntDataLength = 1023;
    public const int MaxCustomUserStringDataLength = 2047;
}
