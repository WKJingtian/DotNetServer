using System;
using System.Collections.Generic;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameOutside.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityCoopBossInfoSet",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    LastLevel = table.Column<int>(type: "integer", nullable: false),
                    LastLevelActivateTime = table.Column<long>(type: "bigint", nullable: false),
                    RefreshCountToday = table.Column<int>(type: "integer", nullable: false),
                    LastRefreshTime = table.Column<long>(type: "bigint", nullable: false),
                    DrewCount = table.Column<int>(type: "integer", nullable: false),
                    GameEndCountToday = table.Column<int>(type: "integer", nullable: false),
                    LastRefreshEndCountTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCoopBossInfoSet", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });

            migrationBuilder.CreateTable(
                name: "ActivityEndlessChallenges",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    MaxUnlockDifficulty = table.Column<int>(type: "integer", nullable: false),
                    TodayGameCount = table.Column<int>(type: "integer", nullable: false),
                    LastGameTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEndlessChallenges", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });

            migrationBuilder.CreateTable(
                name: "ActivityLuckyStars",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    Free = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLuckyStars", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "ActivityPiggyBanks",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    ClaimStatus = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    PaidLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityPiggyBanks", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });

            migrationBuilder.CreateTable(
                name: "ActivityUnrivaledGods",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    KeyCount = table.Column<int>(type: "integer", nullable: false),
                    GuaranteeProgress = table.Column<int>(type: "integer", nullable: false),
                    ScorePoint = table.Column<int>(type: "integer", nullable: false),
                    ExchangeRecord = table.Column<Dictionary<int, int>>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    TaskRecord = table.Column<Dictionary<string, UnrivaledGodTask>>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityUnrivaledGods", x => new { x.PlayerId, x.ShardId, x.ActivityId });
                });

            migrationBuilder.CreateTable(
                name: "PaidOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ClaimStatus = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Awards = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaidOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformNotifies",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ClaimStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformNotifies", x => new { x.PlayerId, x.Id, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "PromotionStatus",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    LastPromotedPackage = table.Column<string>(type: "text", nullable: false),
                    PackagePromotionTime = table.Column<long>(type: "bigint", nullable: false),
                    IceBreakingPayPromotion = table.Column<int>(type: "integer", nullable: false),
                    DoubleDiamondBonusTriggerRecords = table.Column<Dictionary<string, long>>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionStatus", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "RedisLuaScripts",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sha = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedisLuaScripts", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "SeasonRefreshedHistories",
                columns: table => new
                {
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RefreshedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonRefreshedHistories", x => x.SeasonNumber);
                });

            migrationBuilder.CreateTable(
                name: "ServerDataset",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerDataset", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocInfos",
                columns: table => new
                {
                    DeviceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocInfos", x => x.DeviceName);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ConfigId = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    CurrentIndex = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    Received = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => new { x.PlayerId, x.ConfigId, x.Target, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserAssets",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    CoinCount = table.Column<int>(type: "integer", nullable: false),
                    DiamondCount = table.Column<int>(type: "integer", nullable: false),
                    Heroes = table.Column<List<int>>(type: "integer[]", nullable: false),
                    LevelData_LevelScore = table.Column<int>(type: "integer", nullable: false),
                    LevelData_Level = table.Column<int>(type: "integer", nullable: false),
                    LevelData_RewardStatus = table.Column<long>(type: "bigint", nullable: false),
                    DifficultyData_Levels = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "ARRAY[]::integer[]"),
                    DifficultyData_Stars = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "ARRAY[]::integer[]"),
                    TimeZoneOffset = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAssets", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserAttendances",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    RewardIndex = table.Column<int>(type: "integer", nullable: false),
                    CreateTime = table.Column<long>(type: "bigint", nullable: false),
                    TotalLoginDays = table.Column<int>(type: "integer", nullable: false),
                    LastLoginDate = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAttendances", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserBattlePassInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    PassId = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    ClaimStatus = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    SuperPassLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBattlePassInfos", x => new { x.PlayerId, x.PassId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserBeginnerTasks",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    StartTime = table.Column<long>(type: "bigint", nullable: false),
                    DayIndex = table.Column<int>(type: "integer", nullable: false),
                    FinishedCount = table.Column<int>(type: "integer", nullable: false),
                    Received = table.Column<bool>(type: "boolean", nullable: false),
                    TaskList = table.Column<List<BeginnerTaskData>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBeginnerTasks", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserCommodityBoughtRecords",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    CommodityId = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCommodityBoughtRecords", x => new { x.PlayerId, x.CommodityId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserCustomCardPools",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    HeroId = table.Column<int>(type: "integer", nullable: false),
                    CardList = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCustomCardPools", x => new { x.PlayerId, x.ShardId, x.HeroId });
                });

            migrationBuilder.CreateTable(
                name: "UserCustomData",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    IntData = table.Column<string>(type: "character varying(1023)", maxLength: 1023, nullable: false),
                    StrData = table.Column<string>(type: "character varying(2047)", maxLength: 2047, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCustomData", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserDailyStoreIndices",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyStoreIndices", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserDailyStoreItems",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    PriceType = table.Column<int>(type: "integer", nullable: false),
                    TimeStamp = table.Column<long>(type: "bigint", nullable: false),
                    Bought = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyStoreItems", x => new { x.PlayerId, x.ItemId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserDailyTasks",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    TaskRefreshTime = table.Column<long>(type: "bigint", nullable: false),
                    TaskProgress = table.Column<List<int>>(type: "integer[]", nullable: false),
                    DailyTaskRewardClaimStatus = table.Column<int>(type: "integer", nullable: false),
                    ActiveScoreRewardClaimStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyTasks", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserDailyTreasureBoxProgresses",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    RewardClaimStatus = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyTreasureBoxProgresses", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserDivisions",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    DivisionScore = table.Column<int>(type: "integer", nullable: false),
                    MaxDivisionScore = table.Column<int>(type: "integer", nullable: false),
                    LastSeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    RewardReceived = table.Column<bool>(type: "boolean", nullable: false),
                    LastDivisionScore = table.Column<int>(type: "integer", nullable: false),
                    LastDivisionRank = table.Column<int>(type: "integer", nullable: false),
                    LastWorldRank = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDivisions", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserEndlessRanks",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    SurvivorScore = table.Column<long>(type: "bigint", nullable: false),
                    SurvivorTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    TowerDefenceScore = table.Column<long>(type: "bigint", nullable: false),
                    TowerDefenceTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    TrueEndlessScore = table.Column<long>(type: "bigint", nullable: false),
                    TrueEndlessTimestamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEndlessRanks", x => new { x.PlayerId, x.ShardId, x.SeasonNumber });
                });

            migrationBuilder.CreateTable(
                name: "UserFixedLevelMapProgress",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    MapId = table.Column<int>(type: "integer", nullable: false),
                    StarCount = table.Column<int>(type: "integer", nullable: false),
                    FinishedTaskList = table.Column<List<int>>(type: "integer[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFixedLevelMapProgress", x => new { x.PlayerId, x.MapId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserFortuneBagInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    FortuneBags = table.Column<List<FortuneBagAcquireInfo>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    FortuneLevelRewardClaimStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFortuneBagInfos", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserGameInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    LastGameEndTime = table.Column<long>(type: "bigint", nullable: false),
                    DelayBoxSequence = table.Column<int>(type: "integer", nullable: false),
                    TicketBoxSequence = table.Column<int>(type: "integer", nullable: false),
                    DrawNewCardCountList = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "ARRAY[]::integer[]"),
                    DrawCardCountList = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "ARRAY[]::integer[]"),
                    GeneralCardCountList = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "ARRAY[]::integer[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGameInfos", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserGlobalInfos",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastLoginPlayerId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGlobalInfos", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserIapPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    IapItemId = table.Column<string>(type: "text", nullable: false),
                    WhenPurchased = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIapPurchases", x => new { x.Id, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserIdleRewardInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    StartTime = table.Column<long>(type: "bigint", nullable: false),
                    StolenRecords = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    IdleRewardId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdleRewardInfos", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Signature = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HideHistory = table.Column<bool>(type: "boolean", nullable: false),
                    AvatarFrameItemID = table.Column<int>(type: "integer", nullable: false),
                    NameCardItemID = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInfos", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserMallAdvertisements",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LastTime = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMallAdvertisements", x => new { x.PlayerId, x.Id, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserMonthPassInfos",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    PassAcquireTime = table.Column<long>(type: "bigint", nullable: false),
                    PassDayLength = table.Column<int>(type: "integer", nullable: false),
                    RewardClaimStatus = table.Column<int>(type: "integer", nullable: false),
                    LastRewardClaimDay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMonthPassInfos", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserRanks",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Division = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    HighestScore = table.Column<long>(type: "bigint", nullable: false),
                    Win = table.Column<bool>(type: "boolean", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRanks", x => new { x.PlayerId, x.ShardId, x.SeasonNumber });
                });

            migrationBuilder.CreateTable(
                name: "UserStarStoreStatus",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    StarRewardClaimStatus = table.Column<List<long>>(type: "bigint[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStarStoreStatus", x => new { x.PlayerId, x.ShardId });
                });

            migrationBuilder.CreateTable(
                name: "UserCards",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    CardLevel = table.Column<int>(type: "integer", nullable: false),
                    CardExp = table.Column<int>(type: "integer", nullable: false),
                    CardArenaDifficultyReached = table.Column<int>(type: "integer", nullable: false),
                    CardArenaLevelReached = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCards", x => new { x.PlayerId, x.CardId, x.ShardId });
                    table.ForeignKey(
                        name: "FK_UserCards_UserAssets_PlayerId_ShardId",
                        columns: x => new { x.PlayerId, x.ShardId },
                        principalTable: "UserAssets",
                        principalColumns: new[] { "PlayerId", "ShardId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserItems",
                columns: table => new
                {
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserItems", x => new { x.PlayerId, x.ItemId, x.ShardId });
                    table.ForeignKey(
                        name: "FK_UserItems_UserAssets_PlayerId_ShardId",
                        columns: x => new { x.PlayerId, x.ShardId },
                        principalTable: "UserAssets",
                        principalColumns: new[] { "PlayerId", "ShardId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTreasureBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    StarCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTreasureBoxes", x => new { x.Id, x.ShardId });
                    table.ForeignKey(
                        name: "FK_UserTreasureBoxes_UserAssets_PlayerId_ShardId",
                        columns: x => new { x.PlayerId, x.ShardId },
                        principalTable: "UserAssets",
                        principalColumns: new[] { "PlayerId", "ShardId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ShardId = table.Column<short>(type: "smallint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    GameTime = table.Column<float>(type: "real", nullable: false),
                    GameStartTime = table.Column<long>(type: "bigint", nullable: false),
                    MapType = table.Column<int>(type: "integer", nullable: false),
                    TypedMapId = table.Column<int>(type: "integer", nullable: false),
                    MapId = table.Column<int>(type: "integer", nullable: false),
                    Win = table.Column<bool>(type: "boolean", nullable: false),
                    KillCount = table.Column<int>(type: "integer", nullable: false),
                    MapUnlockRatio = table.Column<float>(type: "real", nullable: false),
                    Exp = table.Column<float>(type: "real", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    ResourceRecords = table.Column<List<HistoryResourceRecord>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    FightUnitInfos = table.Column<List<HistoryFightUnitInfo>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    DynamicInfo = table.Column<string>(type: "text", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHistories", x => new { x.Id, x.ShardId });
                    table.ForeignKey(
                        name: "FK_UserHistories_UserInfos_PlayerId_ShardId",
                        columns: x => new { x.PlayerId, x.ShardId },
                        principalTable: "UserInfos",
                        principalColumns: new[] { "PlayerId", "ShardId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaidOrders_PlayerId_ClaimStatus",
                table: "PaidOrders",
                columns: new[] { "PlayerId", "ClaimStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformNotifies_PlayerId_ClaimStatus_ShardId",
                table: "PlatformNotifies",
                columns: new[] { "PlayerId", "ClaimStatus", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_PlayerId_ShardId",
                table: "UserCards",
                columns: new[] { "PlayerId", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_SurvivorScore_SurvivorTimestamp",
                table: "UserEndlessRanks",
                columns: new[] { "SurvivorScore", "SurvivorTimestamp" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_TowerDefenceScore_TowerDefenceTimestamp",
                table: "UserEndlessRanks",
                columns: new[] { "TowerDefenceScore", "TowerDefenceTimestamp" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserEndlessRanks_TrueEndlessScore_TrueEndlessTimestamp",
                table: "UserEndlessRanks",
                columns: new[] { "TrueEndlessScore", "TrueEndlessTimestamp" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserHistories_PlayerId",
                table: "UserHistories",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserHistories_PlayerId_ShardId",
                table: "UserHistories",
                columns: new[] { "PlayerId", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserIapPurchases_PlayerId_ShardId",
                table: "UserIapPurchases",
                columns: new[] { "PlayerId", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserItems_PlayerId_ShardId",
                table: "UserItems",
                columns: new[] { "PlayerId", "ShardId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRanks_HighestScore_Timestamp",
                table: "UserRanks",
                columns: new[] { "HighestScore", "Timestamp" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_UserRanks_SeasonNumber_Division_GroupId",
                table: "UserRanks",
                columns: new[] { "SeasonNumber", "Division", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTreasureBoxes_PlayerId_ShardId",
                table: "UserTreasureBoxes",
                columns: new[] { "PlayerId", "ShardId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityCoopBossInfoSet");

            migrationBuilder.DropTable(
                name: "ActivityEndlessChallenges");

            migrationBuilder.DropTable(
                name: "ActivityLuckyStars");

            migrationBuilder.DropTable(
                name: "ActivityPiggyBanks");

            migrationBuilder.DropTable(
                name: "ActivityUnrivaledGods");

            migrationBuilder.DropTable(
                name: "PaidOrders");

            migrationBuilder.DropTable(
                name: "PlatformNotifies");

            migrationBuilder.DropTable(
                name: "PromotionStatus");

            migrationBuilder.DropTable(
                name: "RedisLuaScripts");

            migrationBuilder.DropTable(
                name: "SeasonRefreshedHistories");

            migrationBuilder.DropTable(
                name: "ServerDataset");

            migrationBuilder.DropTable(
                name: "SocInfos");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserAttendances");

            migrationBuilder.DropTable(
                name: "UserBattlePassInfos");

            migrationBuilder.DropTable(
                name: "UserBeginnerTasks");

            migrationBuilder.DropTable(
                name: "UserCards");

            migrationBuilder.DropTable(
                name: "UserCommodityBoughtRecords");

            migrationBuilder.DropTable(
                name: "UserCustomCardPools");

            migrationBuilder.DropTable(
                name: "UserCustomData");

            migrationBuilder.DropTable(
                name: "UserDailyStoreIndices");

            migrationBuilder.DropTable(
                name: "UserDailyStoreItems");

            migrationBuilder.DropTable(
                name: "UserDailyTasks");

            migrationBuilder.DropTable(
                name: "UserDailyTreasureBoxProgresses");

            migrationBuilder.DropTable(
                name: "UserDivisions");

            migrationBuilder.DropTable(
                name: "UserEndlessRanks");

            migrationBuilder.DropTable(
                name: "UserFixedLevelMapProgress");

            migrationBuilder.DropTable(
                name: "UserFortuneBagInfos");

            migrationBuilder.DropTable(
                name: "UserGameInfos");

            migrationBuilder.DropTable(
                name: "UserGlobalInfos");

            migrationBuilder.DropTable(
                name: "UserHistories");

            migrationBuilder.DropTable(
                name: "UserIapPurchases");

            migrationBuilder.DropTable(
                name: "UserIdleRewardInfos");

            migrationBuilder.DropTable(
                name: "UserItems");

            migrationBuilder.DropTable(
                name: "UserMallAdvertisements");

            migrationBuilder.DropTable(
                name: "UserMonthPassInfos");

            migrationBuilder.DropTable(
                name: "UserRanks");

            migrationBuilder.DropTable(
                name: "UserStarStoreStatus");

            migrationBuilder.DropTable(
                name: "UserTreasureBoxes");

            migrationBuilder.DropTable(
                name: "UserInfos");

            migrationBuilder.DropTable(
                name: "UserAssets");
        }
    }
}
