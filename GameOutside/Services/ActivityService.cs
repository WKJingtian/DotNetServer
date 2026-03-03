using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Services;
using GameOutside.Util;
using StackExchange.Redis;

namespace GameOutside;

public class ActivityService(
    ServerConfigService serverConfigService,
    UserAssetService userAssetService,
    ServerDataService serverDataService,
    IActivityRepository activityRepository,
    [FromKeyedServices("GlobalCache")] IConnectionMultiplexer globalCache,
    IConnectionMultiplexer distributedRedis,
    ILogger<ActivityService> logger,
    BuildingGameDB dbCtx)
{
    private const string _activityOneShotKillProgressKeyPrefix = "activity_one_shot_kill_progress_";
    private const string _activityOneShotKillLocalProgressAddKeyPrefix = "activity_one_shot_kill_local_progress_add_";
    private const string _activityOneShotKillLocalProgressMinusKeyPrefix = "activity_one_shot_kill_local_progress_minus_";
    private const string _activityOneShotKillTaskProgressKeyPrefix = "activity_one_shot_kill_task_";
    private const string _activityOneShotKillEventListKeyPrefix = "activity_one_shot_kill_event_list_";
    private const string _activityOneShotKillVictoryCountKeyPrefix = "activity_one_shot_kill_victory_count_";

    public async Task OneShotKillDataInit(ActivityTimeConfig activityConfig)
    {
        // 加载配置
        int activityId = activityConfig.id;
        var mapConfigList = serverConfigService.GetOneShotKillMapConfigListByActivityId(activityId);
        var leaderConfigList = serverConfigService.GetOneShotKillLeaderConfigListByActivityId(activityId);
        if (mapConfigList.Count == 0 ||
            leaderConfigList.Count == 0)
            return;

        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDefaultProgressDecay,
                out var defaultProgressDecay) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillPhase1EndAfter,
                out var phase1EndAfter) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillPhase2EndAfter,
                out var phase2EndAfter) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillFirstDayGrowthDefault,
                out var firstDayGrowthDefault) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillDataRefreshInterval,
                out var dataRefreshInterval))
            return;

        var currentTime = TimeUtils.GetCurrentTime();
        long activityBeginTime = TimeUtils.ParseDateTimeStrToUnixSecond(activityConfig.start_time);
        var secondSinceStart = Math.Max(1, currentTime - activityBeginTime);
        int intervalCountSinceStart = (int)((secondSinceStart) / dataRefreshInterval);

        var redisDb = distributedRedis.GetDatabase();
        var oneShotKillProgressList = await redisDb.HashGetAllAsync(_activityOneShotKillProgressKeyPrefix + activityId);
        var oneShotKillProgressAddHistoryList = await redisDb.HashGetAllAsync(_activityOneShotKillLocalProgressAddKeyPrefix + activityId);
        var oneShotKillProgressMinusHistoryList = await redisDb.HashGetAllAsync(_activityOneShotKillLocalProgressMinusKeyPrefix + activityId);

        Dictionary<int, int> oneShotKillProgressDict = oneShotKillProgressList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
        Dictionary<int, int> oneShotKillProgressAddDict = oneShotKillProgressAddHistoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
        Dictionary<int, int> oneShotKillProgressMinusDict = oneShotKillProgressMinusHistoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);

        Dictionary<int, OneShotKillRegionState> regionFinalStateDict = new Dictionary<int, OneShotKillRegionState>();
        Dictionary<int, int> regionFinalProgressDict = new Dictionary<int, int>();

        // 如果本次活动尚未被初始化
        if (oneShotKillProgressDict.Count == 0)
        {
            CalculateOneShotKillInitState(mapConfigList,
                oneShotKillProgressAddDict, oneShotKillProgressMinusDict,
                regionFinalProgressDict, regionFinalStateDict);

            currentTime = activityBeginTime + 1; // 不要让事件发生早于活动开始
        }
        else return; // 如果已经初始化过，后面的逻辑也不用跑了

        bool IfRegionAtWar(int level)
        {
            OneShotKillRegionState state;
            if (regionFinalStateDict.TryGetValue(level, out state))
                return state == OneShotKillRegionState.WarZone;
            return false;
        }

        // 写入数据
        foreach (var pair in regionFinalProgressDict)
            await redisDb.HashSetAsync(_activityOneShotKillProgressKeyPrefix + activityId, pair.Key, pair.Value);
        foreach (var pair in oneShotKillProgressAddDict)
            if (IfRegionAtWar(pair.Key))
                await redisDb.HashSetAsync(_activityOneShotKillLocalProgressAddKeyPrefix + activityId, pair.Key, pair.Value);
        foreach (var pair in oneShotKillProgressMinusDict)
            if (IfRegionAtWar(pair.Key))
                await redisDb.HashSetAsync(_activityOneShotKillLocalProgressMinusKeyPrefix + activityId, pair.Key, pair.Value);
        foreach (var item in regionFinalStateDict)
            if (item.Value != OneShotKillRegionState.Freed)
            {
                OneShotKillEventType typ;
                switch (item.Value)
                {
                    case OneShotKillRegionState.Fallen: typ = OneShotKillEventType.RegionFell; break;
                    case OneShotKillRegionState.Freed: typ = OneShotKillEventType.RegionFreed; break;
                    default: typ = OneShotKillEventType.RegionWarBegin; break;
                }
                await redisDb.HashSetAsync(_activityOneShotKillEventListKeyPrefix + activityId,
                    GetOneShotKillEventHashKey(typ, item.Key, intervalCountSinceStart),
                    currentTime + (typ == OneShotKillEventType.RegionWarBegin ? 1 : 0)); // +1方便排序
            }
        redisDb.KeyExpireAsync(_activityOneShotKillProgressKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillLocalProgressAddKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillLocalProgressMinusKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillEventListKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
    }

    public async Task OneShotKillTenMinuteUpdateAsync(ActivityTimeConfig activityConfig)
    {
        int activityId = activityConfig.id;
        var mapConfigList = serverConfigService.GetOneShotKillMapConfigListByActivityId(activityId);
        var leaderConfigList = serverConfigService.GetOneShotKillLeaderConfigListByActivityId(activityId);
        if (mapConfigList.Count == 0 ||
            leaderConfigList.Count == 0)
            return;

        // 加载配置
        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDefaultProgressDecay,
                out var defaultProgressDecay) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillPhase1EndAfter,
                out var phase1EndAfter) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillPhase2EndAfter,
                out var phase2EndAfter) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillFirstDayGrowthDefault,
                out var firstDayGrowthDefault) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillDataRefreshInterval,
                out var dataRefreshInterval))
            return;

        var currentTime = TimeUtils.GetCurrentTime();
        long activityBeginTime = TimeUtils.ParseDateTimeStrToUnixSecond(activityConfig.start_time);
        var secondSinceStart = Math.Max(1, currentTime - activityBeginTime);
        bool isFirstDay = secondSinceStart <= Consts.DaySeconds;
        int activityPhase =
            (secondSinceStart <= phase1EndAfter) ? 0 :
            (secondSinceStart <= phase2EndAfter) ? 1 :
            2;
        int intervalCountSinceStart = (int)((secondSinceStart) / dataRefreshInterval);

        var redisDb = distributedRedis.GetDatabase();
        var oneShotKillProgressList = await redisDb.HashGetAllAsync(_activityOneShotKillProgressKeyPrefix + activityId);
        var oneShotKillProgressAddHistoryList = await redisDb.HashGetAllAsync(_activityOneShotKillLocalProgressAddKeyPrefix + activityId);
        var oneShotKillProgressMinusHistoryList = await redisDb.HashGetAllAsync(_activityOneShotKillLocalProgressMinusKeyPrefix + activityId);
        var oneShotKillLastHourVictoryList = await redisDb.HashGetAllAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId);

        Dictionary<int, int> oneShotKillProgressDict = oneShotKillProgressList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
        Dictionary<int, int> oneShotKillProgressAddDict = oneShotKillProgressAddHistoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
        Dictionary<int, int> oneShotKillProgressMinusDict = oneShotKillProgressMinusHistoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
        Dictionary<int, int> oneShotKillLastHourVictoryDict = oneShotKillLastHourVictoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);

        if (!oneShotKillLastHourVictoryDict.TryGetValue(
                GetOneShotKillVictoryCountHashKey(0, 0),
                out var firstDayProgressPerRegion))
            firstDayProgressPerRegion = 1; // 会被除，不能是0
        int intervalCountPerDay = Consts.DaySeconds / dataRefreshInterval;

        Dictionary<int, OneShotKillRegionState> regionOriginalStateDict = new Dictionary<int, OneShotKillRegionState>();
        Dictionary<int, int> regionNewProgressDict = new Dictionary<int, int>();

        Dictionary<int, OneShotKillRegionState> regionFinalStateDict = new Dictionary<int, OneShotKillRegionState>();
        Dictionary<int, int> regionFinalProgressDict = new Dictionary<int, int>();

        // 计算当前状态
        foreach (var mapConfig in mapConfigList)
        {
            var level = mapConfig.level;
            if (!oneShotKillProgressDict.TryGetValue(level, out var progress))
                progress = mapConfig.victory_count_to_conquer; // 默认状态是解放

            OneShotKillRegionState state;
            if (progress <= 0) state = OneShotKillRegionState.Fallen;
            else if (progress >= mapConfig.victory_count_to_conquer) state = OneShotKillRegionState.Freed;
            else state = OneShotKillRegionState.WarZone;
            regionOriginalStateDict[level] = state;
        }

        // 本此活动的首次结算
        if (oneShotKillProgressDict.Count == 0)
        {
            CalculateOneShotKillInitState(mapConfigList,
                oneShotKillProgressAddDict, oneShotKillProgressMinusDict,
                regionFinalProgressDict, regionFinalStateDict);

            currentTime = activityBeginTime + 1; // 不要让事件发生早于活动开始
        }
        // 正常结算流程
        else
        {
            // 计算全局进度变化
            var taskProgressList = await GetOneShotKillTaskProgressAsync(activityId);
            int globalProgressAdd = GetOneShotKillGlobalProgressAdd(activityId, taskProgressList);

            int loopCount = 0; // 最多循环三次
            while (loopCount < 3 && CalculateOneShotKillRegionState(
                       mapConfigList, globalProgressAdd, activityPhase, isFirstDay,
                       intervalCountSinceStart, intervalCountPerDay, firstDayProgressPerRegion,
                       oneShotKillLastHourVictoryDict, oneShotKillProgressDict,
                       oneShotKillProgressAddDict, oneShotKillProgressMinusDict,
                       regionNewProgressDict, regionFinalStateDict
                       ))
            {
                loopCount++;
                foreach (var item in regionNewProgressDict)
                    regionFinalProgressDict[item.Key] = item.Value;
            }
            foreach (var item in regionNewProgressDict)
                regionFinalProgressDict[item.Key] = item.Value;
        }

        bool IfRegionAtWar(int level)
        {
            OneShotKillRegionState state;
            if (regionFinalStateDict.TryGetValue(level, out state))
                return state == OneShotKillRegionState.WarZone;
            if (regionOriginalStateDict.TryGetValue(level, out state))
                return state == OneShotKillRegionState.WarZone;
            return false;
        }

        // 写入数据
        foreach (var pair in regionFinalProgressDict)
            await redisDb.HashSetAsync(_activityOneShotKillProgressKeyPrefix + activityId, pair.Key, pair.Value);
        foreach (var pair in oneShotKillProgressAddDict)
            if (IfRegionAtWar(pair.Key))
                await redisDb.HashSetAsync(_activityOneShotKillLocalProgressAddKeyPrefix + activityId, pair.Key, pair.Value);
        foreach (var pair in oneShotKillProgressMinusDict)
            if (IfRegionAtWar(pair.Key))
                await redisDb.HashSetAsync(_activityOneShotKillLocalProgressMinusKeyPrefix + activityId, pair.Key, pair.Value);
        foreach (var item in regionFinalStateDict)
            if (!regionOriginalStateDict.TryGetValue(item.Key, out var oldState) || oldState != item.Value)
            {
                OneShotKillEventType typ;
                switch (item.Value)
                {
                    case OneShotKillRegionState.Fallen: typ = OneShotKillEventType.RegionFell; break;
                    case OneShotKillRegionState.Freed: typ = OneShotKillEventType.RegionFreed; break;
                    default: typ = OneShotKillEventType.RegionWarBegin; break;
                }
                await redisDb.HashSetAsync(_activityOneShotKillEventListKeyPrefix + activityId,
                    GetOneShotKillEventHashKey(typ, item.Key, intervalCountSinceStart),
                    currentTime + (typ == OneShotKillEventType.RegionWarBegin ? 1 : 0)); // +1方便排序
            }
        redisDb.KeyExpireAsync(_activityOneShotKillProgressKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillLocalProgressAddKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillLocalProgressMinusKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillEventListKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
    }

    // 返回true则意味着需要再次执行
    private bool CalculateOneShotKillRegionState(
        List<ActivityOneShotKillMapConfig> mapConfigList,
        int globalProgressAdd,
        int activityPhase,
        bool isFirstDay,
        int intervalCountSinceStart,
        int intervalCountPerDay,
        int firstDayVictoryCountPerRegion,
        Dictionary<int, int> lastHourVictoryDict,
        Dictionary<int, int> originalProgressDict,
        Dictionary<int, int> progressAddDict,
        Dictionary<int, int> progressLoseDict,
        Dictionary<int, int> newProgressDict,
        Dictionary<int, OneShotKillRegionState> newStateDict)
    {
        // 清空结果容器
        newProgressDict.Clear();

        // 加载配置
        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDefaultProgressDecay,
                out var defaultProgressDecay) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillDeltaDecayPhase1,
                out var deltaDecayPhase1) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillMaxDecayPhase1,
                out var maxDecayPhase1) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillProgressMarginPhase1,
                out var progressMarginPhase1) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillExtraDecayPhase1,
                out var extraDecayPhase1) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillDeltaDecayPhase2,
                out var deltaDecayPhase2) ||
            !serverConfigService.TryGetParameterFloat(Params.OneShotKillMarginDecayRatePhase2,
                out var marginDecayRatePhase2) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillTargetDecayPhase3,
                out var targetDecayPhase3) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillDeltaDecayPhase3,
                out var deltaDecayPhase3) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillFirstDayGrowthDefault,
                out var firstDayGrowthDefault) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillFirstDayGrowthMax,
                out var firstDayGrowthMax) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillFirstDayGrowthChangeRate,
                out var firstDayGrowthChangeRate) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillGrowthDefault,
                out var growthDefault) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillGrowthChangeRateBase,
                out var growthChangeRateBase) ||
            !serverConfigService.TryGetParameterFloat(Params.OneShotKillInitProgressFromFallenRegion,
                out var initProgressFromFallenRegion) ||
            !serverConfigService.TryGetParameterFloat(Params.OneShotKillInitProgressFromFreedRegion,
                out var initProgressFromFreedRegion))
            return false;

        int GetOldRegionProgress(ActivityOneShotKillMapConfig map)
        {
            return originalProgressDict.TryGetValue(map.level, out var p) ? p : map.victory_count_to_conquer;
        }
        int GetRegionProgress(ActivityOneShotKillMapConfig map)
        {
            if (newProgressDict.TryGetValue(map.level, out var newP))
                return newP;
            return GetOldRegionProgress(map);
        }
        OneShotKillRegionState GetOriginalRegionState(ActivityOneShotKillMapConfig map)
        {
            var p = GetOldRegionProgress(map);
            if (p <= 0) return OneShotKillRegionState.Fallen;
            if (p >= map.victory_count_to_conquer) return OneShotKillRegionState.Freed;
            return OneShotKillRegionState.WarZone;
        }
        OneShotKillRegionState GetRegionState(ActivityOneShotKillMapConfig map)
        {
            var p = GetRegionProgress(map);
            if (p <= 0) return OneShotKillRegionState.Fallen;
            if (p >= map.victory_count_to_conquer) return OneShotKillRegionState.Freed;
            return OneShotKillRegionState.WarZone;
        }

        // 1，根据全局进度增粘，本地进度衰减，本地进度增长计算所有原本进度在1到100之间的区域的进度变化。
        foreach (var mapConfig in mapConfigList)
        {
            if (GetOriginalRegionState(mapConfig) != OneShotKillRegionState.WarZone)
                continue;

            var level = mapConfig.level;
            int progress = GetOldRegionProgress(mapConfig);

            // 一堆数值加减
            int lastHourAdd = progressAddDict.ContainsKey(level)
                ? progressAddDict[level]
                : 0;
            int lastHourMinus = progressLoseDict.ContainsKey(level)
                ? progressLoseDict[level]
                : 0;
            int valDeduct = 0;
            int valAdd = 0;

            // 计算进度衰减
            switch (activityPhase)
            {
                case 0:
                    valDeduct = Math.Min(maxDecayPhase1, lastHourMinus + deltaDecayPhase1);
                    if (valDeduct >= maxDecayPhase1)
                    {
                        if (lastHourMinus < lastHourAdd + progressMarginPhase1)
                            valDeduct += extraDecayPhase1;
                    }
                    break;
                case 1:
                    valDeduct = lastHourMinus;
                    if (lastHourMinus < lastHourAdd)
                        valDeduct += Math.Max((int)((lastHourAdd - lastHourMinus) * marginDecayRatePhase2), deltaDecayPhase2);
                    break;
                case 2:
                    valDeduct = lastHourMinus;
                    if (valDeduct > targetDecayPhase3)
                        valDeduct -= deltaDecayPhase3;
                    break;
            }


            // 计算进度增加
            if (isFirstDay)
            {
                valAdd = lastHourAdd;
                if (lastHourAdd < lastHourMinus)
                    valAdd += firstDayGrowthChangeRate;
                valAdd = Math.Clamp(valAdd, 0, firstDayGrowthMax);
            }
            else if (lastHourVictoryDict.TryGetValue(
                         GetOneShotKillVictoryCountHashKey(intervalCountSinceStart - 1, mapConfig.level),
                         out var lastHourProgress))
            {
                valAdd = (int)(Math.Log((float)lastHourProgress / firstDayVictoryCountPerRegion * intervalCountPerDay + (Math.E - 1)) * growthChangeRateBase);
            }
            else
            {
                valAdd = (int)(Math.Log(Math.E - 1) * growthChangeRateBase);
            }

            valDeduct = Math.Max(valDeduct, 0);
            progress = progress - valDeduct + globalProgressAdd + valAdd;

            if (progress <= 0)
                newStateDict[level] = OneShotKillRegionState.Fallen;
            else if (progress >= mapConfig.victory_count_to_conquer)
                newStateDict[level] = OneShotKillRegionState.Freed;
            newProgressDict[level] = progress;
            progressAddDict[level] = valAdd;
            progressLoseDict[level] = valDeduct;
        }

        // 2，遍历所有原本进度为0或100的区域，如果该区域和临近的区域里存在进度为0的区域和进度为100的区域，则标记为新交战区。
        foreach (var mapConfig in mapConfigList)
        {
            var selfState = GetOriginalRegionState(mapConfig);
            if (selfState == OneShotKillRegionState.WarZone)
                continue;

            var level = mapConfig.level;
            int nearFallenRegionCount = 0, nearFreeRegionCount = 0;
            if (selfState == OneShotKillRegionState.Fallen) nearFallenRegionCount++;
            else if (selfState == OneShotKillRegionState.Freed) nearFreeRegionCount++;
            foreach (var nearRegionLevel in mapConfig.adjacent_region)
            {
                var nearRegionConfig = mapConfigList[nearRegionLevel];
                var nearRegionState = GetRegionState(nearRegionConfig);
                if (nearRegionState == OneShotKillRegionState.Fallen) nearFallenRegionCount++;
                else if (nearRegionState == OneShotKillRegionState.Freed) nearFreeRegionCount++;
            }

            if (nearFallenRegionCount > 0 && nearFreeRegionCount > 0)
                newStateDict[level] = OneShotKillRegionState.WarZone;
        }

        // 3，如果所有区域进度都是0，把id最大的区域标记为新交战区。
        int fallenRegionCount = 0;
        foreach (var mapConfig in mapConfigList)
            if (GetRegionState(mapConfig) == OneShotKillRegionState.Fallen) fallenRegionCount++;
        if (fallenRegionCount == mapConfigList.Count)
            newStateDict[mapConfigList.Count - 1] = OneShotKillRegionState.WarZone;

        // 4，所有被标记为新交战区的区域根据原本进度设置新的进度。
        foreach (var item in newStateDict)
        {
            if (item.Value != OneShotKillRegionState.WarZone)
                continue;
            int level = item.Key;
            var warRegionConfig = mapConfigList[level];
            newProgressDict[level] = (int)(warRegionConfig.victory_count_to_conquer *
                (GetOriginalRegionState(warRegionConfig) == OneShotKillRegionState.Freed ? initProgressFromFreedRegion : initProgressFromFallenRegion));
            progressAddDict[level] = isFirstDay ? firstDayGrowthDefault : growthDefault;
            progressLoseDict[level] = defaultProgressDecay; // 默认的进度减少速度
        }

        // 5，如果此时存在进度为0的区域且不存在进度在0到100之间的区域，重复这个流程。
        fallenRegionCount = 0;
        int warRegionCount = 0;
        foreach (var mapConfig in mapConfigList)
        {
            var state = GetRegionState(mapConfig);
            if (state == OneShotKillRegionState.Fallen) fallenRegionCount++;
            else if (state == OneShotKillRegionState.WarZone) warRegionCount++;
        }

        return fallenRegionCount > 0 && warRegionCount == 0;
    }

    private void CalculateOneShotKillInitState(
        List<ActivityOneShotKillMapConfig> mapConfigList,
        Dictionary<int, int> progressAddDict,
        Dictionary<int, int> progressLoseDict,
        Dictionary<int, int> newProgressDict,
        Dictionary<int, OneShotKillRegionState> stateDict)
    {
        // 加载配置
        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDefaultProgressDecay,
                out var defaultProgressDecay) ||
            !serverConfigService.TryGetParameterInt(Params.OneShotKillFirstDayGrowthDefault,
                out var firstDayGrowthDefault))
            return;
        foreach (var mapConfig in mapConfigList)
        {
            var level = mapConfig.level;
            OneShotKillRegionState state;
            if (mapConfig.default_progress >= mapConfig.victory_count_to_conquer)
                state = OneShotKillRegionState.Freed;
            else if (mapConfig.default_progress <= 0)
                state = OneShotKillRegionState.Fallen;
            else
                state = OneShotKillRegionState.WarZone;
            newProgressDict[level] = mapConfig.default_progress;
            if (state == OneShotKillRegionState.WarZone)
            {
                stateDict[level] = state;
                progressAddDict[level] = firstDayGrowthDefault;
                progressLoseDict[level] = defaultProgressDecay;
            }
            else if (state == OneShotKillRegionState.Fallen)
                stateDict[level] = state;
        }
    }

    public async Task AddOneShotKillVictoryAsync(int activityId, int level, List<int> taskProgressAdded, bool isWin, bool isChallengeMode)
    {
        var mapConfigList = serverConfigService.GetOneShotKillMapConfigListByActivityId(activityId);
        var taskConfigList = serverConfigService.GetOneShotKillTaskConfigListByActivityId(activityId);
        if (mapConfigList.Count == 0 ||
            taskConfigList.Count == 0)
            return;
        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDataRefreshInterval, out var dataRefreshInterval))
            return;

        var currentTime = TimeUtils.GetCurrentTime();
        var redisDb = distributedRedis.GetDatabase();
        var oneShotKillStateHistoryList = await redisDb.HashGetAllAsync(_activityOneShotKillTaskProgressKeyPrefix + activityId);
        Dictionary<int, int> taskProgressOld = oneShotKillStateHistoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);

        foreach (var taskConfig in taskConfigList)
        {
            if (taskProgressAdded.Count <= taskConfig.task_id) continue;
            var beginTime = TimeUtils.ParseDateTimeStrToUnixSecond(taskConfig.begin_time);
            var endTime = TimeUtils.ParseDateTimeStrToUnixSecond(taskConfig.end_time);
            if (currentTime < beginTime || currentTime > endTime) continue;
            if (taskProgressOld.TryGetValue(taskConfig.task_id, out var oldTaskProgress) &&
                oldTaskProgress >= taskConfig.count_to_complete)
                continue;
            var progressToAdd = Math.Clamp(taskProgressAdded[taskConfig.task_id], 0, taskConfig.count_to_complete / 10000);
            await redisDb.HashIncrementAsync(_activityOneShotKillTaskProgressKeyPrefix + activityId,
                taskConfig.task_id,
                progressToAdd);
            if (progressToAdd + oldTaskProgress >= taskConfig.count_to_complete)
                await redisDb.HashSetAsync(_activityOneShotKillEventListKeyPrefix + activityId,
                    GetOneShotKillEventHashKey(OneShotKillEventType.TaskCompleted, taskConfig.task_id, 0),
                    currentTime);
        }
        redisDb.KeyExpireAsync(_activityOneShotKillEventListKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillTaskProgressKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);

        if (!isWin)
            return;

        var activityConfig = serverConfigService.GetActivityConfigById(activityId);
        if (activityConfig == null)
            return;
        long activityBeginTime = TimeUtils.ParseDateTimeStrToUnixSecond(activityConfig.start_time);
        var secondSinceStart = Math.Max(1, currentTime - activityBeginTime);
        bool isFirstDay = secondSinceStart <= Consts.DaySeconds;
        int intervalCountSinceStart = (int)((secondSinceStart) / dataRefreshInterval);
        var mapConfig = mapConfigList[level];
        var advanceAmount = isChallengeMode
            ? mapConfig.progress_per_challenge_mode_victory
            : mapConfig.progress_per_victory;
        if (isFirstDay) // 除初始交战区数量是为了之后方便计算
            await redisDb.HashIncrementAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId,
                GetOneShotKillVictoryCountHashKey(0, 0),
                Math.Ceiling((float)advanceAmount / serverConfigService.GetOneShotKillInitialWarZoneCount(activityId)));
        else
            await redisDb.HashIncrementAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId,
                GetOneShotKillVictoryCountHashKey(intervalCountSinceStart, mapConfig.level),
                advanceAmount);
        redisDb.KeyExpireAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);

        // 不再在每局结束后立刻改变解放进度了
        /*
        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDefaultProgressDecay,
                out var defaultProgressDecay))
            return;
            
        var oneShotKillProgressList = await redisDb.HashGetAllAsync(_activityOneShotKillProgressKeyPrefix + activityId);
        Dictionary<int, int> oneShotKillProgressDict = new Dictionary<int, int>();
        foreach (var progressData in oneShotKillProgressList)
            oneShotKillProgressDict[(int)progressData.Name] = (int)progressData.Value;

        if (!oneShotKillProgressDict.TryGetValue(level, out var progress) ||
            progress <= 0 || progress >= mapConfig.victory_count_to_conquer)
            return;

        var progressToAdd = mapConfig.victory_count_to_conquer;
        var newProgress = progress + progressToAdd;
        await redisDb.HashIncrementAsync(_activityOneShotKillProgressKeyPrefix + activityId, level, progressToAdd);
        await redisDb.HashIncrementAsync(_activityOneShotKillProgressAddKeyPrefix + activityId, level, progressToAdd);
        if (newProgress >= mapConfig.victory_count_to_conquer)
        {
            foreach (var adjacentLevel in mapConfig.adjacent_region)
            {
                var adjacentMapConfig = mapConfigList[adjacentLevel];
                var adjacentProgress = oneShotKillProgressDict.ContainsKey(adjacentLevel)
                    ? oneShotKillProgressDict[adjacentLevel]
                    : adjacentMapConfig.victory_count_to_conquer;
                if (adjacentProgress <= 0) // 临近的沦陷区
                {
                    var initProgress = (int)(adjacentMapConfig.victory_count_to_conquer * 0.5f);
                    await redisDb.HashSetAsync(_activityOneShotKillProgressKeyPrefix + activityId, adjacentLevel, initProgress);
                    await redisDb.HashSetAsync(_activityOneShotKillProgressAddKeyPrefix + activityId, adjacentLevel, 0);
                    await redisDb.HashSetAsync(_activityOneShotKillProgressMinusKeyPrefix + activityId, adjacentLevel, defaultProgressDecay);
                }
            }
        }
        redisDb.KeyExpireAsync(_activityOneShotKillProgressKeyPrefix + activityId, TimeSpan.FromDays(180), flags:CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillProgressAddKeyPrefix + activityId, TimeSpan.FromDays(180), flags:CommandFlags.FireAndForget);
        redisDb.KeyExpireAsync(_activityOneShotKillProgressMinusKeyPrefix + activityId, TimeSpan.FromDays(180), flags:CommandFlags.FireAndForget);
        */
    }

    public async Task ClearOneShotKillServerData(int activityId)
    {
        var redisDb = distributedRedis.GetDatabase();
        RedisKey[] keyToDelete =
        {
            _activityOneShotKillProgressKeyPrefix + activityId,
            _activityOneShotKillLocalProgressAddKeyPrefix + activityId,
            _activityOneShotKillLocalProgressMinusKeyPrefix + activityId,
            _activityOneShotKillTaskProgressKeyPrefix + activityId,
            _activityOneShotKillEventListKeyPrefix + activityId,
            _activityOneShotKillVictoryCountKeyPrefix + activityId,
        };
        await redisDb.KeyDeleteAsync(keyToDelete);
    }

    public async Task<HashEntry[]> GetOneShotKillEventListAsync(int activityId)
    {
        var redisDb = distributedRedis.GetDatabase();
        var oneShotKillEventList = await redisDb.HashGetAllAsync(_activityOneShotKillEventListKeyPrefix + activityId);
        return oneShotKillEventList;
    }

    public async Task<List<int>> GetOneShotKillMapProgressAsync(int activityId)
    {
        return await GetOneShotKillDataListAsync(activityId, _activityOneShotKillProgressKeyPrefix);
    }
    public async Task<List<int>> GetOneShotKillTaskProgressAsync(int activityId)
    {
        return await GetOneShotKillDataListAsync(activityId, _activityOneShotKillTaskProgressKeyPrefix);
    }
    public async Task<List<int>> GetOneShotKillLocalProgressAddAsync(int activityId)
    {
        return await GetOneShotKillDataListAsync(activityId, _activityOneShotKillLocalProgressAddKeyPrefix);
    }
    public async Task<List<int>> GetOneShotKillLocalProgressMinusAsync(int activityId)
    {
        return await GetOneShotKillDataListAsync(activityId, _activityOneShotKillLocalProgressMinusKeyPrefix);
    }
    public async Task<Dictionary<int, int>> GetOneShotKillVictoryRecordAsync(int activityId)
    {
        var redisDb = distributedRedis.GetDatabase();
        var oneShotKillLastHourVictoryList = await redisDb.HashGetAllAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId);
        return oneShotKillLastHourVictoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
    }
    public async Task<Dictionary<int, int>> SetOneShotKillVictoryRecordAsync(int activityId, string timeString, int level, int setTo)
    {
        var redisDb = distributedRedis.GetDatabase();
        var timeConfig = serverConfigService.GetActivityConfigById(activityId);
        if (timeConfig == null) return new();
        if (!serverConfigService.TryGetParameterInt(Params.OneShotKillDataRefreshInterval,
                out var dataRefreshInterval))
            return new();
        var startTime = TimeUtils.ParseDateTimeStrToUnixSecond(timeConfig.start_time);
        var victoryTime = TimeUtils.ParseDateTimeStrToUnixSecond(timeString);
        var invervalNumber = (victoryTime - startTime) / dataRefreshInterval;
        var hash = GetOneShotKillVictoryCountHashKey((int)invervalNumber, level);

        await redisDb.HashSetAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId, hash, setTo);
        var oneShotKillLastHourVictoryList = await redisDb.HashGetAllAsync(_activityOneShotKillVictoryCountKeyPrefix + activityId);
        return oneShotKillLastHourVictoryList.ToDictionary(
            item => (int)item.Name, item => (int)item.Value);
    }

    public async Task SetOneShotKillLevelProgressAsync(int activityId, int level, int progress)
    {
        var redisDb = distributedRedis.GetDatabase();
        await redisDb.HashSetAsync(_activityOneShotKillProgressKeyPrefix + activityId, level, progress);
    }

    public async Task SetOneShotKillTaskProgressAsync(int activityId, int taskId, int progress)
    {
        var redisDb = distributedRedis.GetDatabase();
        await redisDb.HashSetAsync(_activityOneShotKillTaskProgressKeyPrefix + activityId, taskId, progress);
    }

    public int GetOneShotKillGlobalProgressAdd(int activityId, List<int> oneShotKillTaskProgressList)
    {
        var taskConfigList = serverConfigService.GetOneShotKillTaskConfigListByActivityId(activityId);
        var leaderConfigList = serverConfigService.GetOneShotKillLeaderConfigListByActivityId(activityId);
        Dictionary<int, int> oneShotKillTaskProgressDict = new();
        for (int i = 0; i < oneShotKillTaskProgressList.Count; i++)
            oneShotKillTaskProgressDict[i] = oneShotKillTaskProgressList[i];

        var currentTime = TimeUtils.GetCurrentTime();
        int globalProgressAdd = 0;

        foreach (var taskConfig in taskConfigList)
        {
            var progress = oneShotKillTaskProgressDict.ContainsKey(taskConfig.task_id)
                ? oneShotKillTaskProgressDict[taskConfig.task_id]
                : 0;
            if (progress >= taskConfig.count_to_complete)
                globalProgressAdd += taskConfig.progress_add;
        }

        foreach (var leaderConfig in leaderConfigList)
        {
            var leaderJoinTimestamp = TimeUtils.ParseDateTimeStrToUnixSecond(leaderConfig.leader_enable_time);
            if (leaderJoinTimestamp <= currentTime)
                globalProgressAdd += leaderConfig.progress_add;
        }

        return globalProgressAdd;
    }

    private int GetOneShotKillVictoryCountHashKey(int intervalNumber, int regionId)
    {
        return /*地区标记*/100000 * regionId + /*时间标记*/intervalNumber;
    }
    private int GetOneShotKillEventHashKey(OneShotKillEventType eventType, int targetId, int intervalNumber)
    {
        return /*事件对象ID*/targetId * 1000000 + /*事件类型标记*/100000 * (int)eventType + /*时间标记*/intervalNumber;
    }

    private async Task<List<int>> GetOneShotKillDataListAsync(int activityId, string dataKeyPrefix)
    {
        var redisDb = distributedRedis.GetDatabase();
        var oneShotKillDataList = await redisDb.HashGetAllAsync(dataKeyPrefix + activityId);
        if (oneShotKillDataList.Length == 0)
            return new();
        var listLength = oneShotKillDataList.Max(item => (int)item.Name) + 1;
        List<int> result = new List<int>(Enumerable.Repeat(0, listLength));
        foreach (var item in oneShotKillDataList)
            result[(int)item.Name] = (int)item.Value;
        redisDb.KeyExpireAsync(dataKeyPrefix + activityId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        return result;
    }

    public async Task AddOneShotMapProgressAsync(int level, int activityId)
    {
        var redisDb = distributedRedis.GetDatabase();
        await redisDb.HashIncrementAsync(_activityOneShotKillProgressKeyPrefix + activityId, level);
        redisDb.KeyExpireAsync(_activityOneShotKillProgressKeyPrefix, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
    }

    public async ValueTask<ActivityLuckyStar?> GetActivityLuckStarDataAsync(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetActivityLuckStarDataAsync(playerId, shardId, trackingOptions));
    }

    public ActivityLuckyStar AddActivityLuckyStarData(long playerId, short shardId, int activityId)
    {
        return activityRepository.AddActivityLuckyStarData(playerId, shardId, activityId);
    }

    private const string _activityFortuneBagLevelDataKey = "activity_fortune_bag_level";

    public async ValueTask<UserFortuneBagInfo?> GetUserFortuneBagInfoAsync(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetUserFortuneBagInfoAsync(playerId, shardId, trackingOptions));
    }

    public UserFortuneBagInfo AddUserFortuneBagInfo(long playerId, short shardId, int activityId)
    {
        return activityRepository.AddUserFortuneBagInfo(playerId, shardId, activityId);
    }

    public async Task<int> GetFortuneBagLevelAsync(int activityId)
    {
        var redisDb = globalCache.GetDatabase();
        var fortuneLevelData = await redisDb.HashGetAsync(_activityFortuneBagLevelDataKey, activityId);
        redisDb.KeyExpireAsync(_activityFortuneBagLevelDataKey, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
        return (int)fortuneLevelData;
    }

    public async Task AddFortuneBagLevelAsync(int inc, int activityId)
    {
        var redisDb = globalCache.GetDatabase();
        await redisDb.HashIncrementAsync(_activityFortuneBagLevelDataKey, activityId, value: inc);
        redisDb.KeyExpireAsync(_activityFortuneBagLevelDataKey, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
    }


    public async Task<FortuneBagDataClient?> FetchFortuneBagActivityInfoAsync(int activityId, long playerId, short shardId)
    {
        var activityConfig = serverConfigService.GetActivityConfigById(activityId);
        if (activityConfig == null)
            return null;
        if (!activityConfig.activity_type.Equals(ActivityType.ActivityFortuneBag))
            return null;
        var fortuneBagLevels = serverConfigService.GetActivityFortuneBagLevelConfigListByActivityId(activityId);
        if (fortuneBagLevels == null)
            return null;
        return await dbCtx.WithRCUDefaultRetry<FortuneBagDataClient?>(async _ =>
        {
            Dictionary<int, int> fortuneLevelRewardDict = new();
            var result = new FortuneBagDataClient();
            result.ActivityId = activityId;
            result.ServerFortuneLevel = await GetFortuneBagLevelAsync(activityId);
            result.UnclaimedFortuneLevelReward = new();
            result.FortuneLevelRewardCount = new();

            var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(shardId, playerId);
            if (timeZoneOffset == null)
                return null;
            var fortuneBagInfo = await GetUserFortuneBagInfoAsync(playerId, shardId, TrackingOptions.NoTracking);
            // 服务器聚福等级带来的奖励
            int fortuneBagLevelRewardClaimStatus =
                fortuneBagInfo == null || fortuneBagInfo.ActivityId != activityId
                    ? 0
                    : fortuneBagInfo.FortuneLevelRewardClaimStatus;

            for (int i = 0; i < fortuneBagLevels.Count; i++)
            {
                var levelConfig = fortuneBagLevels[i];
                if (result.ServerFortuneLevel < levelConfig.fortune_bag_required)
                    break;
                if (fortuneBagLevelRewardClaimStatus > levelConfig.id)
                    continue;
                for (int ii = 0; ii < levelConfig.item_list.Count; ii++)
                {
                    fortuneLevelRewardDict.TryAdd(levelConfig.item_list[ii], 0);
                    fortuneLevelRewardDict[levelConfig.item_list[ii]] += levelConfig.count_list[ii];
                }
            }

            foreach (var item in fortuneLevelRewardDict)
            {
                result.UnclaimedFortuneLevelReward.Add(item.Key);
                result.FortuneLevelRewardCount.Add(item.Value);
            }

            // 已经购买的福袋数量和可以领取的福袋奖励总额
            if (fortuneBagInfo == null || fortuneBagInfo.ActivityId != activityId)
            {
                result.BagBought = 0;
                result.UnclaimedDiamondCountInFortuneBag = 0;
                result.FortuneBagDiamondAvailableTomorrow = 0;
                result.FortuneLevelRewardClaimStatus = 0;
                return result;
            }

            result.FortuneLevelRewardClaimStatus = fortuneBagInfo.FortuneLevelRewardClaimStatus;
            var bagConfig = serverConfigService.GetActivityFortuneBagConfigByActivityId(fortuneBagInfo.ActivityId);
            var bagContentList = bagConfig == null ? new List<int>() { } : bagConfig.fortune_bag_diamond_count;
            foreach (var bag in fortuneBagInfo.FortuneBags)
            {
                result.BagBought += bag.BagCount;
                var dayDifference = TimeUtils.GetDayDiffBetween(
                    TimeUtils.GetCurrentTime(), bag.AcquireTime, timeZoneOffset.Value, 0);
                for (int claimTime = bag.ClaimStatus;
                     claimTime <= dayDifference && claimTime < bagContentList.Count;
                     claimTime++)
                {
                    result.UnclaimedDiamondCountInFortuneBag += bagContentList[claimTime] * bag.BagCount;
                }

                if (dayDifference + 1 < bagContentList.Count)
                    result.FortuneBagDiamondAvailableTomorrow += bagContentList[dayDifference + 1] * bag.BagCount;
            }

            return result;
        });
    }


    public async Task AddPiggyBankExpAsync(long playerId, short shardId, int exp)
    {
        var piggyBankStatus = await GetPiggyBankStatusAsync(playerId, shardId);
        if (piggyBankStatus == null)
            piggyBankStatus = CreateDefaultPiggyBank(playerId, shardId);
        piggyBankStatus.Exp += exp;
    }

    public async Task<ActivityPiggyBank?> GetPiggyBankStatusAsync(long playerId, short? shardId)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetPiggyBankStatusAsync(playerId, shardId));
    }

    public ActivityPiggyBank CreateDefaultPiggyBank(long playerId, short shardId)
    {
        return activityRepository.CreateDefaultPiggyBank(playerId, shardId);
    }

    public async ValueTask<ActivityUnrivaledGod?> GetUnrivaledGodDataAsync(
        long playerId,
        short shardId,
        int activityId,
        TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ =>
            activityRepository.GetUnrivaledGodDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public async Task<ActivityUnrivaledGod> CreateDefaultUnrivaledGodDataAsync(
        long playerId,
        short shardId,
        int activityId)
    {
        var oldActivities
            = await dbCtx.WithDefaultRetry(_ => activityRepository.GetAllUnrivaledGodDataAsync(playerId, shardId));
        var totalScore = 0;
        if (!oldActivities.IsNullOrEmpty())
        {
            totalScore = oldActivities.Sum(activityData => activityData.ScorePoint);
            // 清空旧数据
            activityRepository.ClearUnrivaledGodDataList(oldActivities);
        }

        if (totalScore > 0)
        {
            logger.LogInformation($"Player {playerId} Inherit Old Unrivaled God Score: {totalScore}");
        }

        var unrivaledGodData = activityRepository.CreateDefaultUnrivaledGodData(playerId, shardId, activityId);
        unrivaledGodData.ScorePoint = totalScore;
        return unrivaledGodData;
    }

    public async ValueTask<ActivityCoopBossInfo?> GetCoopBossDataAsync(
        long playerId,
        short shardId,
        int activityId,
        TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ =>
            activityRepository.GetCoopBossDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public ActivityCoopBossInfo CreateDefaultCoopBossData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultCoopBossData(playerId, shardId, activityId);
    }

    public async Task<int> TryOpenBossActivityAsync(long playerId, short shardId, ActivityTimeConfig? coopBossTimeConfig)
    {
        if (coopBossTimeConfig == null)
            return (int)ErrorKind.SUCCESS;
        var activityId = coopBossTimeConfig.id;
        var coopBossStatus = await GetCoopBossDataAsync(playerId, shardId, activityId, TrackingOptions.Tracking);
        if (coopBossStatus == null)
            coopBossStatus = CreateDefaultCoopBossData(playerId, shardId, activityId);
        var currentTimeStamp = TimeUtils.GetCurrentTime();
        if (!serverConfigService.TryGetParameterInt(Params.CoopBossReleaseDuration, out var coopBossTaskDuration))
            return (int)ErrorKind.NO_PARAM_CONFIG;

        // 检查游戏进度是否达到活动解锁要求了
        // TODO 这里应该可以从外部拿到LevelData
        var userAsset = await userAssetService.GetUserAssetsSimpleAsync(shardId, playerId);
        if (userAsset == null)
            return (int)ErrorKind.NO_USER_ASSET;
        var levelData = userAsset.LevelData;
        if (levelData.Level < coopBossTimeConfig.unlock_user_level)
            return (int)ErrorKind.SUCCESS;

        // 有活动入口了就不再随了
        if (currentTimeStamp - coopBossStatus.LastLevelActivateTime < coopBossTaskDuration)
            return (int)ErrorKind.SUCCESS;

        // 检查一下跨天刷新
        var newDay = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), coopBossStatus.LastRefreshTime,
            userAsset.TimeZoneOffset, 0) > 0;
        if (newDay)
        {
            coopBossStatus.LastRefreshTime = TimeUtils.GetCurrentTime();
            coopBossStatus.GameEndCountToday = 0;
            coopBossStatus.RefreshCountToday = 0;
        }

        // 到了每天刷新的上限就不刷新了
        if (!serverConfigService.TryGetParameterInt(Params.CoopBossOpenCountPerDay, out var coopBossOpenCountPerDay))
            return (int)ErrorKind.NO_PARAM_CONFIG;
        if (coopBossStatus.RefreshCountToday >= coopBossOpenCountPerDay)
            return (int)ErrorKind.SUCCESS;

        // 检查都通过了，刷一下康康刷不刷得到
        if (!serverConfigService.TryGetParameterInt(Params.CoopBossOpenChance, out var coopBossOpenChance))
            return (int)ErrorKind.NO_PARAM_CONFIG;

        // 读一下配置
        var coopBossRewardList = serverConfigService.GetCoopBossRewardConfigListByActivityId(activityId);
        if (coopBossRewardList == null)
            return (int)ErrorKind.NO_COOP_BOSS_REWARD_CONFIG;
        var maxLevel = coopBossRewardList.Count - 1;

        // 触发保底的话直接就给
        if (coopBossStatus.DrewCount >= coopBossOpenChance - 1)
        {
            coopBossStatus.DrewCount = 0;
            coopBossStatus.LastLevel = CalculateCoopBossLevelByLastLevel(coopBossStatus.LastLevel, maxLevel);
            coopBossStatus.LastLevelActivateTime = currentTimeStamp;
            ++coopBossStatus.RefreshCountToday;
            return (int)ErrorKind.SUCCESS;
        }

        // 抽一下
        var random = new Random();
        var index = random.Next(0, coopBossOpenChance);
        if (index >= 1)
        {
            // 没抽到, 计数+1
            ++coopBossStatus.DrewCount;
            return (int)ErrorKind.SUCCESS;
        }

        // 抽到了，计数清空
        coopBossStatus.DrewCount = 0;
        coopBossStatus.LastLevel = CalculateCoopBossLevelByLastLevel(coopBossStatus.LastLevel, maxLevel);
        coopBossStatus.LastLevelActivateTime = currentTimeStamp;
        ++coopBossStatus.RefreshCountToday;
        return (int)ErrorKind.SUCCESS;
    }

    private int CalculateCoopBossLevelByLastLevel(int lastLevel, int maxLevel)
    {
        if (lastLevel < 0)
            return 0;
        // 这块的概率不太方便配置，直接写死
        var newLevel = 0;
        var random = new Random();
        var index = random.Next(0, 4);
        newLevel = index switch
        {
            <= 1 => lastLevel + 1,
            2 => lastLevel + 2,
            3 => lastLevel - 1,
            _ => lastLevel
        };
        newLevel = newLevel > maxLevel ? maxLevel : newLevel;
        newLevel = newLevel < 0 ? 0 : newLevel;
        return newLevel;
    }


    public async ValueTask<ActivityTreasureMaze?> GetTreasureMazeDataAsync(
        long playerId,
        short shardId,
        int activityId)
    {
        return await dbCtx.WithDefaultRetry(_ =>
            activityRepository.GetTreasureMazeDataAsync(playerId, shardId, activityId));
    }

    public ActivityTreasureMaze CreateTreasureMazeData(long playerId, short shardId, int activityId, long whenStarted)
    {
        return activityRepository.CreateTreasureMazeData(playerId, shardId, activityId, whenStarted - 86400);
    }

    public async ValueTask<ActivityEndlessChallenge?> GetEndlessChallengeDataAsync(
        long playerId,
        short shardId,
        int activityId)
    {
        return await dbCtx.WithDefaultRetry(_ =>
            activityRepository.GetEndlessChallengeDataAsync(playerId, shardId, activityId));
    }

    public ActivityEndlessChallenge CreateDefaultEndlessChallengeData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultEndlessChallengeData(playerId, shardId, activityId);
    }


    public List<ActivityTimeConfig> GetActivitiesByType(string activityType)
    {
        var configList = serverConfigService.GetActivityConfigList();
        return configList.Where(config => config.activity_type == activityType).ToList();
    }

    public List<ActivityTimeConfig> GetOpeningActivities(string gameVersion, DateTime? now = null)
    {
        var configList = serverConfigService.GetActivityConfigList();
        return configList.Where(config => IsOpen(config, gameVersion, now)).ToList();
    }

    public ActivityTimeConfig? GetOpeningActivityByType(string activityType, string gameVersion, DateTime? now = null)
    {
        var openingActivities = GetOpeningActivities(gameVersion, now);
        return openingActivities.FirstOrDefault(activityTimeConfig =>
            activityTimeConfig.activity_type == activityType);
    }

    // 暂时使用Utc时间
    public bool IsOpen(ActivityTimeConfig config, string gameVersion, DateTime? now = null)
    {
        now ??= DateTime.UtcNow;
        var startTime = TimeUtils.ParseDateTimeStr(config.start_time);
        var endTime = TimeUtils.ParseDateTimeStr(config.end_time);

        // 正常情况下这个不会是null，但是先更服务器再更配置的话会是null，这里做一下处理。
        if (config.min_version != null &&
            gameVersion.CompareVersionStrServer(config.min_version) < 0)
            return false;
        return now >= startTime && now < endTime;
    }

    public static bool IsOpenOrAlreadyClosed(ActivityTimeConfig config)
    {
        var now = DateTime.UtcNow;
        var startTime = TimeUtils.ParseDateTimeStr(config.start_time);
        return now >= startTime;
    }

    public static bool IsAlreadyClosed(ActivityTimeConfig config)
    {
        var now = DateTime.UtcNow;
        var endTime = TimeUtils.ParseDateTimeStr(config.end_time);
        return now >= endTime;
    }

    // 锁定活动，一些活动可以提前几天锁定购买操作
    public bool IsLocked(ActivityTimeConfig config)
    {
        var now = DateTime.UtcNow;
        var lockTime = TimeUtils.ParseDateTimeStr(config.lock_time);
        return now >= lockTime;
    }

    public async Task AddTaskProgressToActiveActivityTask(
        string taskId,
        long playerId, short shardId, int count, int timeZoneOffset, string gameVersion)
    {
        if (count <= 0)
            return;

        // 无双神将活动任务
        var unrivaledGodData = await GetOpeningUnrivaledGodActivityData(playerId, shardId, gameVersion);
        if (unrivaledGodData != null)
            RecordUnrivaledGodTask(unrivaledGodData, taskId, count, timeZoneOffset);

        // CSGO开箱活动
        var csgoLotteryData = await GetOpeningCsgoStyleLotteryData(playerId, shardId, gameVersion);
        if (csgoLotteryData != null)
            RecordCsgoStyleLotteryTask(csgoLotteryData, taskId, count, timeZoneOffset);
    }

    #region 无双神将

    private async Task<ActivityUnrivaledGod?> GetOpeningUnrivaledGodActivityData(long playerId, short shardId, string gameVersion)
    {
        var unrivaledGodTimeConfig = GetOpeningActivityByType(ActivityType.ActivityUnrivaledGod, gameVersion);
        if (unrivaledGodTimeConfig != null)
        {
            var activityId = unrivaledGodTimeConfig.id;
            var unrivaledGodData = await activityRepository.GetUnrivaledGodDataAsync(playerId, shardId, activityId, TrackingOptions.Tracking);
            if (unrivaledGodData == null)
                unrivaledGodData = activityRepository.CreateDefaultUnrivaledGodData(playerId, shardId, activityId);
            return unrivaledGodData;
        }

        return null;
    }

    public bool CheckRefreshUnrivaledGodTask(ActivityUnrivaledGod unrivaledGodData, int timeZoneOffset)
    {
        // 任务跨天刷新下
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool databaseChanged = false;
        foreach (var pair in unrivaledGodData.TaskRecord)
        {
            var taskConfig = serverConfigService.GetUnrivaledGodTaskConfig(unrivaledGodData.ActivityId, pair.Key);
            if (taskConfig != null && taskConfig.is_daily)
            {
                if (TimeUtils.GetDayDiffBetween(currentTime, pair.Value.UpdatedAt, timeZoneOffset,
                        0) > 0)
                {
                    pair.Value.Progress = 0;
                    pair.Value.Claimed = false;
                    databaseChanged |= true;
                }
            }
        }

        return databaseChanged;
    }

    public bool RecordUnrivaledGodTask(
        ActivityUnrivaledGod unrivaledGodData,
        string taskKey,
        int count,
        int timeZoneOffset)
    {
        var taskConfig = serverConfigService.GetUnrivaledGodTaskConfig(unrivaledGodData.ActivityId, taskKey);
        if (taskConfig == null)
            return false;
        // 首先检查下是不是跨天刷新了
        bool databaseChanged = CheckRefreshUnrivaledGodTask(unrivaledGodData, timeZoneOffset);
        if (unrivaledGodData.TaskRecord.TryGetValue(taskKey, out var record))
        {
            if (record.Progress < taskConfig.target_progress)
            {
                databaseChanged |= true;
                if (taskKey.Equals(ActivityTaskKeys.AccuUnrivaledGodDrawReward))
                    record.Progress = Math.Min(record.Progress + count, taskConfig.target_progress);
                else
                    record.Progress += count;
                record.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }
        else
        {
            databaseChanged |= true;
            unrivaledGodData.TaskRecord.Add(taskKey,
                new UnrivaledGodTask()
                {
                    Progress = count,
                    Claimed = false,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });
        }

        if (databaseChanged)
            dbCtx.Entry(unrivaledGodData).Property(t => t.TaskRecord).IsModified = true;

        return databaseChanged;
    }

    #endregion

    #region 宝藏迷宫

    // (是否*没有*配置错误, 是否有数据更新)
    public (bool, bool) CheckRefreshTreasureMazeData(ActivityTreasureMaze treasureBossData, long timeZoneOffset)
    {
        if (!serverConfigService.TryGetParameterInt(Params.MaxTreasureMazeKeyStack, out int maxKeyStack))
            return (false, false);
        if (!serverConfigService.TryGetParameterInt(Params.TreasureMazeKeyPerDay, out int keyPerDay))
            return (false, false);
        bool dataChanged = false;
        var currentTime = TimeUtils.GetCurrentTime();
        int dayDiff = TimeUtils.GetDayDiffBetween(
            TimeUtils.GetCurrentTime(), treasureBossData.LastGameKeyTimestamp,
            timeZoneOffset, 0);
        if (dayDiff > 0)
        {
            dataChanged = true;
            treasureBossData.GameKeyCount += dayDiff * keyPerDay;
            treasureBossData.GameKeyCount = Math.Min(treasureBossData.GameKeyCount, maxKeyStack);
            treasureBossData.LastGameKeyTimestamp = currentTime;
        }

        dayDiff = TimeUtils.GetDayDiffBetween(
            TimeUtils.GetCurrentTime(), treasureBossData.LastAwayGameTimestamp,
            timeZoneOffset, 0);
        if (dayDiff > 0)
        {
            dataChanged = true;
            treasureBossData.AwayGameCountToday = 0;
            treasureBossData.LastAwayGameTimestamp = currentTime;
        }

        return (true, dataChanged);
    }

    #endregion

    #region 老虎机活动

    public async Task<ActivitySlotMachine?> GetSlotMachineDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetSlotMachineDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public async Task<List<ActivitySlotMachine>> GetSlotMachineListByPlayerAsync(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetSlotMachineListByPlayerAsync(playerId, shardId, trackingOptions));
    }

    public ActivitySlotMachine CreateDefaultSlotMachineData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultSlotMachineData(playerId, shardId, activityId);
    }


    public (List<int>?, List<int>?, int) ClaimSlotMachineReward(int activityId, int rewardMultiplier, List<int> rewards)
    {
        var prizePoolConfig = serverConfigService.GetActivitySlotMachineDrawRewardConfigByActivityId(activityId);
        if (prizePoolConfig == null)
            return (null, null, 0);

        List<int> itemList = new(),
            countList = new();
        int point = 0;
        foreach (var id in rewards)
        {
            var rewardConfig = prizePoolConfig[id];
            itemList.Add(rewardConfig.item_id);
            countList.Add(rewardConfig.item_count * rewardMultiplier);
            point += rewardConfig.point * rewardMultiplier;
        }
        return (itemList, countList, point);
    }

    public int RandomizeDrawReward(List<ActivitySlotMachineDrawRewardConfig> pool, Random rand, int forceQuality = -1, int avoidReward = -1)
    {
        bool noQualityRequirement = forceQuality == -1;
        var prizePool = pool.ToList();
        if (!noQualityRequirement)
        {
            prizePool = pool.Where(item => noQualityRequirement || item.quality == forceQuality).ToList();
        }
        if (prizePool.Count > 1)
            prizePool.RemoveAll(item => item.reward_id == avoidReward);
        int totalWeight = prizePool.Sum(item => item.weight);
        if (totalWeight == 0) // 不应该出现的情况
            return pool[0].reward_id;
        var random = rand.Next(0, totalWeight);
        int idx = 0;
        while (random >= 0)
        {
            random -= prizePool[idx].weight;
            idx++;
        }

        return prizePool[idx - 1].reward_id;
    }

    #endregion

    #region 一击必杀

    public async Task<ActivityOneShotKill?> GetOneShotKillDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetOneShotKillDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public ActivityOneShotKill CreateDefaultOneShotKillData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateOneShotKillData(playerId, shardId, activityId);
    }

    #endregion

    #region 灵犀探宝活动

    public async Task<ActivityTreasureHunt?> GetTreasureHuntDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetTreasureHuntDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public async Task<List<ActivityTreasureHunt>> GetTreasureHuntListByPlayerAsync(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetTreasureHuntListByPlayerAsync(playerId, shardId, trackingOptions));
    }

    public ActivityTreasureHunt CreateDefaultTreasureHuntData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultTreasureHuntData(playerId, shardId, activityId);
    }

    /// <summary>
    /// 生成灵犀探宝奖池（9格）
    /// </summary>
    public List<TreasureHuntSlot> GenerateTreasureHuntPool(int activityId)
    {
        var rand = new Random();
        var config = serverConfigService.GetTreasureHuntConfigByActivityId(activityId);
        var drawConfigs = serverConfigService.GetTreasureHuntDrawConfigByActivityId(activityId);
        if (config == null || drawConfigs == null)
            return new List<TreasureHuntSlot>();

        var slots = new List<TreasureHuntSlot>();
        // 按 quality 分组
        var configsByQuality = drawConfigs
            .GroupBy(c => c.quality)
            .ToDictionary(g => g.Key, g => g.ToList());

        // pool_count_by_quality[0..3] 对应 quality 0..3
        for (int quality = 0; quality < config.pool_count_by_quality.Length; quality++)
        {
            int count = config.pool_count_by_quality[quality];
            if (!configsByQuality.TryGetValue(quality, out var qualityPool) || qualityPool.Count == 0)
                continue;

            for (int i = 0; i < count; i++)
            {
                // 按 weight 加权随机选择 reward_id
                var selectConfig = qualityPool.WeightedRandomSelectOne(c => c.weight);
                bool isVariant = false;

                // 非无双品质可以变异
                if (quality < (int)ItemQuality.Unrivaled)
                {
                    float variantRoll = (float)rand.NextDouble();
                    if (variantRoll < config.p_variant)
                    {
                        // 变异：替换为高一品质的奖励
                        int higherQuality = quality + 1;
                        if (configsByQuality.TryGetValue(higherQuality, out var higherPool) && higherPool.Count > 0)
                        {
                            selectConfig = higherPool.WeightedRandomSelectOne(c => c.weight);
                            isVariant = true;
                        }
                    }
                }

                if (selectConfig == null)
                    throw new Exception("Failed to select treasure hunt reward config. for quality " + quality);

                slots.Add(new TreasureHuntSlot
                {
                    Id = selectConfig.reward_id,
                    IsVariant = isVariant,
                    HasOpen = false
                });
            }
        }

        // 打乱一下顺序
        slots.Shuffle();
        return slots;
    }

    /// <summary>
    /// 随机抽取一个未开启的格子
    /// </summary>
    public int? DrawRandomTreasureHuntSlot(List<TreasureHuntSlot> slots)
    {
        var rand = new Random();
        var unopenedIndices = slots
            .Select((slot, index) => new { slot, index })
            .Where(x => !x.slot.HasOpen)
            .Select(x => x.index)
            .ToList();

        if (unopenedIndices.Count == 0)
            return null;

        int randomIndex = rand.Next(0, unopenedIndices.Count);
        return unopenedIndices[randomIndex];
    }

    /// <summary>
    /// 计算抽奖消耗（钻石）
    /// </summary>
    public int CalculateTreasureHuntDrawCost(int activityId, int openedCount)
    {
        var config = serverConfigService.GetTreasureHuntConfigByActivityId(activityId);
        if (config == null)
            return int.MaxValue;
        return config.draw_diamond_base + openedCount * config.draw_diamond_n;
    }

    /// <summary>
    /// 计算刷新奖池消耗（钻石）
    /// </summary>
    public int CalculateTreasureHuntRefreshCost(int activityId, int refreshCount)
    {
        var config = serverConfigService.GetTreasureHuntConfigByActivityId(activityId);
        if (config == null)
            return int.MaxValue;
        int cost = config.refresh_diamond_base + refreshCount * config.refresh_diamond_n;
        return Math.Min(cost, config.refresh_diamond_max);
    }

    /// <summary>
    /// 领取灵犀探宝积分奖励
    /// </summary>
    public (List<int>?, List<int>?) ClaimTreasureHuntScoreReward(int activityId, int rewardId)
    {
        var pointRewardConfigs = serverConfigService.GetTreasureHuntPointRewardConfigByActivityId(activityId);
        if (pointRewardConfigs == null)
            return (null, null);

        var rewardConfig = pointRewardConfigs.FirstOrDefault(c => c.reward_id == rewardId);
        if (rewardConfig == null)
            return (null, null);

        return (new List<int> { rewardConfig.item_id }, new List<int> { rewardConfig.item_count });
    }

    /// <summary>
    /// 获取灵犀探宝奖励物品（根据 reward_id）
    /// </summary>
    public (int itemId, int itemCount, int point)? GetTreasureHuntRewardById(int activityId, int rewardId)
    {
        var drawConfigs = serverConfigService.GetTreasureHuntDrawConfigByActivityId(activityId);
        if (drawConfigs == null)
            return null;

        var config = drawConfigs.FirstOrDefault(c => c.reward_id == rewardId);
        if (config == null)
            return null;

        return (config.item_id, config.item_count, config.point);
    }

    #endregion
    
    #region RPG玩法活动
    

    public async Task<ActivityRpgGame?> GetRpgGameDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetRpgGameDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public ActivityRpgGame CreateDefaultRpgGameData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultRpgGameData(playerId, shardId, activityId);
    }
    #endregion

    #region Loog玩法活动

    public async Task<ActivityLoogGame?> GetLoogGameDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetLoogGameDataAsync(playerId, shardId, activityId, trackingOptions));
    }

    public ActivityLoogGame CreateDefaultLoogGameData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultLoogGameData(playerId, shardId, activityId);
    }
    #endregion

    #region CsgoStyleLottery活动

    public async Task<ActivityCsgoStyleLottery?> GetCsgoStyleLotteryDataAsync(long playerId, short shardId, int activityId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetCsgoStyleLotteryDataAsync(playerId, shardId, activityId, trackingOptions));
    }
    
    public ActivityCsgoStyleLottery CreateDefaultCsgoStyleLotteryData(long playerId, short shardId, int activityId)
    {
        return activityRepository.CreateDefaultCsgoStyleLotteryData(playerId, shardId, activityId);
    }

    /// <summary>
    /// 检查并刷新CsgoStyleLottery任务（日常任务跨天重置）
    /// </summary>
    public bool CheckRefreshCsgoStyleLotteryTask(ActivityCsgoStyleLottery lotteryData, int timeZoneOffset)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool databaseChanged = false;
        foreach (var pair in lotteryData.TaskRecord)
        {
            var taskConfig = serverConfigService.GetCsgoLotteryTaskConfigByKey(lotteryData.ActivityId, pair.Key);
            if (taskConfig != null && taskConfig.is_daily)
            {
                if (TimeUtils.GetDayDiffBetween(currentTime, pair.Value.UpdatedAt, timeZoneOffset, 0) > 0)
                {
                    pair.Value.Progress = 0;
                    pair.Value.Claimed = false;
                    databaseChanged = true;
                }
            }
        }
        return databaseChanged;
    }

    /// <summary>
    /// 记录CsgoStyleLottery任务进度
    /// </summary>
    public bool RecordCsgoStyleLotteryTask(
        ActivityCsgoStyleLottery lotteryData,
        string taskKey,
        int count,
        int timeZoneOffset)
    {
        var taskConfig = serverConfigService.GetCsgoLotteryTaskConfigByKey(lotteryData.ActivityId, taskKey);
        if (taskConfig == null)
            return false;

        bool databaseChanged = CheckRefreshCsgoStyleLotteryTask(lotteryData, timeZoneOffset);

        if (lotteryData.TaskRecord.TryGetValue(taskKey, out var record))
        {
            if (record.Progress < taskConfig.target_progress)
            {
                databaseChanged = true;
                record.Progress = Math.Min(record.Progress + count, taskConfig.target_progress);
                record.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }
        else
        {
            databaseChanged = true;
            lotteryData.TaskRecord.Add(taskKey,
                new CsgoStyleLotteryTask()
                {
                    Progress = Math.Min(count, taskConfig.target_progress),
                    Claimed = false,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });
        }

        if (databaseChanged)
            dbCtx.Entry(lotteryData).Property(t => t.TaskRecord).IsModified = true;

        return databaseChanged;
    }
    
    /// <summary>
    /// 获取活动中的CsgoStyleLottery数据
    /// </summary>
    private async Task<ActivityCsgoStyleLottery?> GetOpeningCsgoStyleLotteryData(long playerId, short shardId, string gameVersion)
    {
        var timeConfig = GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, gameVersion);
        if (timeConfig != null)
        {
            var activityId = timeConfig.id;
            var data = await activityRepository.GetCsgoStyleLotteryDataAsync(playerId, shardId, activityId, TrackingOptions.Tracking);
            if (data == null)
                data = activityRepository.CreateDefaultCsgoStyleLotteryData(playerId, shardId, activityId);
            return data;
        }
        return null;
    }

    /// <summary>
    /// 获取所有的csgo lottery数据
    /// </summary>
    public async Task<List<ActivityCsgoStyleLottery>> GetCsgoLotteryDataList(long playerId, short shardId, TrackingOptions trackingOptions)
    {
        return await dbCtx.WithDefaultRetry(_ => activityRepository.GetCsgoLotteryDataList(playerId, shardId, trackingOptions));
    }

    #endregion
}
