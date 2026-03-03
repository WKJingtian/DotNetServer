using System.ComponentModel.DataAnnotations.Schema;

// ReSharper disable InconsistentNaming

namespace GameOutside.Models;

using System.ComponentModel.DataAnnotations;
using ChillyRoom.Functions.DBModel;
using Microsoft.EntityFrameworkCore;

// dotnet ef migrations add 迁移名称
// dotnet ef migrations script -o Migrations.sql
// http://localhost:8080/swagger/v2/swagger.json
// nswag run

public class UserRank : BaseEntityNoSoftDelete
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int Division { get; set; }
    public long GroupId { get; set; }
    public long HighestScore { get; set; }
    public bool Win { get; set; }
    public long Timestamp { get; set; }
    public int SeasonNumber { get; set; }
}

public class UserDivision : BaseEntity
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int DivisionScore { get; set; }
    public int MaxDivisionScore { get; set; }
    public int LastSeasonNumber { get; set; }
    public bool RewardReceived { get; set; }
    public int LastDivisionScore { get; set; }
    public int LastDivisionRank { get; set; }

    [Obsolete("UseLess Filed")]
    public int LastWorldRank { get; set; }
}

public class UserEndlessRank
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int SeasonNumber { get; set; }
    public long SurvivorScore { get; set; } = 0;
    public long SurvivorTimestamp { get; set; } = 0;
    public long TowerDefenceScore { get; set; } = 0;
    public long TowerDefenceTimestamp { get; set; } = 0;
    public long TrueEndlessScore { get; set; } = 0;
    public long TrueEndlessTimestamp { get; set; } = 0;
}

public class UserInfo : BaseEntity
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required long UserId { get; set; }

    [StringLength(255)]
    public string? Signature { get; set; }

    public bool HideHistory { get; set; }
    public int AvatarFrameItemID { get; set; }
    public int NameCardItemID { get; set; }

    // 世界排名历史
    public List<int> WorldRankHistories { get; set; } = [];

    // 世界排名历史赛季
    public List<int> WorldRankSeasonHistories { get; set; } = [];

    [NotMapped]
    public List<UserHistory> Histories { get; set; } = new();
}

[Owned]
public class UserLevelData
{
    public int LevelScore { get; set; }

    public int Level { get; set; }

    public List<long> RewardStatusList { get; set; } = [];
}

[Owned]
public class UserDifficultyData
{
    public List<int> Levels { get; init; } = [];

    public List<int> Stars { get; init; } = [];
}

public class UserAssets
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int CoinCount { get; set; }
    public int DiamondCount { get; set; }

    public long LastInDebtTime { get; set; } // 上次负债（DiamondCount < 0）时间 
    public List<int> Heroes { get; set; } = new();

    public UserLevelData LevelData { get; set; } = new();

    public UserDifficultyData DifficultyData { get; set; } = new();

    public int TimeZoneOffset { get; set; }

    [NotMapped]
    public List<UserItem> UserItems { get; set; } = [];

    [NotMapped]
    public List<UserCard> UserCards { get; set; } = [];

    [NotMapped]
    public List<UserTreasureBox> UserTreasureBoxes { get; set; } = [];
}

public class UserItem
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int ItemId { get; set; }
    public int ItemCount { get; set; }
}

public class UserCard : BaseEntityNoSoftDelete
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int CardId { get; set; }
    public int CardLevel { get; set; }
    public int CardExp { get; set; }
    public int CardArenaDifficultyReached { get; set; } = -1;
    public int CardArenaLevelReached { get; set; } = -1;
    public long Timestamp { get; set; }
}

public class UserTreasureBox
{
    public Guid Id { get; set; }
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int ItemId { get; set; }
    public int ItemCount { get; set; }
    public int StarCount { get; set; }
}

public class UserGameInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public long LastGameEndTime { get; set; }
    public int DelayBoxSequence { get; set; }
    public int TicketBoxSequence { get; set; }
    public int CheatAccumulate { get; set; } // 作弊次数累计
    public long LastArenaBoxRewardTime { get; set; }   // 上次竞技场获得宝箱奖励的时间
    public long TodayArenaBoxRewardCount { get; set; } // 今日获得竞技场宝箱奖励的次数
    public List<int> DrawNewCardCountList { get; init; } = [];
    public List<int> DrawCardCountList { get; init; } = [];

    // 外卡数量记录 derelict
    public List<int> GeneralCardCountList { get; init; } = [];
}

public class PlayerPunishmentTask : BaseEntityNoSoftDelete
{
    public required string TaskId { get; set; }
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
}

public class SocInfo
{
    [Key]
    [StringLength(255)]
    public required string DeviceName { get; set; }
}

/// <summary>
/// 用于记录赛季刷新历史，判断是否有进行完整的赛季更迭流程
/// </summary>
public class SeasonRefreshedHistory : BaseEntity
{
    public int SeasonNumber { get; set; }
    public DateTime RefreshedTime { get; set; }
}

public class UserCustomData
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public string IntData { get; set; } = "";
    public string StrData { get; set; } = "";
}

public class UserCustomCardPool
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int HeroId { get; set; }
    public List<int> CardList { get; set; } = [];
    public int ExtraSlotCount { get; set; } = 0;
}

public class HistoryResourceRecord
{
    public string Key { get; set; }
    public int AccuCount { get; set; }
    public int Production { get; set; }
}

public class HistoryFightUnitInfo
{
    public string Key { get; set; }
    public int Level { get; set; }
    public long TotalDamage { get; set; }
}

public class UserHistory
{
    [Key] public Guid Id { get; set; }
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public long Timestamp { get; set; }
    public float GameTime { get; set; }
    public long GameStartTime { get; set; }
    public int MapType { get; set; }
    public int TypedMapId { get; set; }
    public int MapId { get; set; }
    public bool Win { get; set; }
    public int KillCount { get; set; }
    public float MapUnlockRatio { get; set; }
    public float Exp { get; set; }
    public long Score { get; set; }
    public int Difficulty { get; set; }

    [Column(TypeName = "jsonb")]
    public List<HistoryResourceRecord> ResourceRecords { get; set; } = new();

    [Column(TypeName = "jsonb")]
    public List<HistoryFightUnitInfo> FightUnitInfos { get; set; } = new();

    public string DynamicInfo { get; set; } = "{}";
}

public class UserDailyStoreItem
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int ItemId { get; set; }
    public int ItemCount { get; set; }
    public int Price { get; set; }
    public int PriceType { get; set; }
    public long TimeStamp { get; set; }
    public bool Bought { get; set; }
}

public class UserCommodityBoughtRecord : BaseEntityNoSoftDelete
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int CommodityId { get; set; }
    public int Count { get; set; }
}

public class UserAttendance
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int RewardIndex { get; set; }
    public long CreateTime { get; set; }
    public int TotalLoginDays { get; set; }
    public long LastLoginDate { get; set; }
}

public class UserBattlePassInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int PassId { get; set; }
    public int Exp { get; set; }
    public List<long> ClaimStatus { get; set; } = [];
    public int SuperPassLevel { get; set; }
}

public class UserAchievement
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int ConfigId { get; set; }
    public string Target { get; set; }
    public int CurrentIndex { get; set; }
    public int Progress { get; set; }
    public bool Received { get; set; }
}

public class BeginnerTaskData
{
    public int Id { get; set; }
    public int Progress { get; set; }
}

public class UserBeginnerTask
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public long StartTime { get; set; }
    public int DayIndex { get; set; }
    public int FinishedCount { get; set; }
    public bool Received { get; set; }

    [Column(TypeName = "jsonb")]
    public List<BeginnerTaskData> TaskList { get; set; } = new();
}

public class UserDailyTask
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public long TaskRefreshTime { get; set; }
    public List<int> TaskProgress { get; set; } = [];
    public int DailyTaskRewardClaimStatus { get; set; }
    public int ActiveScoreRewardClaimStatus { get; set; }
}

public class UserDailyStoreIndex
{
    public required short ShardId { get; set; }

    [Key]
    public required long PlayerId { get; set; }

    public int Index { get; set; }
}

public class UserMallAdvertisement
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int Id { get; set; }
    public long LastTime { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// 全渠道的User信息（以UserId索引）
/// </summary>
public class UserGlobalInfo
{
    public long UserId { get; set; }

    public long LastLoginPlayerId { get; set; }
    //public long LastLoginTime { get; set; }
    //public long LastOnlineTime { get; set; }
    //public int IconIndex { get; set; }
    //[StringLength(255)]
    //public string Signature { get; set; } = "";
}

public class UserFixedLevelMapProgress
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int MapId { get; set; }
    public int StarCount { get; set; }
    public List<int>? FinishedTaskList { get; set; }
}

public class ActivityLuckyStar
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; } // 当前条目对应的活动Id(用来判定是否在活动时间内)
    public int Sequence { get; set; } = 0;       // 买到第几个了
    public int Cycle { get; set; } = 0;          // 循环次数
    public bool Free { get; set; } = false;      // 本次购买是否免费
}

public class UserIapPurchaseRecord
{
    public Guid Id { get; set; }
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public string IapItemId { get; set; }
    public long WhenPurchased { get; set; }
}

public class UserIdleRewardInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public long StartTime { get; set; }
    public List<long> StolenRecords { get; init; } = [];
    public int IdleRewardId { get; set; } = 0;
}

public class UserPaymentAndPromotionStatus
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public string LastPromotedPackage { get; set; } = "";
    public long PackagePromotionTime { get; set; } = 0;
    public int IceBreakingPayPromotion { get; set; } = 0;

    [Column(TypeName = "jsonb")]
    public Dictionary<string, long> DoubleDiamondBonusTriggerRecords { get; set; } = new();
}

public class UserStarStoreInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    // 战役星星奖励的领取情况，为了以后可能扩张到超过64个奖励。这里用个list
    public List<long> StarRewardClaimStatus { get; set; } = [];
}

[Serializable]
public class FortuneBagAcquireInfo
{
    public long AcquireTime { get; set; } = 0;
    public int BagCount { get; set; } = 0;
    public int ClaimStatus { get; set; } = 0;
}

public class UserFortuneBagInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public required int ActivityId { get; set; } // 当前条目对应的活动Id(用来判定是否在活动时间内)

    [Column(TypeName = "jsonb")]
    public List<FortuneBagAcquireInfo> FortuneBags { get; set; } = new();

    public int FortuneLevelRewardClaimStatus { get; set; } = 0;
}

public class ServerData
{
    public required string Id { get; set; }

    public int Value { get; set; } = 0;
}

public class ActivityPiggyBank
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int Exp { get; set; }
    public List<long> ClaimStatus { get; set; } = [];
    public int PaidLevel { get; set; }
}

public class UnrivaledGodTask
{
    public int Progress { get; set; } // 任务进度
    public bool Claimed { get; set; }
    public long UpdatedAt { get; set; } // 进度更新时间（用于进度刷新
}

public class ActivityUnrivaledGod
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; }

    public int KeyCount { get; set; } // 无双钥匙的数量

    public int GuaranteeProgress { get; set; } // 保底进度

    public int ScorePoint { get; set; } // 积分，积分会继承至下一次活动

    [Column(TypeName = "jsonb")]
    public Dictionary<int, int> ExchangeRecord { get; set; } = new(); // 兑换数量记录

    [Column(TypeName = "jsonb")]
    public Dictionary<string, UnrivaledGodTask> TaskRecord { get; set; } = new(); // 任务记录
}

public class ActivityCoopBossInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; }
    public int LastLevel { get; set; } = 0;
    public long LastLevelActivateTime { get; set; } = 0;
    public int RefreshCountToday { get; set; } = 0;
    public long LastRefreshTime { get; set; }
    public int DrewCount { get; set; } = 0;

    /// <summary>
    /// GameEndCountToday 对应的语义其实是InvitedEndCountToday
    /// </summary>
    public int GameEndCountToday { get; set; } = 0;

    /// <summary>
    /// 这个属性不再使用了，因为统一了刷新时间，都用LastRefreshTime就好
    /// </summary>
    [Obsolete]
    public long LastRefreshEndCountTime { get; set; } = 0;
}

public class MonthPassInfo : BaseEntityNoSoftDelete
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public long PassAcquireTime { get; set; }
    public int PassDayLength { get; set; }
    public int RewardClaimStatus { get; set; }
    public int LastRewardClaimDay { get; set; }
}

[PrimaryKey(nameof(PlayerId), nameof(ShardId), nameof(ActivityId))]
public class ActivityEndlessChallenge
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; }
    public int MaxUnlockDifficulty { get; set; }
    public int TodayGameCount { get; set; }
    public long LastGameTime { get; set; }
}

[PrimaryKey(nameof(PlayerId), nameof(ShardId))]
public class UserDailyTreasureBoxProgress
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public int Progress { get; set; }          // 当前进度，每完成一局游戏增加
    public int RewardClaimStatus { get; set; } // 位存储
    public long Timestamp { get; set; }
}

/// <summary>
/// 注意需要和 global cache 配合使用，不同集群本地的 redis 是数据不通的
/// </summary>
public class RedisLuaScript : BaseEntity
{
    public required string Name { get; set; }

    public required string Sha { get; set; }
}

public class LocalRedisLuaScript : BaseEntity
{
    public required short ShardId { get; set; }

    public required string Name { get; set; }

    public required string Sha { get; set; }
}

public class UserEncryptionInfo
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public string EncryptionKey { get; set; } = "";
}

public class ActivityOneShotKill
{
    public required short ShardId { get; set; }

    public required long PlayerId { get; set; }

    // 当前条目对应的活动Id(用来判定是否在活动时间内)
    public required int ActivityId { get; set; }

    // 每张地图的首通奖励领取状态
    public long MapCompleteRewardClaimStatus { get; set; }

    // 解放区每天可以领一次奖励，记一下每一关上次领奖的时间
    public List<long> MapConquerRewardClaimTimestamp { get; set; } = new();

    // 每个全局任务奖励的领取状态
    public long TaskCompleteRewardClaimStatus { get; set; }

    // 所有地图都攻略完成后的终极奖励的领取状态
    public bool OneShotKillUltimateRewardClaimStatus { get; set; }

    // 每天主动游戏次数 已废弃
    [Obsolete] public int TodayGameCount { get; set; }

    [Obsolete] public long GameCountUpdateTimestamp { get; set; }

    // 每天受邀请游戏次数 已废弃
    [Obsolete] public int TodayAwayGameCount { get; set; }

    [Obsolete] public long AwayGameCountUpdateTimestamp { get; set; }

    // 今天普通难度胜利次数
    public int NormalVictoryCount { get; set; }

    public long NormalVictoryUpdateTimestamp { get; set; }

    // 今天挑战难度胜利次数
    public int ChallengeVictoryCount { get; set; }
    public long ChallengeVictoryUpdateTimestamp { get; set; }
}

public class ActivitySlotMachine : BaseEntity
{
    public required short ShardId { get; set; }

    public required long PlayerId { get; set; }

    // 当前条目对应的活动Id(用来判定是否在活动时间内)
    public required int ActivityId { get; set; }

    // 上次抽奖的时间，用于判断是否是新的一天
    public long LastDrawTime { get; set; }

    // 今日抽奖次数
    public int TodayDrawCount { get; set; }

    // 活动积分
    public int ActivityPoint { get; set; }

    // 积分奖励领取记录
    public long PointRewardClaimStatus { get; set; }

    // 玩家是否为此次抽奖购买了奖励翻倍
    public List<int> RewardDoubledUpItemCount { get; set; } = new();

    // 当前抽奖每个槽位里的奖励
    public List<int> RewardsInSlot { get; set; } = new();

    // 当前抽奖每个槽位的重抽次数，每次抽奖会重置
    public List<int> RerollCounts { get; set; } = new();

    // 保底进度，每个品质有各自的保底计算，高品质的保底进度清空时会同时清空更低等级的保底进度
    public List<int> GuaranteeProgressList { get; set; } = new();
}

/// <summary>
/// 大概的规则简述:
/// 1.NextShowingLevel:开启活动要求的历练之路条件,初始值直是9,领取奖励后立马根据配置升级NextShowingLeve
/// 2.是否能领取奖励由客户端决定(不安全),只要客户端调用了,且历练之路等级满足了,就可以发放奖励并且展示任务
/// 3.客户端展示的任务进度和状态都来自于凉屋公共服务,不是本服务
/// </summary>
public class UserH5FriendActivityInfo : BaseEntityNoSoftDelete
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    /// <summary>
    /// 记录下一次开放奖励的历练之路奖励的等级, 因为旧活动结束这个字段被废弃了
    /// </summary>
    [Obsolete]
    public long NextShowingLevel { get; set; }

    /// <summary>
    /// 记录下一次开放奖励的历练之路奖励的等级
    /// </summary>
    public long NextShowingLevelV1 { get; set; }

    /// <summary>
    /// 是否领取过被邀请奖励了, 一个人只能领取一次邀请码
    /// </summary>
    public bool ClaimedInvitationCode { get; set; }
}

/// <summary>
/// 因业务更改，这张表不再使用了，请勿使用
/// </summary>
[Obsolete]
public class InvitationCodeClaimRecord
{
    /// <summary>
    /// 已经过期，不在通过这张表来存储邀请码了，请勿使用
    /// </summary>
    [Obsolete]
    public string GiftCode { get; set; } = "";
}

/// <summary>
/// 给ios用户存一下game center奖励的领取状态
/// </summary>
public class IosGameCenterRewardInfo : BaseEntityNoSoftDelete
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    /// <summary>
    /// 是否领取过奖励
    /// </summary>
    public bool RewardClaimed { get; set; }
}

public class ActivityTreasureMaze
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; } // 当前条目对应的活动Id(用来判定是否在活动时间内)

    // 钥匙数，主动建房需要，每天自动获得
    public int GameKeyCount { get; set; } = 0;

    // 上次获得钥匙的时间戳
    public long LastGameKeyTimestamp { get; set; } = 0;

    // 客场游戏次数，有上限
    public int AwayGameCountToday { get; set; } = 0;

    // 上一场客场游戏时间戳，按开始时间算
    public long LastAwayGameTimestamp { get; set; } = 0;

    // 记录已经完成的难度
    public List<int> LevelPassed { get; set; } = new();
}

public class TreasureHuntSlot
{
    public int Id { get; set; }
    public bool IsVariant { get; set; }
    public bool HasOpen { get; set; }
}

public class ActivityTreasureHunt : BaseEntity
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }

    public required int ActivityId { get; set; }

    // 上次刷新奖池的时间
    public long LastRefreshTime { get; set; } = 0;

    // 今日刷新奖池的次数，活动限制玩家每天最多刷新99次
    public int TodayRefreshCount { get; set; } = 0;

    // 当前积分
    public int ScorePoints { get; set; } = 0;

    // 当前钥匙数量
    public int KeyCount { get; set; } = 0;

    // 积分奖励领取状态（位存储）
    public long ScoreRewardClaimStatus { get; set; } = 0;

    //  奖池信息
    [Column(TypeName = "jsonb")]
    public List<TreasureHuntSlot> RewardSlots { get; set; } = new();
}

public class ActivityRpgGame : BaseEntity
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; }

    // 上次游玩次数记录时间
    public long LastGameCountRecordTime { get; set; } = 0;
    // 今日游戏次数
    public int TodayGameCount { get; set; } = 0;
    // 关卡通过状态
    public long LevelPassedStatus { get; set; } = 0;
}

public class ActivityLoogGame : BaseEntity
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; }

    // 上次游玩次数记录时间
    public long LastGameCountRecordTime { get; set; } = 0;
    // 今日游戏次数
    public int TodayGameCount { get; set; } = 0;
    // 关卡通过状态
    public long LevelPassedStatus { get; set; } = 0; 
}

public class CsgoStyleLotteryTask
{
    public int Progress { get; set; } // 任务进度
    public bool Claimed { get; set; }
    public long UpdatedAt { get; set; } // 进度更新时间（用于进度刷新
}

public class ActivityCsgoStyleLottery : BaseEntity
{
    public required short ShardId { get; set; }
    public required long PlayerId { get; set; }
    public required int ActivityId { get; set; }
    
    // 活动积分
    public int ActivityPoint { get; set; } = 0;
    // 活动积分
    public long PointRewardClaimStatus { get; set; } = 0;
    // 抽奖钥匙数量
    public int KeyCount { get; set; } = 0;
    // 使用玉璧购买了几次钥匙
    public int KeyPurchaseCountByDiamond { get; set; } = 0;
    //今天一共抽了几次奖
    public int TotalLotteryDrawToday { get; set; }= 0;
    //上述两条信息的最后刷新时间
    public long LotteryDrawInfoRefreshTimestamp { get; set; } = 0;
    // 活动高级通行证激活状态
    public long ActivityPremiumPassStatus { get; set; } = 0; 
    // 高级通行证的奖励领取时间
    public List<long> PremiumPassDailyRewardClaimStatus { get; set; } = []; 
    // 奖励记录，用于展示和计算保底
    public List<int> RewardRecord { get; set; } = [];
    // 每个奖励记录对应的事件，用于展示
    public List<long> RewardRecordTime { get; set; } = [];
    // 任务记录
    [Column(TypeName = "jsonb")]
    public Dictionary<string, CsgoStyleLotteryTask> TaskRecord { get; set; } = new();
}
