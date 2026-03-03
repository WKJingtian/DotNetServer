using System.Globalization;

public static class TimeUtils
{
    /// <summary>
    /// 获取两个时间戳之间的天数差值
    /// </summary>
    /// <param name="currentTime"> 当前时间戳 </param>
    /// <param name="baseTimeStamp"> 拿来比较的时间戳 </param>
    /// <param name="timeZoneOffset"> 用户时区 - UserAsset里 </param>
    /// <param name="timeOffset"> 传一下分割天数的时间，比如00:00传 0 </param>
    /// <returns></returns>
    public static int GetDayDiffBetween(long currentTime, long baseTimeStamp, long timeZoneOffset, long timeOffset)
    {
        var offset = timeZoneOffset - timeOffset;
        var baseDayCount = (baseTimeStamp + offset) / 86400;
        var currentDayCount = (currentTime + offset) / 86400;
        return (int) (currentDayCount - baseDayCount);
    }

    public static bool IfTwoEpochsInSameWeek(long timeA, long timeB, long timeZoneOffset)
    {
        long weekA = (timeA + 345600 + timeZoneOffset) / 604800;
        long weekB = (timeB + 345600 + timeZoneOffset) / 604800;
        return weekA == weekB;
    }
    public static bool IfTwoEpochsInSameMonth(long timeA, long timeB, long timeZoneOffset)
    {
        TimeSpan offset = TimeSpan.FromSeconds(timeZoneOffset);
        DateTime dateTime1 = DateTimeOffset.FromUnixTimeSeconds(timeA).UtcDateTime;
        DateTime localDate1 = dateTime1.Add(offset);
        DateTime dateTime2 = DateTimeOffset.FromUnixTimeSeconds(timeB).UtcDateTime;
        DateTime localDate2 = dateTime2.Add(offset);
        return localDate1.Year == localDate2.Year && localDate1.Month == localDate2.Month;
    }
    public static bool IfTwoEpochsInSameYear(long timeA, long timeB, long timeZoneOffset)
    {
        TimeSpan offset = TimeSpan.FromSeconds(timeZoneOffset);
        DateTime dateTime1 = DateTimeOffset.FromUnixTimeSeconds(timeA).UtcDateTime;
        DateTime localDate1 = dateTime1.Add(offset);
        DateTime dateTime2 = DateTimeOffset.FromUnixTimeSeconds(timeB).UtcDateTime;
        DateTime localDate2 = dateTime2.Add(offset);
        return localDate1.Year == localDate2.Year;
    }

    public static bool IfRecordTimeIsInRange(int refreshInterval, int spRefreshRule, long current, long record, long timezone)
    {
        if (spRefreshRule == 0)
        {
            if (refreshInterval == 0) return true;
            return GetDayDiffBetween(current, record, timezone, 0) < refreshInterval;
        }

        switch (spRefreshRule)
        {
            case 1:
                return IfTwoEpochsInSameWeek(current, record, timezone);
            case 2:
                return IfTwoEpochsInSameMonth(current, record, timezone);
            case 3:
                return IfTwoEpochsInSameYear(current, record, timezone);
        }

        return true;
    }
    
    public static long GetCurrentTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    // -12 ~ +14
    public static bool IsValidTimeZoneOffset(int timeZoneOffset)
    {
        return timeZoneOffset is >= -43200 and <= 50400;
    }
    
    public static long GetLocalMidnightEpoch(int timeZoneOffsetSeconds, int dayStartOffsetSeconds)
    {
        var offset = TimeSpan.FromSeconds(timeZoneOffsetSeconds);
        var nowUtc = DateTimeOffset.UtcNow;
        var localNow = nowUtc.ToOffset(offset);
        var localMidnight = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);

        var localDayStart = localMidnight.AddSeconds(dayStartOffsetSeconds);
        return new DateTimeOffset(localDayStart, offset).ToUnixTimeSeconds();
    }

    private static readonly string[] s_dateTimeFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss", "yyyy-MM-dd"];

    // 解析两种日期配置格式
    public static DateTime ParseDateTimeStr(string timeStr, bool isGmt8 = false)
    {
        var styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
        if (DateTime.TryParseExact(timeStr, s_dateTimeFormats, CultureInfo.InvariantCulture, styles, out var dt))
        {
            if (isGmt8)
            {
                // 解析到的 DateTime 视为 GMT+8 的本地时间；将其转换为 UTC
                var utc = dt - TimeSpan.FromHours(8);
                return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            }
            else
            {
                return dt;   
            }
        }

        throw new Exception("Could not parse timeStr");
    }

    public static long ParseDateTimeStrToUnixSecond(string timeStr)
    {
        return ((DateTimeOffset)ParseDateTimeStr(timeStr)).ToUnixTimeSeconds();;
    }

    /// <summary>
    /// 直接写死的方法，估计未来不会动, 等新版本H5活动就绪，旧客户端不再有人访问时可移除: todo
    /// </summary>
    /// <returns></returns>
    public static bool ShouldOldH5ActivityClose()
    {
        var closeTimeStamp = TimeUtils.ParseDateTimeStrToUnixSecond("2025-11-28 04:00:00");
        return GetCurrentTime() >= closeTimeStamp;
    }
}