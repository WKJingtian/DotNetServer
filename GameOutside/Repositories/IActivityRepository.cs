﻿﻿using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IActivityRepository
{
    public Task<ActivityLuckyStar?> GetActivityLuckStarDataAsync(
        long playerId,
        short shardId,
        TrackingOptions trackingOptions);

    public ActivityLuckyStar AddActivityLuckyStarData(long playerId, short shardId, int activityId);

    public Task<UserFortuneBagInfo?> GetUserFortuneBagInfoAsync(
        long playerId,
        short shardId,
        TrackingOptions trackingOptions);

    public UserFortuneBagInfo AddUserFortuneBagInfo(long playerId, short shardId, int activityId);
    public Task<ActivityPiggyBank?> GetPiggyBankStatusAsync(long playerId, short? shardId);
    public ActivityPiggyBank CreateDefaultPiggyBank(long playerId, short shardId);

    public Task<ActivityUnrivaledGod?> GetUnrivaledGodDataAsync(
        long playerId,
        short shardId,
        int activityId,
        TrackingOptions trackingOptions);

    public ActivityUnrivaledGod CreateDefaultUnrivaledGodData(long playerId, short shardId, int activityId);

    public Task<List<ActivityUnrivaledGod>> GetAllUnrivaledGodDataAsync(
        long playerId,
        short shardId,
        TrackingOptions trackingOptions = TrackingOptions.NoTracking);

    public void ClearUnrivaledGodDataList(List<ActivityUnrivaledGod> unrivaledGodDataList);

    public Task<ActivityCoopBossInfo?> GetCoopBossDataAsync(
        long playerId,
        short shardId,
        int activityId,
        TrackingOptions trackingOptions);

    public ActivityCoopBossInfo CreateDefaultCoopBossData(long playerId, short shardId, int activityId);

    public Task<ActivityTreasureMaze?> GetTreasureMazeDataAsync(
        long playerId,
        short shardId,
        int activityId);

    public ActivityTreasureMaze CreateTreasureMazeData(long playerId, short shardId, int activityId, long startTimestamp);

    public Task<ActivityEndlessChallenge?> GetEndlessChallengeDataAsync(
        long playerId,
        short shardId,
        int activityId);

    public ActivityEndlessChallenge CreateDefaultEndlessChallengeData(long playerId, short shardId, int activityId);

    public Task<ActivityOneShotKill?> GetOneShotKillDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions);

    public ActivityOneShotKill CreateOneShotKillData(long playerId, short shardId, int activityId);

    public Task<ActivitySlotMachine?> GetSlotMachineDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions);

    public Task<List<ActivitySlotMachine>> GetSlotMachineListByPlayerAsync(long playerId, short shardId, TrackingOptions trackingOptions);

    public ActivitySlotMachine CreateDefaultSlotMachineData(long playerId, short shardId, int activityId);

    public Task<ActivityTreasureHunt?> GetTreasureHuntDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions);

    public Task<List<ActivityTreasureHunt>> GetTreasureHuntListByPlayerAsync(long playerId, short shardId, TrackingOptions trackingOptions);

    public ActivityTreasureHunt CreateDefaultTreasureHuntData(long playerId, short shardId, int activityId);

    public Task<ActivityRpgGame?> GetRpgGameDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions);
    
    public ActivityRpgGame CreateDefaultRpgGameData(long playerId, short shardId, int activityId);

    public Task<ActivityLoogGame?> GetLoogGameDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions);
    
    public ActivityLoogGame CreateDefaultLoogGameData(long playerId, short shardId, int activityId);

    public Task<ActivityCsgoStyleLottery?> GetCsgoStyleLotteryDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions);

    public ActivityCsgoStyleLottery CreateDefaultCsgoStyleLotteryData(long playerId, short shardId, int activityId);
    
    public Task<List<ActivityCsgoStyleLottery>> GetCsgoLotteryDataList(long playerId, short shardId, TrackingOptions trackingOptions);
}

public class ActivityRepository(BuildingGameDB dbCtx) : IActivityRepository
{
    public Task<ActivityLuckyStar?> GetActivityLuckStarDataAsync(
        long playerId,
        short shardId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityLuckyStars.Where(t => t.PlayerId == playerId && t.ShardId == shardId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public ActivityLuckyStar AddActivityLuckyStarData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultLuckyStarData(playerId, shardId, activityId);
        dbCtx.ActivityLuckyStars.Add(data);
        return data;
    }

    public Task<UserFortuneBagInfo?> GetUserFortuneBagInfoAsync(
        long playerId,
        short shardId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.UserFortuneBagInfos.Where(u => u.PlayerId == playerId && u.ShardId == shardId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public UserFortuneBagInfo AddUserFortuneBagInfo(long playerId, short shardId, int activityId)
    {
        var info = DataUtil.DefaultFortuneBagData(playerId, shardId, activityId);
        dbCtx.Entry(info).Property(t => t.FortuneBags).IsModified = true;
        dbCtx.UserFortuneBagInfos.Add(info);
        return info;
    }

    public Task<ActivityPiggyBank?> GetPiggyBankStatusAsync(long playerId, short? shardId)
    {
        return dbCtx.ActivityPiggyBanks.FirstOrDefaultAsync(u =>
            shardId.HasValue ? u.PlayerId == playerId && u.ShardId == shardId : u.PlayerId == playerId);
    }

    public ActivityPiggyBank CreateDefaultPiggyBank(long playerId, short shardId)
    {
        var data = DataUtil.DefaultPiggyBankData(playerId, shardId);
        dbCtx.ActivityPiggyBanks.Add(data);
        return data;
    }

    public Task<ActivityUnrivaledGod?> GetUnrivaledGodDataAsync(
        long playerId,
        short shardId,
        int activityId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityUnrivaledGods
            .Where(t => t.PlayerId == playerId && t.ShardId == shardId && t.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public ActivityUnrivaledGod CreateDefaultUnrivaledGodData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultUnrivaledGodData(playerId, shardId, activityId);
        dbCtx.ActivityUnrivaledGods.Add(data);
        return data;
    }

    public Task<List<ActivityUnrivaledGod>> GetAllUnrivaledGodDataAsync(
        long playerId,
        short shardId,
        TrackingOptions trackingOptions = TrackingOptions.NoTracking)
    {
        return dbCtx.ActivityUnrivaledGods
            .Where(t => t.PlayerId == playerId && t.ShardId == shardId)
            .WithTrackingOptions(trackingOptions)
            .ToListAsync();
    }

    public void ClearUnrivaledGodDataList(List<ActivityUnrivaledGod> unrivaledGodDataList)
    {
        dbCtx.ActivityUnrivaledGods.RemoveRange(unrivaledGodDataList);
    }

    public Task<ActivityCoopBossInfo?> GetCoopBossDataAsync(
        long playerId,
        short shardId,
        int activityId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityCoopBossInfoSet
            .Where(t => t.PlayerId == playerId && t.ShardId == shardId && t.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public ActivityCoopBossInfo CreateDefaultCoopBossData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultCoopBossData(playerId, shardId, activityId);
        dbCtx.ActivityCoopBossInfoSet.Add(data);
        return data;
    }

    public Task<ActivityTreasureMaze?> GetTreasureMazeDataAsync(
        long playerId,
        short shardId,
        int activityId)
    {
        return dbCtx.ActivityTreasureMazeInfos
            .Where(t => t.PlayerId == playerId && t.ShardId == shardId && t.ActivityId == activityId)
            .FirstOrDefaultAsync();
    }

    public ActivityTreasureMaze CreateTreasureMazeData(long playerId, short shardId, int activityId, long startTimestamp)
    {
        var data = DataUtil.DefaultTreasureMazeData(playerId, shardId, activityId, startTimestamp);
        dbCtx.ActivityTreasureMazeInfos.Add(data);
        return data;
    }

    public Task<ActivityEndlessChallenge?> GetEndlessChallengeDataAsync(
        long playerId,
        short shardId,
        int activityId)
    {
        return dbCtx.ActivityEndlessChallenges
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .FirstOrDefaultAsync();
    }

    public ActivityEndlessChallenge CreateDefaultEndlessChallengeData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultEndlessChallengeData(playerId, shardId, activityId);
        dbCtx.ActivityEndlessChallenges.Add(data);
        return data;
    }

    public Task<ActivitySlotMachine?> GetSlotMachineDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return dbCtx.ActivitySlotMachines
            .IgnoreQueryFilters()
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public Task<List<ActivitySlotMachine>> GetSlotMachineListByPlayerAsync(long playerId, short shardId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.ActivitySlotMachines
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId)
            .WithTrackingOptions(trackingOptions)
            .ToListAsync();
    }

    public ActivitySlotMachine CreateDefaultSlotMachineData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultSlotMachineData(playerId, shardId, activityId);
        dbCtx.ActivitySlotMachines.Add(data);
        return data;
    }

    public Task<ActivityOneShotKill?> GetOneShotKillDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityOneShotKills
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }
    public ActivityOneShotKill CreateOneShotKillData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultOneShotKillData(playerId, shardId, activityId);
        dbCtx.ActivityOneShotKills.Add(data);
        return data;
    }

    public Task<ActivityTreasureHunt?> GetTreasureHuntDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityTreasureHunts
            .IgnoreQueryFilters()
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public Task<List<ActivityTreasureHunt>> GetTreasureHuntListByPlayerAsync(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityTreasureHunts
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId)
            .WithTrackingOptions(trackingOptions)
            .ToListAsync();
    }

    public ActivityTreasureHunt CreateDefaultTreasureHuntData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultTreasureHuntData(playerId, shardId, activityId);
        dbCtx.ActivityTreasureHunts.Add(data);
        return data;
    }

    public Task<ActivityRpgGame?> GetRpgGameDataAsync(long playerId, short shardId, int activityId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityRpgGames
            .IgnoreQueryFilters()
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }
    
    public ActivityRpgGame CreateDefaultRpgGameData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultRpgGameData(playerId, shardId, activityId);
        dbCtx.ActivityRpgGames.Add(data);
        return data;
    }

    public Task<ActivityLoogGame?> GetLoogGameDataAsync(long playerId, short shardId, int activityId,
        TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityLoogGames
            .IgnoreQueryFilters()
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }
    
    public ActivityLoogGame CreateDefaultLoogGameData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultLoogGameData(playerId, shardId, activityId);
        dbCtx.ActivityLoogGames.Add(data);
        return data;
    }

    public Task<ActivityCsgoStyleLottery?> GetCsgoStyleLotteryDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityCsgoStyleLotteryInfos
            .IgnoreQueryFilters()
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId && data.ActivityId == activityId)
            .WithTrackingOptions(trackingOptions)
            .FirstOrDefaultAsync();
    }

    public ActivityCsgoStyleLottery CreateDefaultCsgoStyleLotteryData(long playerId, short shardId, int activityId)
    {
        var data = DataUtil.DefaultCsgoStyleLotteryData(playerId, shardId, activityId);
        dbCtx.ActivityCsgoStyleLotteryInfos.Add(data);
        return data;
    }

    public Task<List<ActivityCsgoStyleLottery>> GetCsgoLotteryDataList(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return dbCtx.ActivityCsgoStyleLotteryInfos
            .Where(data => data.PlayerId == playerId && data.ShardId == shardId)
            .WithTrackingOptions(trackingOptions)
            .ToListAsync();
    }
}