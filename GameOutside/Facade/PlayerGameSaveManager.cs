using System.Text.Json;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.v1.Management;
using GameOutside.Controllers;
using GameOutside.DBContext;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpLogging;

namespace GameOutside.Facade;

[HttpLogging(HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody)]
public class PlayerGameSaveManager(
    PlayerModule playerModule,
    BuildingGameDB dbCtx,
    ILogger<PlayerGameSaveManager> logger) : PlayerGameSaveManagement.PlayerGameSaveManagementBase
{
    private const string GameUnitIdPlayerDataPrefix = "PlayerData";
    public override async Task<FetchGameUnitsReply> FetchGameUnits(FetchGameUnitsRequest request, ServerCallContext context)
    {
        var exportedPlayerData = await ExportPlayerDataAsync(request.Pid);
        return new FetchGameUnitsReply
        {
            GameUnits =
            {
                ExportedPlayerDataToGameUnit(exportedPlayerData)
            }
        };
    }

    public override async Task<RawPayloadToGameUnitReply> RawPayloadToGameUnit(RawPayloadToGameUnitRequest request, ServerCallContext context)
    {
        if (request.Pid <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Pid 必须大于 0"));
        }

        if (string.IsNullOrWhiteSpace(request.RawPayload))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "RawPayload 不能为空"));
        }

        var playerData = JsonSerializer.Deserialize<ExportedPlayerData>(request.RawPayload);
        if (playerData == null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "RawPayload 反序列化失败或数据为空"));
        }

        return new RawPayloadToGameUnitReply { GameUnit = ExportedPlayerDataToGameUnit(playerData) };
    }

    public override async Task<FetchGameUnitsReply> UpsertGameUnits(UpsertGameUnitsRequest request, ServerCallContext context)
    {
        if (request.Pid <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Pid 必须大于 0"));
        }

        foreach (var (id, rawPayload) in request.GameUnits.ToDictionary(gu => gu.Id, gu => gu.RawPayload))
        {
            if (id.StartsWith(GameUnitIdPlayerDataPrefix))
            {
                var shardId = await playerModule.GetPlayerShardId(request.Pid);
                if (!shardId.HasValue)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"无法获取玩家 {request.Pid} 的分片"));
                }

                var exportedPlayerData = JsonSerializer.Deserialize<ExportedPlayerData>(rawPayload);
                if (exportedPlayerData == null)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "RawPayload 反序列化失败或数据为空"));
                }

                await ImportPlayerDataAsync(shardId.Value, request.Pid, exportedPlayerData);

                return new FetchGameUnitsReply
                {
                    GameUnits = {
                        ExportedPlayerDataToGameUnit(exportedPlayerData)
                    }
                };
            }
        }

        throw new RpcException(new Status(StatusCode.InvalidArgument, "没有找到可处理的 GameUnit Id"));
    }

    private async Task<ExportedPlayerData> ExportPlayerDataAsync(long playerId)
    {
        var shardId = await playerModule.GetPlayerShardId(playerId);
        if (!shardId.HasValue)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"无法获取玩家 {playerId} 的分片"));
        }

        var playerData = new ExportedPlayerData
        {
            PlatformNotifies = await dbCtx.PlatformNotifies
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            PaidOrderWithShards = await dbCtx.PaidOrderWithShards
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserRanks = await dbCtx.UserRanks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserEndlessRanks = await dbCtx.UserEndlessRanks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDivision = await dbCtx.UserDivisions
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserInfo = await dbCtx.UserInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserAssets = await dbCtx.UserAssets
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserItems = await dbCtx.UserItems
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserCards = await dbCtx.UserCards
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserTreasureBoxes = await dbCtx.UserTreasureBoxes
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserGameInfos = await dbCtx.UserGameInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserCustomData = await dbCtx.UserCustomData
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserIdleRewardInfos = await dbCtx.UserIdleRewardInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserHistories = await dbCtx.UserHistories
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDailyStoreItems = await dbCtx.UserDailyStoreItems
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDailyStoreIndex = await dbCtx.UserDailyStoreIndices
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserCommodityBoughtRecords = await dbCtx.UserCommodityBoughtRecords
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserAttendances = await dbCtx.UserAttendances
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserBattlePassInfos = await dbCtx.UserBattlePassInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserAchievements = await dbCtx.UserAchievements
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserBeginnerTask = await dbCtx.UserBeginnerTasks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserDailyTasks = await dbCtx.UserDailyTasks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserMallAdvertisements = await dbCtx.UserMallAdvertisements
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserCustomCardPools = await dbCtx.UserCustomCardPools
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserFixedLevelMapProgress = await dbCtx.UserFixedLevelMapProgress
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserIapPurchases = await dbCtx.UserIapPurchases
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityLuckyStar = await dbCtx.ActivityLuckyStars
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            PromotionStatus = await dbCtx.PromotionStatus
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserStarStoreStatus = await dbCtx.UserStarStoreStatus
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivityPiggyBanks = await dbCtx.ActivityPiggyBanks
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityUnrivaledGods = await dbCtx.ActivityUnrivaledGods
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserFortuneBagInfo = await dbCtx.UserFortuneBagInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivityTreasureMazeInfos = await dbCtx.ActivityTreasureMazeInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserMonthPassInfo = await dbCtx.UserMonthPassInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivityCoopBossInfoSet = await dbCtx.ActivityCoopBossInfoSet
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityEndlessChallenges = await dbCtx.ActivityEndlessChallenges
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            UserDailyTreasureBoxProgress = await dbCtx.UserDailyTreasureBoxProgresses
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserH5FriendActivityInfo = await dbCtx.UserH5FriendActivityInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            UserEncryptionInfo = await dbCtx.UserEncryptionInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            IosGameCenterRewardInfo = await dbCtx.IosGameCenterRewardInfos
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).FirstOrDefaultAsync(),
            ActivitySlotMachineInfos = await dbCtx.ActivitySlotMachines
                .IgnoreQueryFilters()
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityOneShotKillInfo = await dbCtx.ActivityOneShotKills
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId).ToListAsync(),
            ActivityTreasureHunts = await dbCtx.ActivityTreasureHunts.IgnoreQueryFilters()
                .Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
            ActivityRpgGames = await dbCtx.ActivityRpgGames.Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
            ActivityLoogGames = await dbCtx.ActivityLoogGames.Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
            ActivityCsgoStyleLotteries = await dbCtx.ActivityCsgoStyleLotteryInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId)
                .ToListAsync(),
        };

        // 清空关联数据，避免重复输出
        if (playerData.UserAssets != null)
        {
            playerData.UserAssets.UserCards = [];
            playerData.UserAssets.UserItems = [];
            playerData.UserAssets.UserTreasureBoxes = [];
        }
        if (playerData.UserInfo != null)
        {
            playerData.UserInfo.Histories = [];
        }

        return playerData;
    }

    private readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private IGameUnit ExportedPlayerDataToGameUnit(ExportedPlayerData playerData)
    {
        var playerId = playerData.UserInfo?.PlayerId;
        var shardId = playerData.UserInfo?.ShardId;
        if (!playerId.HasValue || !shardId.HasValue)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "PlayerData 中缺少 UserInfo，无法转换为 IGameUnit"));
        }

        var rawPayload = JsonSerializer.Serialize(playerData, _defaultJsonOptions);

        return new IGameUnit
        {
            Id = GameUnitIdPlayerDataPrefix + playerId.Value.ToString(),
            ShardId = shardId.Value,
            RawPayload = rawPayload,
            DisplayName = "玩家存档",
            States =
            {
                new StatePair {
                    Key = "属性",
                    Value = {
                        new IGameUnitValueType
                        {
                            Name = "ShardId",
                            DisplayName = "区域信息",
                            RawPayload = FormatRegionInfo(playerData.UserInfo?.ShardId),
                            DataType = "string"
                        },
                        new IGameUnitValueType
                        {
                            Name = "PlayerData",
                            DisplayName = "存档数据",
                            RawPayload = rawPayload,
                            DataType = "json"
                        }
                    }
                }
            }
        };
    }

    private static string FormatRegionInfo(short? shardId)
    {
        return shardId switch
        {
            1000 => "欧洲集群",
            2000 => "杭州集群",
            3000 => "新加坡集群",
            5000 => "北美集群",
            1051 => "深圳集群",
            _ => "本地服务器"
        };
    }

    private async Task ImportPlayerDataAsync(short shardId, long playerId, ExportedPlayerData exportedPlayerData)
    {
        // 清空关联数据，避免重复跟踪
        if (exportedPlayerData.UserAssets != null)
        {
            exportedPlayerData.UserAssets.UserCards = [];
            exportedPlayerData.UserAssets.UserItems = [];
            exportedPlayerData.UserAssets.UserTreasureBoxes = [];
        }
        if (exportedPlayerData.UserInfo != null)
        {
            exportedPlayerData.UserInfo.Histories = [];
        }

        // history 以 Id+ShardId 为主键，需要重新生成以避免冲突
        foreach (var history in exportedPlayerData.UserHistories)
        {
            history.Id = Guid.NewGuid();
        }

        dbCtx.Database.SetCommandTimeout(TimeSpan.FromMinutes(1));

        try
        {
            await dbCtx.WithRCUDefaultRetry(async _ =>
            {
                await using var transaction = await dbCtx.Database.BeginTransactionAsync();
                await DeletePlayerDataAsync(shardId, playerId);
                dbCtx.ChangeTracker.AcceptAllChanges();

                ReplaceWithNewShardIdPlayerId(exportedPlayerData, shardId, playerId);

                dbCtx.PlatformNotifies.AddRange(exportedPlayerData.PlatformNotifies);
                dbCtx.PaidOrderWithShards.AddRange(exportedPlayerData.PaidOrderWithShards);
                dbCtx.UserRanks.AddRange(exportedPlayerData.UserRanks);
                dbCtx.UserEndlessRanks.AddRange(exportedPlayerData.UserEndlessRanks);

                if (exportedPlayerData.UserDivision != null)
                    dbCtx.UserDivisions.Add(exportedPlayerData.UserDivision);
                if (exportedPlayerData.UserInfo != null)
                    dbCtx.UserInfos.Add(exportedPlayerData.UserInfo);
                if (exportedPlayerData.UserAssets != null)
                    dbCtx.UserAssets.Add(exportedPlayerData.UserAssets);

                dbCtx.UserItems.AddRange(exportedPlayerData.UserItems);
                dbCtx.UserCards.AddRange(exportedPlayerData.UserCards);
                dbCtx.UserTreasureBoxes.AddRange(exportedPlayerData.UserTreasureBoxes);
                dbCtx.UserGameInfos.AddRange(exportedPlayerData.UserGameInfos);

                if (exportedPlayerData.UserCustomData != null)
                    dbCtx.UserCustomData.Add(exportedPlayerData.UserCustomData);
                if (exportedPlayerData.UserIdleRewardInfos != null)
                    dbCtx.UserIdleRewardInfos.Add(exportedPlayerData.UserIdleRewardInfos);

                dbCtx.UserHistories.AddRange(exportedPlayerData.UserHistories);
                dbCtx.UserDailyStoreItems.AddRange(exportedPlayerData.UserDailyStoreItems);

                if (exportedPlayerData.UserDailyStoreIndex != null)
                    dbCtx.UserDailyStoreIndices.Add(exportedPlayerData.UserDailyStoreIndex);

                dbCtx.UserCommodityBoughtRecords.AddRange(exportedPlayerData.UserCommodityBoughtRecords);

                if (exportedPlayerData.UserAttendances != null)
                    dbCtx.UserAttendances.Add(exportedPlayerData.UserAttendances);

                dbCtx.UserBattlePassInfos.AddRange(exportedPlayerData.UserBattlePassInfos);
                dbCtx.UserAchievements.AddRange(exportedPlayerData.UserAchievements);

                if (exportedPlayerData.UserBeginnerTask != null)
                    dbCtx.UserBeginnerTasks.Add(exportedPlayerData.UserBeginnerTask);
                if (exportedPlayerData.UserDailyTasks != null)
                    dbCtx.UserDailyTasks.Add(exportedPlayerData.UserDailyTasks);

                dbCtx.UserMallAdvertisements.AddRange(exportedPlayerData.UserMallAdvertisements);
                dbCtx.UserCustomCardPools.AddRange(exportedPlayerData.UserCustomCardPools);
                dbCtx.UserFixedLevelMapProgress.AddRange(exportedPlayerData.UserFixedLevelMapProgress);
                dbCtx.UserIapPurchases.AddRange(exportedPlayerData.UserIapPurchases);

                if (exportedPlayerData.ActivityLuckyStar != null)
                    dbCtx.ActivityLuckyStars.Add(exportedPlayerData.ActivityLuckyStar);
                if (exportedPlayerData.PromotionStatus != null)
                    dbCtx.PromotionStatus.Add(exportedPlayerData.PromotionStatus);
                if (exportedPlayerData.UserStarStoreStatus != null)
                    dbCtx.UserStarStoreStatus.Add(exportedPlayerData.UserStarStoreStatus);

                dbCtx.ActivityPiggyBanks.AddRange(exportedPlayerData.ActivityPiggyBanks);
                dbCtx.ActivityUnrivaledGods.AddRange(exportedPlayerData.ActivityUnrivaledGods);

                if (exportedPlayerData.UserFortuneBagInfo != null)
                    dbCtx.UserFortuneBagInfos.Add(exportedPlayerData.UserFortuneBagInfo);

                dbCtx.ActivityTreasureMazeInfos.AddRange(exportedPlayerData.ActivityTreasureMazeInfos);

                if (exportedPlayerData.UserMonthPassInfo != null)
                    dbCtx.UserMonthPassInfos.Add(exportedPlayerData.UserMonthPassInfo);

                dbCtx.ActivityCoopBossInfoSet.AddRange(exportedPlayerData.ActivityCoopBossInfoSet);
                dbCtx.ActivityEndlessChallenges.AddRange(exportedPlayerData.ActivityEndlessChallenges);

                if (exportedPlayerData.UserDailyTreasureBoxProgress != null)
                    dbCtx.UserDailyTreasureBoxProgresses.Add(exportedPlayerData.UserDailyTreasureBoxProgress);
                if (exportedPlayerData.UserH5FriendActivityInfo != null)
                    dbCtx.UserH5FriendActivityInfos.Add(exportedPlayerData.UserH5FriendActivityInfo);
                if (exportedPlayerData.UserEncryptionInfo != null)
                    dbCtx.UserEncryptionInfos.Add(exportedPlayerData.UserEncryptionInfo);
                if (exportedPlayerData.IosGameCenterRewardInfo != null)
                    dbCtx.IosGameCenterRewardInfos.Add(exportedPlayerData.IosGameCenterRewardInfo);
                dbCtx.ActivitySlotMachines.AddRange(exportedPlayerData.ActivitySlotMachineInfos);
                dbCtx.ActivityOneShotKills.AddRange(exportedPlayerData.ActivityOneShotKillInfo);
                dbCtx.ActivityTreasureHunts.AddRange(exportedPlayerData.ActivityTreasureHunts);
                dbCtx.ActivityRpgGames.AddRange(exportedPlayerData.ActivityRpgGames);
                dbCtx.ActivityLoogGames.AddRange(exportedPlayerData.ActivityLoogGames);
                dbCtx.ActivityCsgoStyleLotteryInfos.AddRange(exportedPlayerData.ActivityCsgoStyleLotteries);

                await dbCtx.SaveChangesWithDefaultRetryAsync(false);
                await transaction.CommitAsync();
                dbCtx.ChangeTracker.AcceptAllChanges();

                return true;
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "导入玩家数据失败，ShardId: {ShardId}, PlayerId: {PlayerId}", shardId, playerId);
            throw new RpcException(new Status(StatusCode.Internal, $"导入 {shardId}-{playerId} 玩家数据失败"));
        }
    }

    private static void ReplaceWithNewShardIdPlayerId(ExportedPlayerData data, short newShardId, long newPlayerId)
    {
        foreach (var prop in data.GetType().GetProperties())
        {
            var value = prop.GetValue(data);
            if (value is null)
                continue;

            if (value is IEnumerable<object> list)
            {
                foreach (var item in list)
                {
                    var shardIdProp = item.GetType().GetProperty("ShardId");
                    if (shardIdProp != null)
                    {
                        shardIdProp.SetValue(item, newShardId);
                    }
                    var playerIdProp = item.GetType().GetProperty("PlayerId");
                    if (playerIdProp != null)
                    {
                        playerIdProp.SetValue(item, newPlayerId);
                    }
                }
            }
            else
            {
                var shardIdProp = value.GetType().GetProperty("ShardId");
                if (shardIdProp != null)
                {
                    shardIdProp.SetValue(value, newShardId);
                }
                var playerIdProp = value.GetType().GetProperty("PlayerId");
                if (playerIdProp != null)
                {
                    playerIdProp.SetValue(value, newPlayerId);
                }
            }
        }
    }

    private async Task DeletePlayerDataAsync(short shardId, long playerId)
    {
        await dbCtx.PlatformNotifies.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.PaidOrderWithShards.Where(p => p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserRanks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserEndlessRanks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserDivisions.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserAssets.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserItems.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserCards.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserTreasureBoxes.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserGameInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserCustomData.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserIdleRewardInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserHistories.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserDailyStoreItems.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserDailyStoreIndices.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserCommodityBoughtRecords.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserAttendances.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserBattlePassInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserAchievements.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserBeginnerTasks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserDailyTasks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserMallAdvertisements.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserCustomCardPools.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserFixedLevelMapProgress.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserIapPurchases.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityLuckyStars.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.PromotionStatus.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserStarStoreStatus.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityPiggyBanks.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityUnrivaledGods.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserFortuneBagInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityTreasureMazeInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserMonthPassInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityCoopBossInfoSet.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityEndlessChallenges.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserDailyTreasureBoxProgresses.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserH5FriendActivityInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.UserEncryptionInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.IosGameCenterRewardInfos.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivitySlotMachines.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityOneShotKills.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityTreasureHunts.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityRpgGames.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
        await dbCtx.ActivityLoogGames.Where(p => p.ShardId == shardId && p.PlayerId == playerId).ExecuteDeleteAsync();
    }
}
