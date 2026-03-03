using GameOutside.Repositories;

namespace GameOutside.Services;

public class SeasonService(ISeasonInfoRepository seasonInfoRepository)
{
    public const int BronzeSeasonNumber = -1;

    /// <summary>
    /// 获取当前赛季号，赛季号从0开始
    /// </summary>
    public int GetCurrentSeasonNumber()
    {
        return seasonInfoRepository.GetCurrentSeasonNumber();
    }

    /// <summary>
    /// 根据段位获取当前赛季号，青铜段位比较特殊，不应跟随赛季计算公式来返回
    /// </summary>
    /// <param name="division"></param>
    /// <returns></returns>
    public int GetCurrentSeasonNumberByDivision(int division)
    {
        if (division == 0)
            return BronzeSeasonNumber;
        return seasonInfoRepository.GetCurrentSeasonNumber();
    }

    /// <summary>
    /// 获取禁止清理的赛季号数组，青铜段位的特殊赛季、当前赛季、当前赛季-1、当前赛季-2、刷新赛季、刷新赛季-1、刷新赛季-2 的赛季号是需要保留的
    /// </summary>
    public HashSet<int> GetSeasonNumbersToBeKept(int seasonToBeRefreshed)
    {
        var currentSeason = GetCurrentSeasonNumber();
        return
        [
            .. new HashSet<int>
            {
                currentSeason,
                currentSeason - 1,
                currentSeason - 2,
                seasonToBeRefreshed,
                seasonToBeRefreshed - 1,
                seasonToBeRefreshed - 2
            }.Where(season => season >= 0),
            BronzeSeasonNumber,
        ];
    }
}
