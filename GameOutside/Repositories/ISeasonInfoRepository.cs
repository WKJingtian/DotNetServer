using GameOutside.Util;

namespace GameOutside.Repositories;

public interface ISeasonInfoRepository
{
    /// <summary>
    /// 获取当前赛季号
    /// </summary>
    /// <returns>当前赛季号</returns>
    int GetCurrentSeasonNumber();
}

public class SeasonInfoRepository : ISeasonInfoRepository
{
    // 刷新时间为北京时间下午7点
    private static readonly DateTime _startSeason = new(2025, 7, 2, 11, 0, 0, DateTimeKind.Utc);
    public int GetCurrentSeasonNumber()
    {
        var now = DateTime.UtcNow;

        var secondsSinceStart = (int)(now - _startSeason).TotalSeconds;
        return secondsSinceStart / (2 * 24 * 60 * 60); // 每两天一个赛季
    }
}
