﻿using GameOutside.Models;
using StackExchange.Redis;

namespace GameOutside.Util
{
    public class LuckyStarDataClient
    {
        public int ActivityId { get; set; }
        public int Sequence { get; set; }
        public int Cycle { get; set; }
        public bool Free { get; set; }
    }

    public class FortuneBagDataClient
    {
        public int ActivityId { get; set; }
        public int ServerFortuneLevel { get; set; }
        public int BagBought { get; set; }
        public int UnclaimedDiamondCountInFortuneBag { get; set; }
        public int FortuneBagDiamondAvailableTomorrow { get; set; }
        public int FortuneLevelRewardClaimStatus { get; set; }
        public List<int> UnclaimedFortuneLevelReward { get; set; }
        public List<int> FortuneLevelRewardCount { get; set; }
    }

    public class SlotMachineDataClient
    {
        public int ActivityId { get; set; }
        public int DrawTimeToday { get; set; }
        public int ActivityPoint { get; set; }
        public long PointRewardClaimStatus { get; set; }
        public List<int> RewardsInSlot { get; set; } = new();
        public List<int> RerollCounts { get; set; } = new();
        public int ProgressToNextTopPrize { get; set; }
        public List<int> DoubleUpCounts { get; set; } = new();
    }

    public class TreasureHuntDataClient
    {
        public int ActivityId { get; set; }
        public int ScorePoints { get; set; }
        public int KeyCount { get; set; }
        public long ScoreRewardClaimStatus { get; set; }
        public int TodayRefreshCount { get; set; }
        public long LastRefreshTime { get; set; }
        public List<TreasureHuntSlot> RewardSlots { get; set; } = new();
    }

    public struct OneShotKillEvent
    {
        public int EventHash { get; set; }
        public long Timestamp { get; set; }
    }
    
    public class OneShotKillDataClient
    {
        public int ActivityId { get; set; }
        public List<int> MapProgress { get; set; } = new();
        public List<int> ProgressAddLastHour { get; set; } = new();
        public List<int> ProgressLoseLastHour { get; set; } = new();
        public List<int> TaskProgress { get; set; } = new();
        public long MapCompleteRewardClaimStatus { get; set; }
        public List<long> MapConquerRewardClaimTimestamp { get; set; } = new();
        public long TaskCompleteRewardClaimStatus { get; set; }
        public bool UltimateRewardClaimStatus { get; set; }
        public int NormalVictoryToday { get; set; }
        public int ChallengeVictoryToday { get; set; }
        public List<OneShotKillEvent> EventList { get; set; } = new();
    }

    public class RpgGameDataClient
    {
        public int ActivityId { get; set; }
        public long LevelPassed { get; set; }
        public int GameCountToday { get; set; }
    }

    public class LoogGameDataClient
    {
        public int ActivityId { get; set; }
        public long LevelPassed { get; set; }
        public int GameCountToday { get; set; }
    }

    public class CsgoStyleLotteryDataClient
    {
        public int ActivityId { get; set; }
        public int ActivityPoint { get; set; }
        public long PointRewardClaimStatus { get; set; }
        public int KeyCount { get; set; }
        public int KeyPurchaseCountByDiamond { get; set; }
        public int TotalCsgoLotteryDrawToday { get; set; }
        public long ActivityPremiumPassStatus { get; set; }
        public List<long> PremiumPassDailyRewardClaimStatus { get; set; } = [];
        public List<int> RewardRecord { get; set; } = [];
        public List<long> RewardRecordTime { get; set; } = [];
        public Dictionary<string, CsgoStyleLotteryTask> TaskRecord { get; set; } = new();
    }

    public class PiggyBankDataClient
    {
        public List<long> ClaimStatus { get; set; }
        public int Exp { get; set; }
        public int PaidLevel { get; set; }
    }

    public class UnrivaledGodDataClient
    {
        public int ActivityId { get; set; }
        public int KeyCount { get; set; }                                             // 无双钥匙的数量
        public int ScorePoint { get; set; }                                           // 积分
        public int GuaranteeProgress { get; set; }                                    // 保底进度 
        public Dictionary<int, int> ExchangeRecord { get; set; } = new();             // 兑换数量记录
        public Dictionary<string, UnrivaledGodTask> TaskRecord { get; set; } = new(); // 任务记录
    }

    public class TreasureMazeDataClient
    {
        public int ActivityId { get; set; }
        public int KeyCount { get; set; }
        public int AwayGameCountToday { get; set; }
        public List<int> DifficultyPassed { get; set; }
    }
    
    public class CoopBossDataClient
    {
        public int ActivityId { get; set; }
        public int LastLevel { get; set; }
        public long LastLevelActivateTime { get; set; }
        public int RefreshCountToday { get; set; }
        public long LastRefreshTime { get; set; }
        public int DrewCount { get; set; }
        public int GameEndCountToday { get; set; }
        public long LastRefreshEndCountTime { get; set; }
    }

    public class EndlessChallengeDataClient
    {
        public int ActivityId { get; set; }
        public int MaxUnlockDifficulty { get; set; }
        public int TodayGameCount { get; set; }
        public long LastGameTime { get; set; }
    }

    public static class DataUtil
    {
        public static void Reset(this ActivityLuckyStar luckyStar, int activityId)
        {
            luckyStar.ActivityId = activityId;
            luckyStar.Sequence = 0;
            luckyStar.Cycle = 0;
            luckyStar.Free = false;
        }

        public static ActivityLuckyStar DefaultLuckyStarData(long playerId, short shardId, int activityId)
        {
            return new ActivityLuckyStar()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                Sequence = 0,
                Cycle = 0,
                Free = false
            };
        }

        public static LuckyStarDataClient ToClientApi(this ActivityLuckyStar luckyStar)
        {
            return new LuckyStarDataClient()
            {
                ActivityId = luckyStar.ActivityId,
                Sequence = luckyStar.Sequence,
                Cycle = luckyStar.Cycle,
                Free = luckyStar.Free
            };
        }

        public static void Reset(this UserFortuneBagInfo fortuneBag, int activityId)
        {
            fortuneBag.ActivityId = activityId;
            fortuneBag.FortuneBags = new List<FortuneBagAcquireInfo>();
            fortuneBag.FortuneLevelRewardClaimStatus = 0;
        }

        public static UserFortuneBagInfo DefaultFortuneBagData(long playerId, short shardId, int activityId)
        {
            return new UserFortuneBagInfo()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                FortuneBags = new List<FortuneBagAcquireInfo>(),
                FortuneLevelRewardClaimStatus = 0,
            };
        }

        public static void Reset(this ActivityPiggyBank piggyBank)
        {
            piggyBank.ClaimStatus = [0, 0];
            piggyBank.Exp = 0;
            piggyBank.PaidLevel = 0;
        }

        public static ActivityPiggyBank DefaultPiggyBankData(long playerId, short shardId)
        {
            return new ActivityPiggyBank()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ClaimStatus = [0, 0],
                Exp = 0,
                PaidLevel = 0
            };
        }

        public static PiggyBankDataClient ToClientApi(this ActivityPiggyBank luckyBank)
        {
            return new PiggyBankDataClient()
            {
                PaidLevel = luckyBank.PaidLevel,
                ClaimStatus = luckyBank.ClaimStatus,
                Exp = luckyBank.Exp,
            };
        }

        public static ActivityUnrivaledGod DefaultUnrivaledGodData(long playerId, short shardId, int activityId)
        {
            return new ActivityUnrivaledGod()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                KeyCount = 0,
                GuaranteeProgress = 0,
                ScorePoint = 0,
                ExchangeRecord = new(),
                TaskRecord = new(),
            };
        }

        public static ActivitySlotMachine DefaultSlotMachineData(long playerId, short shardId, int activityId)
        {
            return new ActivitySlotMachine()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                LastDrawTime = 0,
                TodayDrawCount = 0,
                ActivityPoint = 0,
                PointRewardClaimStatus = 0,
                RewardsInSlot = new(),
                RerollCounts = new(),
                GuaranteeProgressList = new(),
                RewardDoubledUpItemCount = new(),
            };
        }

        public static ActivityOneShotKill DefaultOneShotKillData(long playerId, short shardId, int activityId)
        {
            return new ActivityOneShotKill()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                MapCompleteRewardClaimStatus = 0,
                MapConquerRewardClaimTimestamp = new(),
                TaskCompleteRewardClaimStatus = 0,
                OneShotKillUltimateRewardClaimStatus = false,
                NormalVictoryCount = 0,
                NormalVictoryUpdateTimestamp = 0,
                ChallengeVictoryCount = 0,
                ChallengeVictoryUpdateTimestamp = 0,
            };
        }

        public static ActivityTreasureHunt DefaultTreasureHuntData(long playerId, short shardId, int activityId)
        {
            return new ActivityTreasureHunt()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                LastRefreshTime = 0,
                TodayRefreshCount = 0,
                ScorePoints = 0,
                KeyCount = 0,
                ScoreRewardClaimStatus = 0,
                RewardSlots = new(),
            };
        }

        public static ActivityRpgGame DefaultRpgGameData(long playerId, short shardId, int activityId)
        {
            return new ActivityRpgGame()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                LastGameCountRecordTime = 0,
                TodayGameCount = 0,
                LevelPassedStatus = 0,
            };
        }

        public static UnrivaledGodDataClient ToClientApi(this ActivityUnrivaledGod unrivaledGod)
        {
            return new UnrivaledGodDataClient()
            {
                ActivityId = unrivaledGod.ActivityId,
                KeyCount = unrivaledGod.KeyCount,
                ScorePoint = unrivaledGod.ScorePoint,
                GuaranteeProgress = unrivaledGod.GuaranteeProgress,
                ExchangeRecord = unrivaledGod.ExchangeRecord,
                TaskRecord = unrivaledGod.TaskRecord,
            };
        }

        public static ActivityCoopBossInfo DefaultCoopBossData(long playerId, short shardId, int activityId)
        {
            return new ActivityCoopBossInfo()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                DrewCount = 0,
                LastLevel = -1,
                LastRefreshTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RefreshCountToday = 0,
                LastLevelActivateTime = 0,
                GameEndCountToday = 0,
            };
        }

        public static ActivityTreasureMaze DefaultTreasureMazeData(long playerId, short shardId, int activityId, long startTimestamp)
        {
            return new ActivityTreasureMaze()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                GameKeyCount = 0,
                LastGameKeyTimestamp = startTimestamp,
                AwayGameCountToday = 0,
                LastAwayGameTimestamp = startTimestamp,
                LevelPassed = new List<int>(),
            };
        }

        public static CoopBossDataClient ToClientApi(this ActivityCoopBossInfo coopBossInfo)
        {
            return new CoopBossDataClient()
            {
                ActivityId = coopBossInfo.ActivityId,
                DrewCount = coopBossInfo.DrewCount,
                LastLevel = coopBossInfo.LastLevel,
                LastRefreshTime = coopBossInfo.LastRefreshTime,
                RefreshCountToday = coopBossInfo.RefreshCountToday,
                LastLevelActivateTime = coopBossInfo.LastLevelActivateTime,
                GameEndCountToday = coopBossInfo.GameEndCountToday,
                // LastRefreshEndCountTime = coopBossInfo.LastRefreshEndCountTime,
                // 保留旧的API属性，但不再使用
                LastRefreshEndCountTime = 0,
            };
        }

        public static TreasureMazeDataClient ToClientApi(this ActivityTreasureMaze treasureMazeInfo)
        {
            return new TreasureMazeDataClient()
            {
                ActivityId = treasureMazeInfo.ActivityId,
                KeyCount = treasureMazeInfo.GameKeyCount,
                AwayGameCountToday = treasureMazeInfo.AwayGameCountToday,
                DifficultyPassed = treasureMazeInfo.LevelPassed
            };
        }

        public static ActivityEndlessChallenge DefaultEndlessChallengeData(long playerId, short shardId, int activityId)
        {
            return new ActivityEndlessChallenge()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                MaxUnlockDifficulty = 0,
                TodayGameCount = 0,
                LastGameTime = 0,
            };
        }

        public static EndlessChallengeDataClient ToClientApi(this ActivityEndlessChallenge endlessChallenge)
        {
            return new EndlessChallengeDataClient()
            {
                ActivityId = endlessChallenge.ActivityId,
                MaxUnlockDifficulty = endlessChallenge.MaxUnlockDifficulty,
                TodayGameCount = endlessChallenge.TodayGameCount,
                LastGameTime = endlessChallenge.LastGameTime,
            };
        }

        public static SlotMachineDataClient ToClientApi(this ActivitySlotMachine slotMachine)
        {
            return new SlotMachineDataClient()
            {
                ActivityId = slotMachine.ActivityId,
                DrawTimeToday = slotMachine.TodayDrawCount,
                ActivityPoint  = slotMachine.ActivityPoint,
                PointRewardClaimStatus = slotMachine.PointRewardClaimStatus,
                RewardsInSlot = slotMachine.RewardsInSlot,
                RerollCounts = slotMachine.RerollCounts,
                ProgressToNextTopPrize = slotMachine.GuaranteeProgressList.Count > 3 ?
                    slotMachine.GuaranteeProgressList[3] : 0,
                DoubleUpCounts = slotMachine.RewardDoubledUpItemCount,
            };
        }

        public static OneShotKillDataClient ToClientApi(this ActivityOneShotKill oneShotKill,
            List<int> oneShotKillMapProgress, List<int> oneShotKillTaskProgress,
            List<int> oneShotKillProgressAddLastHour, List<int> oneShotKillTaskProgressLoseLastHour,
            HashEntry[] oneShotKillEvent)
        {
            return new OneShotKillDataClient()
            {
                ActivityId = oneShotKill.ActivityId,
                MapProgress = oneShotKillMapProgress,
                TaskProgress = oneShotKillTaskProgress,
                ProgressAddLastHour = oneShotKillProgressAddLastHour,
                ProgressLoseLastHour = oneShotKillTaskProgressLoseLastHour,
                MapCompleteRewardClaimStatus = oneShotKill.MapCompleteRewardClaimStatus,
                MapConquerRewardClaimTimestamp = oneShotKill.MapConquerRewardClaimTimestamp,
                TaskCompleteRewardClaimStatus = oneShotKill.TaskCompleteRewardClaimStatus,
                UltimateRewardClaimStatus = oneShotKill.OneShotKillUltimateRewardClaimStatus,
                NormalVictoryToday = oneShotKill.NormalVictoryCount,
                ChallengeVictoryToday = oneShotKill.ChallengeVictoryCount,
                EventList = oneShotKillEvent.Select(
                    item => new OneShotKillEvent()
                    {
                        EventHash = (int)item.Name,
                        Timestamp = (long)item.Value,
                    }).ToList(),
            };
        }

        public static TreasureHuntDataClient ToClientApi(this ActivityTreasureHunt treasureHunt)
        {
            return new TreasureHuntDataClient()
            {
                ActivityId = treasureHunt.ActivityId,
                ScorePoints = treasureHunt.ScorePoints,
                KeyCount = treasureHunt.KeyCount,
                ScoreRewardClaimStatus = treasureHunt.ScoreRewardClaimStatus,
                TodayRefreshCount = treasureHunt.TodayRefreshCount,
                LastRefreshTime = treasureHunt.LastRefreshTime,
                RewardSlots = treasureHunt.RewardSlots,
            };
        }

        public static RpgGameDataClient ToClientApi(this ActivityRpgGame rpgGameData)
        {
            return new RpgGameDataClient()
            {
                ActivityId = rpgGameData.ActivityId,
                GameCountToday = rpgGameData.TodayGameCount,
                LevelPassed = rpgGameData.LevelPassedStatus,
            };
        }

        public static ActivityLoogGame DefaultLoogGameData(long playerId, short shardId, int activityId)
        {
            return new ActivityLoogGame()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                LastGameCountRecordTime = 0,
                TodayGameCount = 0,
                LevelPassedStatus = 0,
            };
        }

        public static LoogGameDataClient ToClientApi(this ActivityLoogGame loogGameData)
        {
            return new LoogGameDataClient()
            {
                ActivityId = loogGameData.ActivityId,
                GameCountToday = loogGameData.TodayGameCount,
                LevelPassed = loogGameData.LevelPassedStatus,
            };
        }

        public static ActivityCsgoStyleLottery DefaultCsgoStyleLotteryData(long playerId, short shardId, int activityId)
        {
            return new ActivityCsgoStyleLottery()
            {
                PlayerId = playerId,
                ShardId = shardId,
                ActivityId = activityId,
                ActivityPoint = 0,
                PointRewardClaimStatus = 0,
                KeyCount = 0,
                KeyPurchaseCountByDiamond = 0,
                ActivityPremiumPassStatus = 0,
                PremiumPassDailyRewardClaimStatus = [],
                RewardRecord = [],
                TaskRecord = new(),
            };
        }

        public static CsgoStyleLotteryDataClient ToClientApi(this ActivityCsgoStyleLottery csgoLottery,
            UserAssets userAsset)
        {
            var currentTime = TimeUtils.GetCurrentTime();
            int dayDiff = TimeUtils.GetDayDiffBetween(currentTime, csgoLottery.LotteryDrawInfoRefreshTimestamp,
                userAsset.TimeZoneOffset, 0);
            int diamondPurchaseCountByDay = dayDiff > 0 ? 0 : csgoLottery.KeyPurchaseCountByDiamond;
            int totalLotteryDrawToday = dayDiff > 0 ? 0 : csgoLottery.TotalLotteryDrawToday;
            
            return new CsgoStyleLotteryDataClient()
            {
                ActivityId = csgoLottery.ActivityId,
                ActivityPoint = csgoLottery.ActivityPoint,
                PointRewardClaimStatus = csgoLottery.PointRewardClaimStatus,
                KeyCount = csgoLottery.KeyCount,
                KeyPurchaseCountByDiamond = diamondPurchaseCountByDay,
                TotalCsgoLotteryDrawToday = totalLotteryDrawToday,
                ActivityPremiumPassStatus = csgoLottery.ActivityPremiumPassStatus,
                PremiumPassDailyRewardClaimStatus = csgoLottery.PremiumPassDailyRewardClaimStatus,
                RewardRecord = csgoLottery.RewardRecord,
                RewardRecordTime = csgoLottery.RewardRecordTime,
                TaskRecord = csgoLottery.TaskRecord,
            };
        }
    }
}