using System;
using System.Collections.Generic;
using GameExternal;
using MessagePack;

// ReSharper disable CollectionNeverUpdated.Global
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
// ReSharper disable ClassNeverInstantiated.Global

[MessagePackObject]
public class GameEndBuildingInfo
{
    [Key(0)]
    public long PlayerId;

    [Key(1)]
    public int Id;

    [Key(2)]
    public int Count;

    [Key(3)]
    public int DamageCount;
}

[MessagePackObject]
public class GameEndResourceScore
{
    [Key(0)]
    public long PlayerId;

    [Key(1)]
    public int Score;
}

[MessagePackObject]
public class GameEndExpScore
{
    [Key(0)]
    public long PlayerId;

    [Key(1)]
    public float Exp;
}

[MessagePackObject]
public class AchievementRecord
{
    [Key(0)]
    public string Key;

    [Key(1)]
    public string Target;

    [Key(2)]
    public int Count;
}

[MessagePackObject]
public class TaskRecord
{
    [Key(0)]
    public string Key;

    [Key(1)]
    public int Count;
}

[MessagePackObject]
public class GameEndResourceRecord
{
    [Key(0)]
    public string Key;

    [Key(1)]
    public int AccuCount;

    [Key(2)]
    public int Production;
}

[MessagePackObject]
public class GameEndFightUnitInfo
{
    [Key(0)]
    public string Key;

    [Key(1)]
    public int Level;

    [Key(2)]
    public long TotalDamage;
    
    [Key(3)]
    public long PlayerId;
}

[MessagePackObject]
public class RpgGameEndMonoSoldierInfo
{
    [Key(0)]
    public long PlayerId;

    [Key(1)]
    public long DamageDone;
}


[Union(0, typeof(NormalGameEndMessage))]
[Union(1, typeof(EndlessGameEndMessage))]
[Union(2, typeof(SurvivorGameEndMessage))]
[Union(3, typeof(TowerDefenceGameEndMessage))]
[Union(4, typeof(TrueEndlessGameEndMessage))]
[Union(5, typeof(CoopBossGameEndMessage))]
[Union(6, typeof(TreasureMazeGameEndMessage))]
[Union(7, typeof(OneShotKillGameEndMessage))]
[Union(8, typeof(RpgGameEndMessage))]
[Union(9, typeof(LoogGameEndMessage))]
[MessagePackObject]
public abstract class GameEndMessageBase
{
    [Key(0)]
    public long TimeStamp;
}

[MessagePackObject]
public class NormalGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public long RoomId;

    [Key(2)]
    public List<GameEndBuildingInfo> BuildingList;

    [Key(3)]
    public List<GameEndResourceScore> ResourceScoreList;

    [Key(4)]
    public List<GameEndExpScore> ExpScoreList;

    [Key(5)]
    public List<AchievementRecord> AchievementRecords;

    [Key(6)]
    public List<TaskRecord> TaskRecords;

    [Key(7)]
    public int KillCount;

    [Key(8)]
    public float GameTime;

    [Key(9)]
    public float GameRealTime;

    [Key(10)]
    public long GameStartTime;

    [Key(11)]
    public bool Win;

    [Key(12)]
    public float MapUnlockRatio;

    [Key(13)]
    public bool Reborn;

    [Key(14)]
    public GameMapType MapType;

    [Key(15)]
    public int TypedMapId; // mapType对应的config id

    [Key(16)]
    public int Difficulty;

    [Key(17)]
    public int DifficultyLevel;

    [Key(18)]
    public int MapId;

    [Key(19)]
    public int ExplorePoint;

    [Key(20)]
    public int UnlockTileCount;

    [Key(21)]
    public List<GameEndResourceRecord> ResourceList;

    [Key(22)]
    public List<GameEndFightUnitInfo> FightUnitList;

    [Key(23)]
    public int HeadquarterHpPercent;

    [Key(24)]
    public int TrainCount;

    [Key(25)]
    public string DynamicInfo;

    [Key(26)]
    public List<string> AllBuildingBuiltThroughoutGame;

    [Key(27)]
    public float SkippedTimes;

    [Key(28)]
    public int LastWaveTime;
}

[MessagePackObject]
public class EndlessGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<AchievementRecord> AchievementRecords;

    [Key(2)]
    public List<TaskRecord> TaskRecords;

    [Key(3)]
    public int KillCount;

    [Key(4)]
    public float GameTime;

    [Key(5)]
    public float GameRealTime;

    [Key(6)]
    public long GameStartTime;

    [Key(7)]
    public string DynamicInfo;

    [Key(8)]
    public bool Reborn;
    
    [Key(9)]
    public int Difficulty;

    [Key(10)]
    public int Level;

    [Key(11)]
    public int ActivityId;

    [Key(12)]
    public bool Win;
}

[MessagePackObject]
public class SurvivorGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<AchievementRecord> AchievementRecords;

    [Key(2)]
    public List<TaskRecord> TaskRecords;

    [Key(3)]
    public int KillCount;

    [Key(4)]
    public int DifficultyLevel;

    [Key(5)]
    public float GameTime;

    [Key(6)]
    public float GameRealTime;

    [Key(7)]
    public long GameStartTime;

    [Key(8)]
    public string DynamicInfo;

    [Key(9)]
    public bool Reborn;

    [Key(10)]
    public bool Win;

    [Key(11)]
    public List<GameEndResourceRecord> ResourceList;

    [Key(12)]
    public List<GameEndFightUnitInfo> FightUnitList;
}

[MessagePackObject]
public class TowerDefenceGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<AchievementRecord> AchievementRecords;

    [Key(2)]
    public List<TaskRecord> TaskRecords;

    [Key(3)]
    public int KillCount;

    [Key(4)]
    public int DifficultyLevel;

    [Key(5)]
    public float GameTime;

    [Key(6)]
    public float GameRealTime;

    [Key(7)]
    public long GameStartTime;

    [Key(8)]
    public string DynamicInfo;

    [Key(9)]
    public bool Reborn;

    [Key(10)]
    public bool Win;

    [Key(11)]
    public List<GameEndResourceRecord> ResourceList;

    [Key(12)]
    public List<GameEndFightUnitInfo> FightUnitList;
}

[MessagePackObject]
public class TrueEndlessGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<AchievementRecord> AchievementRecords;

    [Key(2)]
    public List<TaskRecord> TaskRecords;

    [Key(3)]
    public int KillCount;

    [Key(4)]
    public int MonsterWaveCount;

    [Key(5)]
    public float GameTime;

    [Key(6)]
    public float GameRealTime;

    [Key(7)]
    public long GameStartTime;

    [Key(8)]
    public string DynamicInfo;

    [Key(9)]
    public bool Reborn;

    [Key(10)]
    public List<GameEndResourceRecord> ResourceList;

    [Key(11)]
    public List<GameEndFightUnitInfo> FightUnitList;
    
    [Key(12)]
    public float SkippedTimes;
}

[MessagePackObject]
public class CoopBossGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<GameEndFightUnitInfo> FightUnitList;

    [Key(2)]
    public bool Win;
    
    [Key(3)]
    public List<AchievementRecord> AchievementRecords;

    [Key(4)]
    public List<TaskRecord> TaskRecords;

    [Key(5)]
    public int ActivityId;

    [Key(6)]
    public int Level;

    [Key(7)]
    public int KillCount;
    
    [Key(8)]
    public List<GameEndResourceRecord> ResourceList;
    
    [Key(9)]
    public bool Invited;

    [Key(10)]
    public float GameTime;
}

[MessagePackObject]
public class RpgGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<GameEndFightUnitInfo> FightUnitList;

    [Key(2)]
    public bool Win;

    [Key(3)]
    public List<AchievementRecord> AchievementRecords;

    [Key(4)]
    public List<TaskRecord> TaskRecords;

    [Key(5)]
    public int ActivityId;

    [Key(6)]
    public int Level;

    [Key(7)]
    public int KillCount;

    [Key(8)]
    public float GameTime;

    [Key(9)]
    public RpgGameEndMonoSoldierInfo MonoSoldierInfo;
    
    [Key(10)]
    public List<GameEndResourceRecord> ResourceList;
}


[MessagePackObject]
public class LoogGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<GameEndFightUnitInfo> FightUnitList;

    [Key(2)]
    public bool Win;

    [Key(3)]
    public List<AchievementRecord> AchievementRecords;

    [Key(4)]
    public List<TaskRecord> TaskRecords;

    [Key(5)]
    public int ActivityId;

    [Key(6)]
    public int Level;

    [Key(7)]
    public int KillCount;

    [Key(8)]
    public float GameTime;

    [Key(9)]
    public RpgGameEndMonoSoldierInfo MonoSoldierInfo;

    [Key(10)]
    public List<GameEndResourceRecord> ResourceList;
}

[MessagePackObject]
public class TreasureMazeGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<GameEndFightUnitInfo> FightUnitList;

    [Key(2)]
    public bool Win;
    
    [Key(3)]
    public List<AchievementRecord> AchievementRecords;

    [Key(4)]
    public List<TaskRecord> TaskRecords;

    [Key(5)]
    public int ActivityId;

    [Key(6)]
    public int DifficultyLevel;

    [Key(7)]
    public int KillCount;
    
    [Key(8)]
    public List<GameEndResourceRecord> ResourceList;
    
    [Key(9)]
    public bool Invited;
    
    [Key(10)]
    public List<int> TreasurePileLooted;

    [Key(11)]
    public int PlayerCount;

    [Key(12)]
    public float GameTime;
}

[MessagePackObject]
public class OneShotKillGameEndMessage : GameEndMessageBase
{
    [Key(1)]
    public List<GameEndFightUnitInfo> FightUnitList;

    [Key(2)]
    public bool Win;
    
    [Key(3)]
    public List<AchievementRecord> AchievementRecords;

    [Key(4)]
    public List<TaskRecord> TaskRecords;

    [Key(5)]
    public int ActivityId;

    [Key(6)]
    public int DifficultyLevel;

    [Key(7)]
    public int KillCount;
    
    [Key(8)]
    public List<GameEndResourceRecord> ResourceList;
    
    [Key(9)]
    public bool Invited;
    
    [Key(10)]
    public List<int> OneShotKillTaskRecords;
    
    [Key(11)]
    public bool IsChallengeMode;

    [Key(12)]
    public float GameTime;
}

public class GetMallAdsRewardMessage
{
    public int Id { get; set; }
    public long TimeStamp { get; set; }
}

public class SpeedUpTreasureBoxMessage
{
    public Guid Id { get; set; }
    public long TimeStamp { get; set; }
}