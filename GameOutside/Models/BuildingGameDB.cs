using AssistActivity.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;
using GameOutside.Util;

// ReSharper disable InconsistentNaming
namespace GameOutside.DBContext;

public class BuildingGameDB(DbContextOptions options) : DBBase(options)
{
    public DbSet<PlayerAssistActivityInfo> PlayerAssistActivityInfos { get; set; }
    public DbSet<PlatformNotify> PlatformNotifies { get; set; }
    public DbSet<PaidOrderWithShard> PaidOrderWithShards { get; set; }
    public DbSet<UserRank> UserRanks { get; set; }
    public DbSet<UserEndlessRank> UserEndlessRanks { get; set; }
    public DbSet<UserDivision> UserDivisions { get; set; }
    public DbSet<UserInfo> UserInfos { get; set; }
    public DbSet<UserAssets> UserAssets { get; set; }
    public DbSet<UserItem> UserItems { get; set; }
    public DbSet<UserCard> UserCards { get; set; }
    public DbSet<UserTreasureBox> UserTreasureBoxes { get; set; }
    public DbSet<UserGameInfo> UserGameInfos { get; set; }
    public DbSet<SocInfo> SocInfos { get; set; }
    public DbSet<SeasonRefreshedHistory> SeasonRefreshedHistories { get; set; }
    public DbSet<UserCustomData> UserCustomData { get; set; }
    public DbSet<UserIdleRewardInfo> UserIdleRewardInfos { get; set; }

    // 外键引用，不定义取不到
    public DbSet<UserHistory> UserHistories { get; set; }
    public DbSet<UserDailyStoreItem> UserDailyStoreItems { get; set; }
    public DbSet<UserDailyStoreIndex> UserDailyStoreIndices { get; set; }
    public DbSet<UserCommodityBoughtRecord> UserCommodityBoughtRecords { get; set; }
    public DbSet<UserAttendance> UserAttendances { get; set; }
    public DbSet<UserBattlePassInfo> UserBattlePassInfos { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }
    public DbSet<UserBeginnerTask> UserBeginnerTasks { get; set; }
    public DbSet<UserDailyTask> UserDailyTasks { get; set; }

    public DbSet<UserMallAdvertisement> UserMallAdvertisements { get; set; }
    public DbSet<UserGlobalInfo> UserGlobalInfos { get; set; }
    public DbSet<UserCustomCardPool> UserCustomCardPools { get; set; }

    public DbSet<UserFixedLevelMapProgress> UserFixedLevelMapProgress { get; set; }

    public DbSet<UserIapPurchaseRecord> UserIapPurchases { get; set; }

    public DbSet<ActivityLuckyStar> ActivityLuckyStars { get; set; }

    public DbSet<UserPaymentAndPromotionStatus> PromotionStatus { get; set; }

    public DbSet<UserStarStoreInfo> UserStarStoreStatus { get; set; }
    public DbSet<ActivityPiggyBank> ActivityPiggyBanks { get; set; }
    public DbSet<ActivityUnrivaledGod> ActivityUnrivaledGods { get; set; }
    public DbSet<UserFortuneBagInfo> UserFortuneBagInfos { get; set; }
    public DbSet<ActivityTreasureMaze> ActivityTreasureMazeInfos { get; set; }
    public DbSet<ActivityCsgoStyleLottery> ActivityCsgoStyleLotteryInfos { get; set; }

    public DbSet<ActivitySlotMachine> ActivitySlotMachines { get; set; }

    public DbSet<ActivityOneShotKill> ActivityOneShotKills { get; set; }

    public DbSet<ActivityTreasureHunt> ActivityTreasureHunts { get; set; }

    public DbSet<ActivityRpgGame> ActivityRpgGames { get; set; }
    public DbSet<ActivityLoogGame> ActivityLoogGames { get; set; }

    public DbSet<MonthPassInfo> UserMonthPassInfos { get; set; }

    public DbSet<ServerData> ServerDataset { get; set; }

    public DbSet<ActivityCoopBossInfo> ActivityCoopBossInfoSet { get; set; }
    public DbSet<ActivityEndlessChallenge> ActivityEndlessChallenges { get; set; }
    public DbSet<UserDailyTreasureBoxProgress> UserDailyTreasureBoxProgresses { get; set; }
    public DbSet<UserH5FriendActivityInfo> UserH5FriendActivityInfos { get; set; }
    public DbSet<RedisLuaScript> RedisLuaScripts { get; set; }
    public DbSet<LocalRedisLuaScript> LocalRedisLuaScripts { get; set; }
    public DbSet<UserEncryptionInfo> UserEncryptionInfos { get; set; }
    public DbSet<PlayerPunishmentTask> PlayerPunishmentTasks { get; set; }

    /// <summary>
    /// 因业务调整，这个东西已过期，不应该再访问了
    /// </summary>
    [Obsolete]
    public DbSet<InvitationCodeClaimRecord> InvitationCodeClaimRecords { get; set; }
    public DbSet<IosGameCenterRewardInfo> IosGameCenterRewardInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        this.ConfigurePlatformNotifies(modelBuilder);
        this.ConfigurePaidOrdersWithShard(modelBuilder);

        modelBuilder.Entity<UserRank>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.SeasonNumber });
            builder.HasIndex(m => new { m.SeasonNumber, m.ShardId });
            builder.HasIndex(m => new { m.SeasonNumber, m.Division, m.GroupId, m.ShardId });
            builder.HasIndex(m => new { m.HighestScore, m.Timestamp }).IsDescending(true, false);
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });

        modelBuilder.Entity<UserDivision>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });

        modelBuilder.Entity<UserEndlessRank>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.SeasonNumber });
            builder.HasIndex(m => new { m.SeasonNumber, m.ShardId });
        });

        modelBuilder.Entity<UserInfo>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property<string>(m => m.Signature).HasMaxLength(255);
            builder.HasMany<UserHistory>(m => m.Histories)
                .WithOne()
                .HasPrincipalKey(m => new { m.PlayerId, m.ShardId })
                .HasForeignKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
            builder.Property(m => m.WorldRankHistories).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.WorldRankSeasonHistories).HasDefaultValueSql("ARRAY[]::integer[]");
        });

        modelBuilder.Entity<UserAssets>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.OwnsOne(m => m.LevelData, d =>
            {
                d.Property(m => m.RewardStatusList).HasDefaultValueSql("ARRAY[]::bigint[]");
            });
            builder.OwnsOne(m => m.DifficultyData, d =>
            {
                d.Property(m => m.Levels).HasDefaultValueSql("ARRAY[]::integer[]");
                d.Property(m => m.Stars).HasDefaultValueSql("ARRAY[]::integer[]");
            });
            builder.Property(m => m.TimeZoneOffset).HasDefaultValue(0);
            builder.HasMany<UserItem>(m => m.UserItems)
                .WithOne()
                .HasPrincipalKey(m => new { m.PlayerId, m.ShardId })
                .HasForeignKey(m => new { m.PlayerId, m.ShardId });
            builder.HasMany<UserCard>(m => m.UserCards)
                .WithOne()
                .HasPrincipalKey(m => new { m.PlayerId, m.ShardId })
                .HasForeignKey(m => new { m.PlayerId, m.ShardId });
            builder.HasMany<UserTreasureBox>(m => m.UserTreasureBoxes)
                .WithOne()
                .HasPrincipalKey(m => new { m.PlayerId, m.ShardId })
                .HasForeignKey(m => new { m.PlayerId, m.ShardId });
        });

        modelBuilder.Entity<UserIdleRewardInfo>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
        });

        modelBuilder.Entity<UserItem>().HasKey(u => new { u.PlayerId, u.ItemId, u.ShardId });
        modelBuilder.Entity<UserCard>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(u => new { u.PlayerId, u.CardId, u.ShardId });
        });

        modelBuilder.Entity<UserTreasureBox>(builder =>
        {
            builder.Property(u => u.Id).HasDefaultValueSql<Guid>("gen_random_uuid()");
            builder.HasKey(u => new { u.Id, u.ShardId });
        });

        modelBuilder.Entity<UserGameInfo>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(m => m.DrawCardCountList).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.DrawNewCardCountList).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.GeneralCardCountList).HasDefaultValueSql("ARRAY[]::integer[]");
        });
        modelBuilder.Entity<SocInfo>().HasKey(r => r.DeviceName);
        modelBuilder.Entity<SeasonRefreshedHistory>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => m.SeasonNumber);
        });

        modelBuilder.Entity<UserCustomData>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(m => m.IntData).HasMaxLength(Consts.MaxCustomUserIntDataLength);
            builder.Property(m => m.StrData).HasMaxLength(Consts.MaxCustomUserStringDataLength);
        });

        modelBuilder.Entity<UserHistory>(builder =>
        {
            builder.HasKey(m => new { m.Id, m.ShardId });
            builder.HasIndex(uh => uh.PlayerId); // TODO: 重复了
            builder.Property(m => m.Id).HasDefaultValueSql<Guid>("gen_random_uuid()");
            builder.Property(uh => uh.DynamicInfo).HasDefaultValue("{}");
            builder.Property(uh => uh.ResourceRecords).HasDefaultValueSql("'[]'::jsonb");
            builder.Property(uh => uh.FightUnitInfos).HasDefaultValueSql("'[]'::jsonb");
        });

        modelBuilder.Entity<UserDailyStoreItem>(builder =>
        {
            builder.HasKey(u => new { u.PlayerId, u.ItemId, u.ShardId });
        });

        modelBuilder.Entity<UserDailyStoreIndex>().HasKey(r => new { r.PlayerId, r.ShardId });
        modelBuilder.Entity<UserCommodityBoughtRecord>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(u => new { u.PlayerId, u.CommodityId, u.ShardId });
        });
        modelBuilder.Entity<UserAttendance>().HasKey(u => new { u.PlayerId, u.ShardId });
        modelBuilder.Entity<UserBattlePassInfo>().HasKey(u => new { u.PlayerId, u.PassId, u.ShardId });
        modelBuilder.Entity<UserAchievement>().HasKey(m => new { m.PlayerId, m.ConfigId, m.Target, m.ShardId });

        modelBuilder.Entity<UserBeginnerTask>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(task => task.TaskList).HasDefaultValueSql("'[]'::jsonb");
        });

        modelBuilder.Entity<UserMallAdvertisement>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.Id, m.ShardId });
            builder.Property(m => m.Count).HasDefaultValue(0);
            builder.Property(m => m.LastTime).HasDefaultValue(0);
        });

        modelBuilder.Entity<UserGlobalInfo>().HasKey(u => u.UserId);

        modelBuilder.Entity<UserCustomCardPool>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.HeroId });
        });

        modelBuilder.Entity<UserFixedLevelMapProgress>().HasKey(r => new { r.PlayerId, r.MapId, r.ShardId });
        modelBuilder.Entity<ActivityLuckyStar>().HasKey(m => new { m.PlayerId, m.ShardId });

        modelBuilder.Entity<UserIapPurchaseRecord>(builder =>
        {
            builder.HasKey(m => new { m.Id, m.ShardId });
            builder.HasIndex(record => new
            {
                record.PlayerId,
                record.ShardId,
            });
            builder.Property(m => m.Id).HasDefaultValueSql<Guid>("gen_random_uuid()");
        });
        modelBuilder.Entity<UserStarStoreInfo>().HasKey(m => new { m.PlayerId, m.ShardId });

        modelBuilder.Entity<UserFortuneBagInfo>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(info => info.FortuneBags).HasDefaultValueSql("'[]'::jsonb");
        });

        modelBuilder.Entity<ServerData>(builder =>
        {
            builder.HasKey(m => new { m.Id });
        });

        modelBuilder.Entity<ActivityPiggyBank>().HasKey(m => new { m.PlayerId, m.ShardId });
        modelBuilder.Entity<ActivityUnrivaledGod>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
            builder.Property(m => m.TaskRecord).HasDefaultValueSql("'{}'::jsonb");
            builder.Property(m => m.ExchangeRecord).HasDefaultValueSql("'{}'::jsonb");
        });

        modelBuilder.Entity<ActivityTreasureMaze>().HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });

        modelBuilder.Entity<ActivityCoopBossInfo>().HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
        modelBuilder.Entity<MonthPassInfo>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
        });
        modelBuilder.Entity<UserDailyTask>().HasKey(m => new { m.PlayerId, m.ShardId });

        modelBuilder.Entity<UserPaymentAndPromotionStatus>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
            builder.Property(m => m.DoubleDiamondBonusTriggerRecords).HasDefaultValueSql("'{}'::jsonb");
        });

        modelBuilder.Entity<RedisLuaScript>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => m.Name);
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });
        modelBuilder.Entity<LocalRedisLuaScript>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.Name, m.ShardId });
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });
        modelBuilder.Entity<UserEncryptionInfo>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
        });
        modelBuilder.Entity<ActivitySlotMachine>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
            builder.Property(m => m.RewardDoubledUpItemCount).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.RewardsInSlot).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.RerollCounts).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.GuaranteeProgressList).HasDefaultValueSql("ARRAY[]::integer[]");
        });
        modelBuilder.Entity<ActivityOneShotKill>(builder =>
        {
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
            builder.Property(m => m.MapConquerRewardClaimTimestamp).HasDefaultValueSql("ARRAY[]::bigint[]");
        });

        modelBuilder.Entity<UserH5FriendActivityInfo>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
        });
        modelBuilder.Entity<InvitationCodeClaimRecord>(builder =>
        {
            builder.HasKey(m => m.GiftCode);
        });
        modelBuilder.Entity<IosGameCenterRewardInfo>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId });
        });
        modelBuilder.Entity<PlayerAssistActivityInfo>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.DistroId });
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });
        modelBuilder.Entity<ActivityTreasureHunt>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
            builder.Property(m => m.RewardSlots).HasDefaultValueSql("'[]'::jsonb");
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });
        modelBuilder.Entity<ActivityRpgGame>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });
        modelBuilder.Entity<ActivityLoogGame>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ShardId, m.ActivityId });
            builder.Property(m => m.UpdatedAt).IsConcurrencyToken();
        });
        modelBuilder.Entity<PlayerPunishmentTask>(builder =>
        {
            SetCommonEntitiesNoSoftDelete(builder);
            builder.HasKey(m => new { m.PlayerId, m.TaskId, m.ShardId });
            builder.Property(m => m.TaskId).HasMaxLength(128);
        });

        modelBuilder.Entity<ActivityCsgoStyleLottery>(builder =>
        {
            SetCommonEntities(builder);
            builder.HasKey(m => new { m.PlayerId, m.ActivityId, m.ShardId });
            builder.Property(m => m.TaskRecord).HasDefaultValueSql("'{}'::jsonb");
            builder.Property(m => m.PremiumPassDailyRewardClaimStatus).HasDefaultValueSql("ARRAY[]::bigint[]");
            builder.Property(m => m.RewardRecord).HasDefaultValueSql("ARRAY[]::integer[]");
            builder.Property(m => m.RewardRecordTime).HasDefaultValueSql("ARRAY[]::bigint[]");
        });
    }

    #region UserAssets

    public ValueTask<UserIdleRewardInfo?> GetUserIdleRewardInfo(short? shardId, long playerId)
    {
        return this.WithDefaultRetry(_ => UserIdleRewardInfos.FirstOrDefaultAsync(u =>
            shardId.HasValue ? u.ShardId == shardId && u.PlayerId == playerId : u.PlayerId == playerId));
    }

    public async Task<UserIdleRewardInfo[]> BatchGetUserIdleRewardInfo(List<long> playerIds)
    {
        return await UserIdleRewardInfos
            .Where(u => playerIds.Contains(u.PlayerId))
            .ToArrayAsync();
    }

    public UserIdleRewardInfo AddUserIdleRewardInfo(short shardId, long playerId)
    {
        var idleRewardInfo = new UserIdleRewardInfo()
        {
            ShardId = shardId,
            PlayerId = playerId,
            StartTime = 0,
            IdleRewardId = 0,
            StolenRecords = new List<long>(),
        };
        UserIdleRewardInfos.Add(idleRewardInfo);
        return idleRewardInfo;
    }

    #endregion

    #region UserSocInfo

    public async Task SaveDeviceName(string deviceName)
    {
        var scoInfo = await SocInfos.FindAsync(deviceName);
        if (scoInfo != null)
            return;
        await SocInfos.AddAsync(new SocInfo() { DeviceName = deviceName });
    }

    #endregion

    #region UserData

    public async Task<UserCustomData?> GetUserDataAsync(short shardId, long playerId)
    {
        return await UserCustomData.FindAsync(playerId, shardId);
    }

    private UserCustomData CreateNewUserCustomData(short shardId, long playerId)
    {
        return new UserCustomData() { ShardId = shardId, PlayerId = playerId, IntData = "", StrData = "" };
    }

    public async Task SetUserIntData(short shardId, long playerId, string data)
    {
        var userData = await GetUserDataAsync(shardId, playerId);
        if (userData == null)
        {
            userData = CreateNewUserCustomData(shardId, playerId);
            userData.IntData = data;
            UserCustomData.Add(userData);
        }
        else
        {
            userData.IntData = data;
        }
    }

    public async Task SetUserStrData(short shardId, long playerId, string data)
    {
        var userData = await UserCustomData.FindAsync(playerId, shardId);
        if (userData == null)
        {
            userData = CreateNewUserCustomData(shardId, playerId);
            userData.StrData = data;
            UserCustomData.Add(userData);
        }
        else
        {
            userData.StrData = data;
        }
    }

    #endregion

    #region DailyStore

    public void DeleteAllDailyStoreItems(short shardId, long playerId)
    {
        var query = UserDailyStoreItems.Where(u => u.PlayerId == playerId && u.ShardId == shardId);
        UserDailyStoreItems.RemoveRange(query);
    }

    public ValueTask<UserDailyStoreItem?> GetDailyStoreItem(short shardId, long playerId, int itemId)
    {
        return this.WithDefaultRetry(_ =>
            UserDailyStoreItems.FindAsync(playerId, itemId, shardId));
    }

    public void AddDailyStoreItems(List<UserDailyStoreItem> itemList)
    {
        UserDailyStoreItems.AddRange(itemList);
    }

    public ValueTask<List<UserDailyStoreItem>> GetUserDailyStoreItems(short shardId, long playerId)
    {
        return this.WithDefaultRetry(_ =>
            UserDailyStoreItems.Where(u => u.PlayerId == playerId && u.ShardId == shardId).ToListAsync());
    }

    public ValueTask<UserDailyStoreIndex?> GetUserDailyStoreIndex(short shardId, long playerId)
    {
        return this.WithDefaultRetry(_ =>
            UserDailyStoreIndices.FindAsync(playerId, shardId));
    }

    public void AddUserDailyStoreIndex(UserDailyStoreIndex userDailyStoreIndex)
    {
        UserDailyStoreIndices.Add(userDailyStoreIndex);
    }

    #endregion

    #region StoreRecord

    public async Task RecordCommodityBoughtBy(short shardId, long playerId, int commodityId, int count)
    {
        var commodityInfo = await UserCommodityBoughtRecords.FindAsync(playerId, commodityId, shardId);
        if (commodityInfo == null)
        {
            commodityInfo = new UserCommodityBoughtRecord()
            {
                ShardId = shardId,
                CommodityId = commodityId,
                Count = count,
                PlayerId = playerId
            };
            await UserCommodityBoughtRecords.AddAsync(commodityInfo);
            return;
        }

        commodityInfo.Count += count;
    }

    public async Task<int> GetCommodityBoughtCount(short shardId, long playerId, int commodityId)
    {
        var commodityInfo = await UserCommodityBoughtRecords.FindAsync(playerId, commodityId, shardId);
        return commodityInfo?.Count ?? 0;
    }

    public ValueTask<List<UserCommodityBoughtRecord>> GetAllBoughtCommodityRecords(short shardId, long playerId)
    {
        return this.WithDefaultRetry(_ => UserCommodityBoughtRecords
            .Where(x => x.PlayerId == playerId && x.ShardId == shardId)
            .ToListAsync());
    }

    #endregion

    #region Attendance

    public UserAttendance CreateUserAttendanceRecord(short shardId, long playerId, long timestamp)
    {
        var userAttendance = new UserAttendance
        {
            ShardId = shardId,
            PlayerId = playerId,
            RewardIndex = 0,
            CreateTime = timestamp,
            TotalLoginDays = 0,
            LastLoginDate = 0
        };
        UserAttendances.Add(userAttendance);
        return userAttendance;
    }

    public async Task<UserAttendance?> GetUserAttendanceRecord(short shardId, long playerId)
    {
        return await UserAttendances.FindAsync(playerId, shardId);
    }

    #endregion

    #region 新手任务

    public ValueTask<UserBeginnerTask?> GetBeginnerTaskAsync(short shardId, long playerId)
    {
        return this.WithDefaultRetry(_ => UserBeginnerTasks.FindAsync(playerId, shardId));
    }

    public void AddBeginnerTask(UserBeginnerTask beginnerTask)
    {
        UserBeginnerTasks.Add(beginnerTask);
    }

    public async Task<UserBeginnerTask?> UpdateBeginnerTaskProgress(
        ServerConfigService serverConfigService,
        List<TaskRecord> taskRecords,
        short shardId,
        long playerId,
        int timeZoneOffset)
    {
        var userTaskData = await GetBeginnerTaskAsync(shardId, playerId);
        if (userTaskData == null)
            return null;

        // 检查时间是否到了目标时间
        serverConfigService.TryGetParameterInt(Params.TaskUpdateTimeOffset, out int offset);
        var currentTime = TimeUtils.GetCurrentTime();
        int dayOffset = TimeUtils.GetDayDiffBetween(currentTime, userTaskData.StartTime, timeZoneOffset, offset);
        if (dayOffset < userTaskData.DayIndex)
            return userTaskData;

        // 更新任务进度
        var recordDict = taskRecords.ToDictionary(record => record.Key);
        var taskList = userTaskData.TaskList;
        foreach (var task in taskList)
        {
            var config = serverConfigService.GetBeginnerTaskConfig(task.Id);
            if (config == null)
                continue;
            if (task.Progress >= config.target_progress)
                continue;
            var taskKey = $"{config.key}|{config.target_key}";
            if (recordDict.TryGetValue(taskKey, out var record))
            {
                task.Progress += record.Count;
                this.Entry(userTaskData).Property(t => t.TaskList).IsModified = true;
            }
        }

        return userTaskData;
    }

    #endregion

    #region 每日任务

    public ValueTask<UserDailyTask?> GetDailyTask(short shardId, long playerId)
    {
        return this.WithDefaultRetry(_ => UserDailyTasks.FindAsync(playerId, shardId));
    }

    public async Task<(UserDailyTask, bool)> AddDailyTaskProgress(
        ServerConfigService serverConfigService,
        DailyTaskType taskType,
        int addAmount,
        short shardId,
        long playerId,
        int timeZoneOffset)
    {
        bool dataChanged = false;
        UserDailyTask? dailyTaskStatus = await GetDailyTask(shardId, playerId);
        var taskList = serverConfigService.GetDailyTaskList();
        if (dailyTaskStatus == null)
        {
            dailyTaskStatus = new UserDailyTask()
            {
                PlayerId = playerId,
                ShardId = shardId,
                TaskRefreshTime = TimeUtils.GetCurrentTime(),
                TaskProgress = Enumerable.Repeat(0, taskList.Count).ToList(),
                DailyTaskRewardClaimStatus = 0,
                ActiveScoreRewardClaimStatus = 0,
            };

            UserDailyTasks.Add(dailyTaskStatus);
            dataChanged = true;
        }

        if (TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), dailyTaskStatus.TaskRefreshTime, timeZoneOffset,
                0) != 0)
        {
            dailyTaskStatus.TaskRefreshTime = TimeUtils.GetCurrentTime();
            dailyTaskStatus.TaskProgress = Enumerable.Repeat(0, taskList.Count).ToList();
            dailyTaskStatus.DailyTaskRewardClaimStatus = 0;
            dailyTaskStatus.ActiveScoreRewardClaimStatus = 0;
            dataChanged |= true;
        }

        for (int i = 0; i < taskList.Count; i++)
        {
            if (taskList[i].daily_task_type == taskType)
            {
                if (dailyTaskStatus.TaskProgress[i] < taskList[i].target_progress)
                {
                    dailyTaskStatus.TaskProgress[i] += addAmount;
                    dataChanged |= true;
                }
                break;
            }
        }

        return (dailyTaskStatus, dataChanged);
    }

    public async Task<(bool, UserH5FriendActivityInfo)> GetOrCreateUserH5FriendActivityInfo(long playerId, short shardId, int openingLevel)
    {
        var result = await this.WithDefaultRetry(_ => UserH5FriendActivityInfos.FindAsync(playerId, shardId));
        if (result != null)
            return (false, result);
        result = new UserH5FriendActivityInfo()
        {
            PlayerId = playerId,
            ShardId = shardId,
            NextShowingLevelV1 = openingLevel
        };
        UserH5FriendActivityInfos.Add(result);
        return (true, result);
    }

    #endregion

    #region 广告

    public async Task<List<UserMallAdvertisement>> ListUserMallAdStatus(short shardId, long playerId)
    {
        return await this.WithDefaultRetry(_ =>
        {
            return UserMallAdvertisements.AsNoTracking()
                .Where(ad => ad.PlayerId == playerId && ad.ShardId == shardId)
                .ToListAsync();
        });
    }

    public async Task<UserMallAdvertisement?> GetUserMallAdvertisement(short shardId, long playerId, int id)
    {
        return await this.WithDefaultRetry(_ => UserMallAdvertisements.FindAsync(playerId, id, shardId));
    }

    public async Task AddUserMallAdvertisement(UserMallAdvertisement advertisement)
    {
        await this.WithDefaultRetry(_ => UserMallAdvertisements.AddAsync(advertisement));
    }

    #endregion

    #region UserCustomCardPool

    public async Task<List<UserCustomCardPool>> GetAllUserCustomCardPoolsAsync(short shardId, long playerId)
    {
        return await this.WithDefaultRetry(_ =>
            UserCustomCardPools.Where(entry => entry.PlayerId == playerId && entry.ShardId == shardId).ToListAsync());
    }

    public async Task<UserCustomCardPool?> GetUserCustomCardPoolAsync(short shardId, long playerId, int heroId)
    {
        return await this.WithDefaultRetry(_ =>
            UserCustomCardPools
                .Where(entry => entry.ShardId == shardId && entry.PlayerId == playerId && entry.HeroId == heroId)
                .FirstOrDefaultAsync());
    }

    public void AddUserCustomCardPool(UserCustomCardPool pool)
    {
        UserCustomCardPools.Add(pool);
    }

    #endregion

    #region 固定关卡

    public async Task<List<UserFixedLevelMapProgress>> GetAllUserFixedLevelMapProgresses(long playerId, short shardId)
    {
        return await this.WithDefaultRetry(_ => UserFixedLevelMapProgress
            .Where(entry => entry.PlayerId == playerId && entry.ShardId == shardId)
            .ToListAsync());
    }

    public async Task<int> GetTotalStoryStarCount(long playerId, short shardId)
    {
        return await this.WithDefaultRetry(_ => UserFixedLevelMapProgress
            .Where(entry => entry.PlayerId == playerId && entry.ShardId == shardId)
            .Select(entry => entry.StarCount)
            .SumAsync());
    }

    public async Task<UserStarStoreInfo> GetOrAddUserStarRewardClaimStatusAsync(long playerId, short shardId)
    {
        var data = await UserStarStoreStatus.Where(entry => entry.PlayerId == playerId && entry.ShardId == shardId)
            .FirstOrDefaultAsync();
        if (data == null)
        {
            data = new UserStarStoreInfo()
            {
                PlayerId = playerId,
                ShardId = shardId,
                StarRewardClaimStatus = new List<long>()
            };
            UserStarStoreStatus.Add(data);
        }

        return data;
    }

    public async Task<UserFixedLevelMapProgress?> GetUserFixedLevelMapProgress(long playerId, short shardId, int mapId)
    {
        return await this.WithDefaultRetry(_ => UserFixedLevelMapProgress.FindAsync(playerId, mapId, shardId));
    }

    public async Task AddUserFixedLevelMapProgress(UserFixedLevelMapProgress fixedLevelMapProgress)
    {
        await this.WithDefaultRetry(_ => UserFixedLevelMapProgress.AddAsync(fixedLevelMapProgress));
    }

    #endregion

    // TODO: 提取模块
    #region 月卡

    public async Task<MonthPassInfo?> GetMonthPassInfo(long playerId, short? shardId)
    {
        return await this.WithDefaultRetry(_ =>
            UserMonthPassInfos.FirstOrDefaultAsync(u =>
                shardId.HasValue ? u.PlayerId == playerId && u.ShardId == shardId : u.PlayerId == playerId));
    }

    public async Task<int> GetNthDayOfMonthPass(long playerId, short shardId, int timeZoneOffset)
    {
        var monthPassInfo = await GetMonthPassInfo(playerId, shardId);
        if (monthPassInfo == null)
            return -1;
        var dayDiff = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), monthPassInfo.PassAcquireTime,
            timeZoneOffset, 0);
        return dayDiff < monthPassInfo.PassDayLength ? dayDiff : -1;
    }

    public async Task<MonthPassInfo> AddMonthPassInfo(long playerId, short shardId)
    {
        var passInfo = new MonthPassInfo()
        {
            PlayerId = playerId,
            ShardId = shardId,
            PassAcquireTime = 0,
            PassDayLength = 0,
            LastRewardClaimDay = -1,
            RewardClaimStatus = 0,
        };
        await UserMonthPassInfos.AddAsync(passInfo);
        return passInfo;
    }
    #endregion

    #region 加密信息

    public async ValueTask<(UserEncryptionInfo, bool)> GetOrAddUserEncryptionInfoAsync(long playerId, short shardId)
    {
        var data = await this.WithDefaultRetry(_ => UserEncryptionInfos.FindAsync(playerId, shardId));
        var added = false;
        if (data == null)
        {
            data = new UserEncryptionInfo()
            {
                PlayerId = playerId,
                ShardId = shardId,
                EncryptionKey = EncryptHelper.EncryptHelper.GenerateRandomString(8)
            };
            UserEncryptionInfos.Add(data);
            added = true;
        }
        return (data, added);
    }

    #endregion

    ///////////////////////////////////////////////////////  分割线    //////////////////////////////////////////////////////////////////////////

    #region gm用

    public async Task<List<UserBattlePassInfo>> GetUserBattlePassInfo(short shardId, long playerId)
    {
        return await this.WithDefaultRetry(_ => UserBattlePassInfos
            .Where(entry => entry.PlayerId == playerId && entry.ShardId == shardId)
            .ToListAsync());
    }

    #endregion
}
