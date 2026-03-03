using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface ISeasonRefreshedHistoryRepository
{
    /// <summary>
    /// 添加赛季刷新历史记录
    /// </summary>
    void AddHistory(SeasonRefreshedHistory history);

    /// <summary>
    /// 判断上赛季是否还在结算中，如果无法查到当前赛季的刷新记录，则说明还未刷新完成
    /// </summary>
    /// <returns></returns>
    Task<bool> IsLastSeasonRefreshingAsync();
}

public class SeasonRefreshedHistoryRepository(
    BuildingGameDB dbCtx,
    ISeasonInfoRepository seasonInfoRepository) : ISeasonRefreshedHistoryRepository
{
    public void AddHistory(SeasonRefreshedHistory history)
    {
        dbCtx.SeasonRefreshedHistories.Add(history);
    }

    public async Task<bool> IsLastSeasonRefreshingAsync()
    {
        // 获取上赛季号
        var lastSeason = seasonInfoRepository.GetCurrentSeasonNumber() - 1;
        if (lastSeason < 0)
            return false;
        // 如果无法查到上赛季的刷新记录，则说明还未刷新完成
        return !await dbCtx.WithDefaultRetry(_ => dbCtx.SeasonRefreshedHistories.AllowStaleRead().AnyAsync(x => x.SeasonNumber == lastSeason));
    }
}
