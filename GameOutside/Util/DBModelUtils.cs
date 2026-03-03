using GameExternal;
using GameOutside.Models;

namespace GameOutside.Util;

public static class DBModelUtils
{
    public static HistoryResourceRecord ToHistory(this GameEndResourceRecord record)
    {
        return new HistoryResourceRecord()
        {
            Key = record.Key, AccuCount = record.AccuCount, Production = record.Production,
        };
    }

    public static HistoryFightUnitInfo ToHistory(this GameEndFightUnitInfo fightUnitInfo)
    {
        return new HistoryFightUnitInfo()
        {
            Key = fightUnitInfo.Key, Level = fightUnitInfo.Level, TotalDamage = fightUnitInfo.TotalDamage,
        };
    }

    public static UserHistory ToUserHistory(this NormalGameEndMessage message, short shardId, long playerId, long score)
    {
        var exp = message.ExpScoreList.FirstOrDefault(element => element.PlayerId == playerId)?.Exp ?? 0;
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            GameTime = message.GameRealTime,
            GameStartTime = message.GameStartTime,
            MapType = (int)message.MapType,
            TypedMapId = message.TypedMapId,
            MapId = message.MapId,
            // TODO 后续将这两个值拆开
            Difficulty = message.Difficulty * 10000 + message.DifficultyLevel,
            Win = message.Win,
            KillCount = message.KillCount,
            MapUnlockRatio = message.MapUnlockRatio,
            Exp = exp,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = message.FightUnitList.Select(ToHistory).ToList(),
            DynamicInfo = message.DynamicInfo,
        };
    }

    public static UserHistory ToFixedMapUserHistory(
        this NormalGameEndMessage message,
        short shardId,
        long playerId,
        long score,
        int starCount)
    {
        var history = ToUserHistory(message, shardId, playerId, score);
        // fixedMap模式使用Difficulty来记录StarCount
        history.Difficulty = starCount;
        return history;
    }

    public static UserHistory ToUserHistory(this EndlessGameEndMessage message, short shardId, long playerId, long score)
    {
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            GameTime = message.GameRealTime,
            GameStartTime = message.GameStartTime,
            MapType = (int)GameMapType.EndlessMap,
            TypedMapId = 0,
            MapId = 0,
            Difficulty = message.Difficulty * 10000 + message.Level,
            Win = false,
            KillCount = message.KillCount,
            MapUnlockRatio = 1f,
            Exp = 0,
            Score = score,
            DynamicInfo = message.DynamicInfo,
        };
    }

    public static UserHistory ToUserHistory(
        this SurvivorGameEndMessage message,
        short shardId,
        long playerId,
        long score)
    {
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            GameTime = message.GameRealTime,
            GameStartTime = message.GameStartTime,
            MapType = (int)GameMapType.SurvivorMap,
            TypedMapId = 0,
            MapId = 0,
            Difficulty = message.DifficultyLevel,
            Win = message.Win,
            KillCount = message.KillCount,
            MapUnlockRatio = 1f,
            Exp = 0,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = message.FightUnitList.Select(ToHistory).ToList(),
            DynamicInfo = message.DynamicInfo,
        };
    }

    public static UserHistory ToUserHistory(
        this TowerDefenceGameEndMessage message,
        short shardId,
        long playerId,
        long score)
    {
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            GameTime = message.GameRealTime,
            GameStartTime = message.GameStartTime,
            MapType = (int)GameMapType.TowerDefenceMap,
            TypedMapId = 0,
            MapId = 0,
            Difficulty = message.DifficultyLevel,
            Win = message.Win,
            KillCount = message.KillCount,
            MapUnlockRatio = 1f,
            Exp = 0,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = message.FightUnitList.Select(ToHistory).ToList(),
            DynamicInfo = message.DynamicInfo,
        };
    }

    public static UserHistory ToUserHistory(
        this TrueEndlessGameEndMessage message,
        short shardId,
        long playerId,
        long score)
    {
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            GameTime = message.GameRealTime,
            GameStartTime = message.GameStartTime,
            MapType = (int)GameMapType.TrueEndlessMap,
            TypedMapId = 0,
            MapId = 0,
            Difficulty = message.MonsterWaveCount,
            Win = false,
            KillCount = message.KillCount,
            MapUnlockRatio = 1f,
            Exp = 0,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = message.FightUnitList.Select(ToHistory).ToList(),
            DynamicInfo = message.DynamicInfo,
        };
    }

    public static UserHistory ToUserHistory(
        this CoopBossGameEndMessage message,
        short shardId,
        long playerId,
        long score
    )
    {
        var fightUnitInfos = message.FightUnitList.Where(info => info.PlayerId == playerId).Select(ToHistory).ToList();
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            MapType = (int)GameMapType.CoopBossMap,
            TypedMapId = 0,
            MapId = 0,
            Win = message.Win,
            MapUnlockRatio = 1f,
            Exp = 0,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = fightUnitInfos,
            GameTime = message.GameTime,
        };
    }
    
    public static UserHistory ToUserHistory(
        this TreasureMazeGameEndMessage message,
        short shardId,
        long playerId,
        long score
    )
    {
        var fightUnitInfos = message.FightUnitList.Where(info => info.PlayerId == playerId).Select(ToHistory).ToList();
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            MapType = (int)GameMapType.TreasureMazeMap,
            TypedMapId = 0,
            MapId = 0,
            Win = message.Win,
            MapUnlockRatio = 1f,
            Exp = 0,
            KillCount = message.KillCount,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = fightUnitInfos,
            GameTime = message.GameTime,
        };
    }

    public static UserHistory ToUserHistory(this OneShotKillGameEndMessage message, short shardId, long playerId, long score)
    {
        var fightUnitInfos = message.FightUnitList.Where(info => info.PlayerId == playerId).Select(ToHistory).ToList();
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            MapType = (int)GameMapType.OneShotKillMap,
            TypedMapId = 0,
            MapId = 0,
            Win = message.Win,
            MapUnlockRatio = 1f,
            Exp = 0,
            KillCount = message.KillCount,
            Score = score,
            ResourceRecords = message.ResourceList.Select(ToHistory).ToList(),
            FightUnitInfos = fightUnitInfos,
            GameTime = message.GameTime,
        };
    }

    public static UserHistory ToUserHistory(this RpgGameEndMessage message, short shardId, long playerId, long score)
    {
        var fightUnitInfos = message.FightUnitList.Where(info => info.PlayerId == playerId).Select(ToHistory).ToList();
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            MapType = (int)GameMapType.RpgGameMap,
            TypedMapId = 0,
            MapId = 0,
            Win = message.Win,
            MapUnlockRatio = 1f,
            Exp = 0,
            KillCount = message.KillCount,
            Score = score,
            FightUnitInfos = fightUnitInfos,
            GameTime = message.GameTime,
        };
    }

    public static UserHistory ToUserHistory(this LoogGameEndMessage message, short shardId, long playerId, long score)
    {
        var fightUnitInfos = message.FightUnitList.Where(info => info.PlayerId == playerId).Select(ToHistory).ToList();
        return new UserHistory()
        {
            ShardId = shardId,
            PlayerId = playerId,
            Timestamp = message.TimeStamp,
            MapType = (int)GameMapType.LoogGameMap,
            TypedMapId = 0,
            MapId = 0,
            Win = message.Win,
            MapUnlockRatio = 1f,
            Exp = 0,
            KillCount = message.KillCount,
            Score = score,
            FightUnitInfos = fightUnitInfos,
            GameTime = message.GameTime,
        };
    }
}