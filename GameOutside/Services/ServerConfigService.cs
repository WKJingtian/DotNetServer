using GameExternal;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.Extensions.Options;
using NuGet.Protocol;

#pragma warning disable CS8618

namespace GameOutside;

public class ServerConfigService
{
    public class GameDataTable
    {
        public List<ConfigUnit> ConfigFile { get; set; }
        public bool EnableLocalCache { get; set; }
    }

    [Serializable]
    public class LocalConfig
    {
        public GameDataTable GameDataTable { get; set; }
    }

    [Serializable]
    public class ConfigUnit
    {
        public string config_key { get; set; }
        public string hash { get; set; }
        public string content { get; set; }
    }

    [Serializable]
    public class ConfigJsonType<T>
    {
        public string key;
        public List<T> content;
    }

    private List<ConfigUnit> ConfigFile;
    private IOptionsMonitor<GameDataTable> _config;
    private const string localCachedPath = "./Resources/Server.json";
    private ILogger<ServerConfigService> _logger;

    public ServerConfigService(IOptionsMonitor<GameDataTable> config, ILogger<ServerConfigService> logger)
    {
        _logger = logger;
        _config = config;
        _config.OnChange(ConfigChanged);

        UpdateConfig();
    }

    private void ConfigChanged(GameDataTable updatedConfig)
    {
        UpdateConfig();
    }

    public void UpdateConfig()
    {
        var config = _config.CurrentValue;

        if (config.EnableLocalCache && File.Exists(localCachedPath))
        {
            var text = File.ReadAllText(localCachedPath);
            var localConfig = text.FromJson<LocalConfig>();

            _logger.LogInformation($"从本地 {localCachedPath} 更新配置表");
            ConfigFile = localConfig.GameDataTable.ConfigFile;
            Bootstrap();
        }
        else if (config.ConfigFile is not null && config.ConfigFile.Count > 0)
        {
            _logger.LogInformation($"从 nacos 更新配置... {config.ConfigFile.Count}");

            ConfigFile = config.ConfigFile;
            Bootstrap();
        }
    }

    #region 一些配置存储

    private GameConfigByIdAccessor<CommodityConfig> _commodityConfigByIdAccessor;
    private GameConfigByIdAccessor<ItemConfig> _itemConfigByIdAccessor;
    private GameConfigByIdAccessor<UserLevelConfig> _userLevelConfigAccessor;
    private GameConfigByKeyAccessor<BuildingCardConfig> _buildingCardConfigByKeyAccessor;

    // TODO 1.2.0版本上线后删除
    private GameConfigAccessor<ScoreRewardConfig> _scoreRewardAccessor;
    private GameConfigAccessor<ScoreRewardConfig> _scoreRewardV1Accessor;
    private GameConfigAccessor<EndlessRewardConfig> _endlessScoreRewardAccessor;
    private GameConfigByIdAccessor<TreasureBoxConfig> _treasureBoxConfigByIdAccessor;
    private Dictionary<ItemQuality, ItemConfig> _magicCardConfigByQuality;
    private GameConfigByIdAccessor<NewCardConfig> _newCardConfigByQualityAccessor;
    private GameConfigByIdAccessor<FixedMapConfig> _fixedMapConfigByIdAccessor;
    private GameConfigByIdAccessor<StarTask> _starTaskConfigByIdAccessor;
    
    private GameConfigAccessor<DelayBoxDropConfig> _delayBoxDropConfigAccessor;
    private int _delayBoxNonLoopCount = 0;
    
    private GameConfigAccessor<HeroConfig> _heroConfigAccessor;
    private GameConfigAccessor<DailyCommodityConfig> _dailyCommodityConfigAccessor;
    private GameConfigByIdAccessor<MapConfig> _mapConfigAccessor;
    private GameConfigAccessor<AttendanceRewardConfig> _attendanceRewardConfigAccessor;
    private GameConfigAccessor<DivisionRewardConfig> _divisionRewardConfigAccessor;
    private Dictionary<int, List<RankRewardConfig>> _rankRewardConfigByDivision;
    private GameConfigAccessor<ExploreBoxConfig> _exploreBoxConfigAccessor;
    private GameConfigAccessor<DifficultySelectionConfig> _difficultySelectionConfigAccessor;
    private GameConfigAccessor<BattlePassConfig> _battlePassConfigAccessor;
    private GameConfigAccessor<BattlePassTimeConfig> _battlePassTimeConfigAccessor;
    private GameConfigByIdAccessor<AchievementConfig> _achievementConfigAccessor;
    private GameConfigAccessor<BeginnerTaskDayConfig> _beginnerTaskDayConfigAccessor;
    private GameConfigAccessor<BeginnerTaskConfig> _beginnerTaskConfigAccessor;
    private GameConfigAccessor<PresetDailyStoreItemConfig> _presetDailyStoreConfigAccessor;
    private GameConfigByIdAccessor<WinRewardConfig> _winRewardConfigByIdAccessor;
    private GameConfigAccessor<DifficultyConfig> _difficultyConfigAccessor;
    private GameConfigAccessor<MallAdConfig> _mallAdsConfigAccessor;
    private GameConfigByIdAccessor<BuildingConfig> _buildingConfigByIdAccessor;
    private GameConfigByIdAccessor<IdleRewardConfig> _idleRewardConfigAccessor;
    private GameConfigByIdAccessor<DecorativeItemPoolConfig> _decorativeItemPoolAccessor;
    private GameConfigAccessor<ActivityPiggyBankConfig> _piggyBankConfigAccessor;
    private GameConfigByKeyAccessor<ResourceConfig> _resourceConfigByKeyAccessor;
    private GameConfigAccessor<BanPlayerTimeConfig> _banPlayerTimeConfigAccessor;
    
    private List<Dictionary<int, BuildingScoreConfig>> _buildingScoreConfigList;
    
    private Dictionary<int, ActivitySlotMachineConfig> _slotMachineConfigByActivityId;
    private Dictionary<int, int> _slotMachineDoubleUpItemLevelDict;
    private Dictionary<int, List<ActivitySlotMachineDrawConfig>> _slotMachineDrawConfigByActivityId;
    private Dictionary<int, List<ActivitySlotMachineDrawRewardConfig>> _slotMachineDrawRewardPoolByActivityId;
    private Dictionary<int, List<ActivitySlotMachinePointRewardConfig>> _slotMachinePointRewardConfigByActivityId;
    
    // CsgoLottery related config containers
    private Dictionary<int, List<CsgoLotteryConfig>> _csgoLotteryConfigByActivityId;
    private Dictionary<int, List<CsgoLotteryPointConfig>> _csgoLotteryPointConfigByActivityId;
    private Dictionary<int, List<CsgoLotteryPassConfig>> _csgoLotteryPassConfigByActivityId;
    private Dictionary<int, List<CsgoLotteryTaskConfig>> _csgoLotteryTaskConfigByActivityId;
    
    private GameConfigByIdAccessor<TreasureHuntConfig> _treasureHuntConfigByIdAccessor;
    private Dictionary<int, List<TreasureHuntDrawConfig>> _treasureHuntDrawConfigByActivityId;
    private Dictionary<int, List<TreasureHuntPointRewardConfig>> _treasureHuntPointRewardConfigByActivityId;
    
    private Dictionary<int, List<ActivityOneShotKillMapConfig>> _oneShotKillMapByActivityId;
    private Dictionary<int, List<ActivityOneShotKillLeaderConfig>> _oneShotKillLeaderByActivityId;
    private Dictionary<int, List<ActivityOneShotKillTaskConfig>> _oneShotKillTaskByActivityId;
    private Dictionary<int, GeneralReward> _oneShotKillUltimateRewardConfigByActivityId;
    
    private GameConfigAccessor<EnemyConfig> _enemyConfigAccessor;
    private GameConfigAccessor<FightBuffConfig> _fightBuffConfigAccessor;

    private readonly Dictionary<string, string> _configTextByKey = new();
    private readonly Dictionary<int, List<int>> _rewardsDictionary = new();
    private readonly List<DivisionConfig> _divisionConfigList = new();
    private readonly List<ItemConfig> _fightCardItemList = new();
    private readonly List<ItemConfig> _buildingCardItemList = new();
    private readonly Dictionary<string, List<FightCardConfig>> _fightCardConfigDic = new();
    private readonly Dictionary<string, int> _cardIdByDetailKeyDict = new();
    private readonly Dictionary<string, int> _socScoreDictionary = new();
    private readonly Dictionary<string, string> _paramDictionary = new();
    private readonly Dictionary<int, List<BattlePassConfig>> _battlePassConfigByPassId = new();
    private readonly Dictionary<string, Dictionary<string, AchievementConfig>> _achievementConfigDictionary = new();
    private readonly Dictionary<string, HeroConfig> _heroConfigByKey = new();
    private readonly List<DailyTaskConfig> _dailyTaskConfigList = new();
    private readonly List<ActiveScoreLevelConfig> _dailyActiveScoreRewardConfigList = new();
    private List<Dictionary<string, BattleExpRules>> _battleExpRulesConfigList = new();

    private long _piggyBankAllRewardClaimedStatus = 0;
    
    private int _totalDivisionCount = 0;

    private readonly Dictionary<ItemQuality, List<int>> _decorativeItemListByQuality = new();
    private GameConfigByIdAccessor<ActivityTimeConfig> _activityTimeConfigByIdAccessor;
    private readonly Dictionary<int, List<ActivityLuckyStarConfig>> _activityLuckyStarConfigByActivityId = new();
    private readonly Dictionary<int, List<ActivityFortuneBagLevelConfig>> _activityFortuneBagLevelConfigByActivityId = new();
    private readonly Dictionary<int, ActivityFortuneBagConfig> _activityFortuneBagConfigByActivityId = new();
    private readonly Dictionary<int, Dictionary<int, TreasureMazeDifficultyConfig>>
        _treasureMazeDifficultyConfigByActivityId = new();
    private readonly Dictionary<int, Dictionary<int, TreasureMazeLootConfig>>
        _treasureMazeLootConfigByActivityId = new();
    private readonly Dictionary<int, Dictionary<int, List<RewardPoolItem>>>
        _treasureMazeLootRewardPoolByActivityId = new();
    
    private GameConfigByIdAccessor<ItemPackageConfig> _packageItemConfigByIdAccessor;
    private GameConfigByKeyAccessor<IapCommodityConfig> _skuItemConfigByKeyAccessor; 
    private GameConfigAccessor<FixedMapStarRewardConfig> _fixedMapStarRewardConfigAccessor;

    private GameConfigByIdAccessor<UnrivaledGodConfig> _unrivaledGodConfigByIdAccessor;
    private readonly Dictionary<int, Dictionary<string, UnrivaledGodTaskConfig>> _unrivaledGodTaskConfigByActivityId = new();

    private readonly Dictionary<int, Dictionary<int, UnrivaledGodExchangeConfig>>
        _unrivaledGodExchangeConfigByActivityId = new();
    private readonly Dictionary<int, List<CoopBossRewardConfig>> _coopBossRewardConfigByActivityId = new();
    private GameConfigAccessor<EndlessDifficultyConfig> _endlessDifficultyConfigAccessor;
    private GameConfigAccessor<WeeklyMailConfig> _weeklyMailConfigAccessor;

    private readonly Dictionary<int, List<RpgGameLevelConfig>> _rpgGameLevelConfigByActivityId = new();
    private readonly Dictionary<int, List<LoogGameLevelConfig>> _loogGameLevelConfigByActivityId = new();
    
    #endregion

    private int _maxLevel = 0;
    private List<float> _cooperateContributeFactorList = new List<float>();
    private List<int> _reRollMapCostList = new();
    private readonly HashSet<int> _basicCardHiddenInTreasureBox = new();
    private readonly List<ItemConfig> _defaultItems = new();

    // 启动时加载配置，需要注意在这里要清空掉所有的配置容器，避免更新配置时发生不可预料的错误
    public void Bootstrap()
    {
        try
        {

            foreach (var config in ConfigFile)
            {
                _logger.LogInformation($"加载配置: {config.config_key}");
                _configTextByKey.TryAdd(config.config_key, "");
                _configTextByKey[config.config_key] = config.content;
            }

            // 段位配置
            _divisionConfigList.Clear();
            var divisionConfigList = GetConfigList<DivisionConfig>("division_config");
            foreach (var config in divisionConfigList)
            {
                _divisionConfigList.Add(config);
            }

            _totalDivisionCount = divisionConfigList.Count;

            // 排行配置
            _rewardsDictionary.Clear();
            for (var i = 0; i < divisionConfigList.Count; ++i)
                _rewardsDictionary.Add(i, new List<int>());
            var rankConfigList = GetConfigList<DivisionRankLine>("division_rank");
            foreach (var line in rankConfigList)
            {
                _rewardsDictionary[0].Add(line.level_0);
                _rewardsDictionary[1].Add(line.level_1);
                _rewardsDictionary[2].Add(line.level_2);
                _rewardsDictionary[3].Add(line.level_3);
                _rewardsDictionary[4].Add(line.level_4);
                _rewardsDictionary[5].Add(line.level_5);
                _rewardsDictionary[6].Add(line.level_6);
                _rewardsDictionary[7].Add(line.level_7);
                _rewardsDictionary[8].Add(line.level_8);
                _rewardsDictionary[9].Add(line.level_9);
            }

            // 游戏内购买配置
            _commodityConfigByIdAccessor = new("commodity_config", config => config.id, this);
            foreach (var config in _commodityConfigByIdAccessor.ConfigList)
            {
                config.accu_weight_list = new List<int>();
                var sumWeight = 0;
                foreach (var weight in config.weight_list)
                {
                    sumWeight += weight;
                    config.accu_weight_list.Add(sumWeight);
                }
            }

            _packageItemConfigByIdAccessor = new("package_config", config => config.id, this);
            _skuItemConfigByKeyAccessor = new("iap_config", config => config.prop_id, this);

            // 游戏物品配置
            _fightCardItemList.Clear();
            _buildingCardItemList.Clear();
            _itemConfigByIdAccessor = new("item_config", config => config.id, this);
            _magicCardConfigByQuality = new(4);
            _basicCardHiddenInTreasureBox.Clear();
            _decorativeItemListByQuality.Clear();
            _defaultItems.Clear();

            foreach (var config in _itemConfigByIdAccessor.ConfigList)
            {
                if (config.type is ItemType.SoldierCard or ItemType.TowerCard)
                    _fightCardItemList.Add(config);
                else if (config.type is ItemType.BuildingCard)
                    _buildingCardItemList.Add(config);
                else if (config.type is ItemType.MagicCard)
                    _magicCardConfigByQuality[config.quality] = config;
                else if (config.type is ItemType.Avatar or ItemType.AvatarFrame or ItemType.NameCard)
                {
                    if (!_decorativeItemListByQuality.ContainsKey(config.quality))
                        _decorativeItemListByQuality.Add(config.quality, new List<int>());
                    _decorativeItemListByQuality[config.quality].Add(config.id);
                }

                if (config.type is ItemType.SoldierCard or ItemType.TowerCard or ItemType.BuildingCard)
                    _cardIdByDetailKeyDict[config.detailed_key] = config.id;
                if (IsBasicBuildingCard(config))
                    _basicCardHiddenInTreasureBox.Add(config.id);
                if (config.default_item && config.type != ItemType.TreasureBox)
                    _defaultItems.Add(config);
            }

            // 用户等级配置
            _userLevelConfigAccessor = new("user_level_config", config => config.level, this);
            _maxLevel = 0;
            foreach (var config in _userLevelConfigAccessor.ConfigList)
            {
                if (_maxLevel < config.level)
                    _maxLevel = config.level;
            }

            // 士兵卡牌配置
            _fightCardConfigDic.Clear();
            var fightCardConfigList = GetConfigList<FightCardConfig>("fight_card_config");
            foreach (var config in fightCardConfigList)
            {
                if (!_fightCardConfigDic.ContainsKey(config.key))
                    _fightCardConfigDic.Add(config.key, new List<FightCardConfig>());
                _fightCardConfigDic[config.key].Add(config);
            }

            // 建筑卡牌配置
            _buildingCardConfigByKeyAccessor = new("building_card_config", config => config.key, this);
            // 旧版战斗奖励配置 1.2.0版本上线后删除
            _scoreRewardAccessor = new("score_rewards", this);
            // 新版战斗奖励配置
            _scoreRewardV1Accessor = new("score_rewards_v1", this);
            // 无尽模式奖励配置
            _endlessScoreRewardAccessor = new("endless_rewards", this);

            // 胜利奖励配置
            _winRewardConfigByIdAccessor =
                new GameConfigByIdAccessor<WinRewardConfig>("win_rewards", config => config.difficulty, this);

            // 设备性能分数配置
            _socScoreDictionary.Clear();
            var socScoreConfigList = GetConfigList<SocScoreConfig>("soc_score");
            foreach (var config in socScoreConfigList)
            {
                var soc = config.soc.ToLower().TrimEnd();
                _socScoreDictionary.Add(soc, config.score);
            }

            // ios的也放一起
            var deviceScoreConfigList = GetConfigList<DeviceScoreConfig>("ios_device_score");
            foreach (var config in deviceScoreConfigList)
            {
                var soc = config.device.ToLower().TrimEnd();
                _socScoreDictionary.Add(soc, config.score);
            }

            // 宝箱配置
            _treasureBoxConfigByIdAccessor = new("treasure_box_config", config => config.id, this);
            // 计时宝箱序列
            _delayBoxDropConfigAccessor = new("delay_box_drop_config", this);
            _delayBoxNonLoopCount = _delayBoxDropConfigAccessor.ConfigList.Count(config => !config.loop);
            // Param
            _paramDictionary.Clear();
            // 清理一下缓存
            var paramConfigList = GetConfigList<ParamContent>("param_config");
            foreach (var config in paramConfigList)
                _paramDictionary.Add(config.key, config.content);

            // 领袖配置
            _heroConfigAccessor = new("hero_config", this);
            _heroConfigByKey.Clear();
            foreach (var heroConfig in _heroConfigAccessor.ConfigList)
                _heroConfigByKey.Add(heroConfig.key, heroConfig);
            // 每日购买刷新配置
            _dailyCommodityConfigAccessor = new("daily_commodity_config", this);
            _presetDailyStoreConfigAccessor = new("daily_commodity_path", this);
            // 地图相关
            _mapConfigAccessor = new GameConfigByIdAccessor<MapConfig>("map_config", config => config.id, this);
            // 签到相关
            _attendanceRewardConfigAccessor =
                new GameConfigAccessor<AttendanceRewardConfig>("attendance_rewards", this);
            // 排名奖励
            _divisionRewardConfigAccessor = new GameConfigAccessor<DivisionRewardConfig>("division_rewards", this);
            var buildingScoreConfigAccessor = new GameConfigAccessor<BuildingScoreConfig>("building_score_config", this);
            List<int> seasonAppearedInBuildingScoreConfigAccessor = new();
            foreach (var config in buildingScoreConfigAccessor.ConfigList)
                if (!seasonAppearedInBuildingScoreConfigAccessor.Contains(config.min_season))
                    seasonAppearedInBuildingScoreConfigAccessor.Add(config.min_season);
            seasonAppearedInBuildingScoreConfigAccessor.Sort();
            _buildingScoreConfigList = new();
            foreach (var config in buildingScoreConfigAccessor.ConfigList)
            {
                var index = seasonAppearedInBuildingScoreConfigAccessor.IndexOf(config.min_season);
                while (_buildingScoreConfigList.Count <= index)
                    _buildingScoreConfigList.Add(new Dictionary<int, BuildingScoreConfig>());
                _buildingScoreConfigList[index].Add(config.id, config);
            }

            _rankRewardConfigByDivision = new Dictionary<int, List<RankRewardConfig>>();
            var rankRewardConfigAccessor = new GameConfigAccessor<RankRewardConfig>("rank_rewards", this);
            foreach (var config in rankRewardConfigAccessor.ConfigList)
            {
                _rankRewardConfigByDivision.TryAdd(config.division, new List<RankRewardConfig>());
                _rankRewardConfigByDivision[config.division].Add(config);
            }

            // 探索奖励
            _exploreBoxConfigAccessor = new GameConfigAccessor<ExploreBoxConfig>("explore_box_config", this);

            // 合作贡献度
            TryGetParameterString(Params.CooperatContributeFactor, out var value);
            _cooperateContributeFactorList = value.Split('|').Select(float.Parse).ToList();
            // 刷图消耗铜币
            TryGetParameterString(Params.GameMapRerollCost, out value);
            _reRollMapCostList = value.Split('|').Select(int.Parse).ToList();

            // 难度选项配置
            _difficultySelectionConfigAccessor =
                new GameConfigAccessor<DifficultySelectionConfig>("difficulty_selection_config", this);
            // 战令配置
            _battlePassConfigAccessor = new GameConfigAccessor<BattlePassConfig>("battle_pass_config", this);
            _battlePassConfigByPassId.Clear();
            foreach (var battlePassConfig in _battlePassConfigAccessor.ConfigList)
            {
                _battlePassConfigByPassId.TryAdd(battlePassConfig.pass_id, new List<BattlePassConfig>());
                _battlePassConfigByPassId[battlePassConfig.pass_id].Add(battlePassConfig);
            }

            _battlePassTimeConfigAccessor =
                new GameConfigAccessor<BattlePassTimeConfig>("battle_pass_time_config", this);

            // 战令经验配置
            var battleExpRulesConfigAccessor = new GameConfigAccessor<BattleExpRulesConfig>("battle_exp_rules", this);
            List<int> seasonAppearedInBattleExpRuleConfigAccessor = new();
            foreach (var config in battleExpRulesConfigAccessor.ConfigList)
                if (!seasonAppearedInBattleExpRuleConfigAccessor.Contains(config.min_season))
                    seasonAppearedInBattleExpRuleConfigAccessor.Add(config.min_season);
            seasonAppearedInBattleExpRuleConfigAccessor.Sort();
            _battleExpRulesConfigList = new();
            foreach (var config in battleExpRulesConfigAccessor.ConfigList)
            {
                var index = seasonAppearedInBattleExpRuleConfigAccessor.IndexOf(config.min_season);
                var rule = new BattleExpRules()
                {
                    key = config.key,
                    ratio = config.ratio,
                    max = config.max,
                    max_score = config.max_score,
                    min_season = config.min_season,
                    eval_lambda = ExpressionLambdaParser.EvaluateBattleScoreEquation(config.eval_equation),
                };
                while (_battleExpRulesConfigList.Count <= index)
                    _battleExpRulesConfigList.Add(new Dictionary<string, BattleExpRules>());
                _battleExpRulesConfigList[index].Add(config.key, rule);
            }
            
            // 成就配置
            _achievementConfigAccessor =
                new GameConfigByIdAccessor<AchievementConfig>("achievement_config", config => config.id, this);
            var configList = _achievementConfigAccessor.ConfigList;
            _achievementConfigDictionary.Clear();
            foreach (var config in configList)
            {
                _achievementConfigDictionary.TryAdd(config.key, new Dictionary<string, AchievementConfig>());
                _achievementConfigDictionary[config.key].TryAdd(config.target, config);
            }

            // 新手任务
            _beginnerTaskConfigAccessor = new GameConfigAccessor<BeginnerTaskConfig>("beginner_task_config", this);
            _beginnerTaskDayConfigAccessor =
                new GameConfigAccessor<BeginnerTaskDayConfig>("beginner_task_day_config", this);

            _difficultyConfigAccessor = new GameConfigAccessor<DifficultyConfig>("difficulty_config", this);
            _mallAdsConfigAccessor =
                new GameConfigByIdAccessor<MallAdConfig>("mall_ad_config", config => config.id, this);

            // 每日任务
            _dailyTaskConfigList.Clear();
            var dailyTaskConfigList = GetConfigList<DailyTaskConfig>("daily_task_config");
            foreach (var config in dailyTaskConfigList)
                _dailyTaskConfigList.Add(config);
            _dailyActiveScoreRewardConfigList.Clear();
            var activeScoreConfigList = GetConfigList<ActiveScoreLevelConfig>("daily_active_score_reward");
            foreach (var config in activeScoreConfigList)
                _dailyActiveScoreRewardConfigList.Add(config);

            // 出新卡的配置
            _newCardConfigByQualityAccessor =
                new GameConfigByIdAccessor<NewCardConfig>("new_card_config", config => config.quality, this);

            // 固定地图配置
            _fixedMapConfigByIdAccessor
                = new GameConfigByIdAccessor<FixedMapConfig>("fixed_map_config", config => config.id, this);
            _starTaskConfigByIdAccessor = new GameConfigByIdAccessor<StarTask>("star_tasks", config => config.id, this);

            // 建筑配置
            _buildingConfigByIdAccessor
                = new GameConfigByIdAccessor<BuildingConfig>("building_config", config => config.id, this);
            // 挂机奖励
            _idleRewardConfigAccessor
                = new GameConfigByIdAccessor<IdleRewardConfig>("idle_reward", config => config.id, this);
            // 装饰物池子
            _decorativeItemPoolAccessor
                = new GameConfigByIdAccessor<DecorativeItemPoolConfig>("decoration_prize_pool_config",
                    config => config.prize_pool_id, this);
            // 运营活动相关
            _activityTimeConfigByIdAccessor =
                new GameConfigByIdAccessor<ActivityTimeConfig>("activity_time_config", config => config.id, this);

            // 福星活动
            var activityLuckyStarConfigAccessor =
                new GameConfigAccessor<ActivityLuckyStarConfig>("activity_lucky_star", this);
            _activityLuckyStarConfigByActivityId.Clear();
            foreach (var config in activityLuckyStarConfigAccessor.ConfigList)
            {
                _activityLuckyStarConfigByActivityId.TryAdd(config.activityId, new List<ActivityLuckyStarConfig>());
                _activityLuckyStarConfigByActivityId[config.activityId].Add(config);
            }

            // 福袋活动
            var activityFortuneLevelConfigAccessor =
                new GameConfigAccessor<ActivityFortuneBagLevelConfig>("fortune_bag_level_config", this);
            _activityFortuneBagLevelConfigByActivityId.Clear();
            foreach (var config in activityFortuneLevelConfigAccessor.ConfigList)
            {
                _activityFortuneBagLevelConfigByActivityId.TryAdd(config.activityId,
                    new List<ActivityFortuneBagLevelConfig>());
                _activityFortuneBagLevelConfigByActivityId[config.activityId].Add(config);
            }

            var activityFortuneBagConfigAccessor =
                new GameConfigAccessor<ActivityFortuneBagConfig>("fortune_bag_config", this);
            _activityFortuneBagConfigByActivityId.Clear();
            foreach (var config in activityFortuneBagConfigAccessor.ConfigList)
                _activityFortuneBagConfigByActivityId[config.activityId] = config;

            // 宝藏迷宫活动
            var activityTreasureMazeDifficultyConfigAccessor =
                new GameConfigAccessor<TreasureMazeDifficultyConfig>("treasure_maze_difficulty_config", this);
            _treasureMazeDifficultyConfigByActivityId.Clear();
            foreach (var config in activityTreasureMazeDifficultyConfigAccessor.ConfigList)
            {
                _treasureMazeDifficultyConfigByActivityId.TryAdd(config.activity_id,
                    new Dictionary<int, TreasureMazeDifficultyConfig>());
                _treasureMazeDifficultyConfigByActivityId[config.activity_id][config.difficulty] = config;
            }

            var activityTreasureMazeLootConfigAccessor =
                new GameConfigAccessor<TreasureMazeLootConfig>("treasure_maze_loot_config", this);
            _treasureMazeLootConfigByActivityId.Clear();
            _treasureMazeLootRewardPoolByActivityId.Clear();
            foreach (var config in activityTreasureMazeLootConfigAccessor.ConfigList)
            {
                _treasureMazeLootConfigByActivityId.TryAdd(config.activity_id,
                    new Dictionary<int, TreasureMazeLootConfig>());
                _treasureMazeLootRewardPoolByActivityId.TryAdd(config.activity_id,
                    new Dictionary<int, List<RewardPoolItem>>());
                _treasureMazeLootConfigByActivityId[config.activity_id][config.loot_id] = config;
                _treasureMazeLootRewardPoolByActivityId[config.activity_id]
                    .TryAdd(config.loot_id, new List<RewardPoolItem>());
                for (int i = 0; i < config.reward_pool.Count; i++)
                {
                    _treasureMazeLootRewardPoolByActivityId[config.activity_id][config.loot_id].Add(new RewardPoolItem()
                    {
                        ItemId = config.reward_pool[i], Weight = config.reward_weight[i]
                    });
                }
            }

            // 存钱罐活动（貔貅瓮）
            _piggyBankConfigAccessor =
                new GameConfigAccessor<ActivityPiggyBankConfig>("activity_piggy_bank_config", this);
            _piggyBankAllRewardClaimedStatus = 0;
            for (int i = 0; i < _piggyBankConfigAccessor.ConfigList.Count; i++)
                _piggyBankAllRewardClaimedStatus |= (1 << i);

            // 战役星星奖励
            _fixedMapStarRewardConfigAccessor =
                new GameConfigAccessor<FixedMapStarRewardConfig>("fixed_map_star_reward", this);

            // 无双神将活动
            _unrivaledGodConfigByIdAccessor = new("unrivaled_god_config", config => config.activity_id, this);
            _unrivaledGodTaskConfigByActivityId.Clear();
            _unrivaledGodExchangeConfigByActivityId.Clear();
            var unrivaledGodTaskConfigList = GetConfigList<UnrivaledGodTaskConfig>("unrivaled_god_task_config")!;
            foreach (var config in unrivaledGodTaskConfigList)
            {
                _unrivaledGodTaskConfigByActivityId.TryAdd(config.activity_id, new());
                _unrivaledGodTaskConfigByActivityId[config.activity_id].Add(config.task_key, config);
            }

            var unrivaledGodExchangeConfigList
                = GetConfigList<UnrivaledGodExchangeConfig>("unrivaled_god_exchange_config")!;
            foreach (var config in unrivaledGodExchangeConfigList)
            {
                _unrivaledGodExchangeConfigByActivityId.TryAdd(config.activity_id, new());
                _unrivaledGodExchangeConfigByActivityId[config.activity_id].Add(config.id, config);
            }

            // 联机共斗boss活动
            var coopBossRewardConfigAccessor =
                new GameConfigAccessor<CoopBossRewardConfig>("co-op_boss_reward_config", this);
            _coopBossRewardConfigByActivityId.Clear();
            foreach (var config in coopBossRewardConfigAccessor.ConfigList)
            {
                _coopBossRewardConfigByActivityId.TryAdd(config.activity_id, new());
                _coopBossRewardConfigByActivityId[config.activity_id].Add(config);
            }
            
            // RPG玩法
            var rpgLevelConfigAccessor =
                new GameConfigAccessor<RpgGameLevelConfig>("rpg_game_level_config", this);
            _rpgGameLevelConfigByActivityId.Clear();
            foreach (var config in rpgLevelConfigAccessor.ConfigList)
            {
                _rpgGameLevelConfigByActivityId.TryAdd(config.activity_id, new());
                _rpgGameLevelConfigByActivityId[config.activity_id].Add(config);
            }

            // Loog玩法
            var loogLevelConfigAccessor =
                new GameConfigAccessor<LoogGameLevelConfig>("loog_game_level_config", this);
            _loogGameLevelConfigByActivityId.Clear();
            foreach (var config in loogLevelConfigAccessor.ConfigList)
            {
                _loogGameLevelConfigByActivityId.TryAdd(config.activity_id, new());
                _loogGameLevelConfigByActivityId[config.activity_id].Add(config);
            }

            // 轮回挑战
            _endlessDifficultyConfigAccessor = new("endless_difficulty_config", this);

            // 老虎机活动
            _slotMachineConfigByActivityId = new();
            _slotMachineDrawConfigByActivityId = new();
            _slotMachineDrawRewardPoolByActivityId = new();
            _slotMachinePointRewardConfigByActivityId = new();
            _slotMachineDoubleUpItemLevelDict = new();
            var slotMachineConfigAccessor = new GameConfigAccessor<ActivitySlotMachineConfig>(
                "slot_machine_config", this);
            foreach (var config in slotMachineConfigAccessor.ConfigList)
                _slotMachineConfigByActivityId.TryAdd(config.activity_id, config);
            var slotMachineDrawConfigAccessor = new GameConfigAccessor<ActivitySlotMachineDrawConfig>(
                "slot_machine_draw_config", this);
            foreach (var config in slotMachineDrawConfigAccessor.ConfigList)
            {
                _slotMachineDrawConfigByActivityId.TryAdd(config.activity_id, new());
                _slotMachineDrawConfigByActivityId[config.activity_id].Add(config);
                var doubleUpItemSku = GetSkuItemConfig(config.double_up_iap_id);
                if (doubleUpItemSku == null) continue; // 配置错误
                _slotMachineDoubleUpItemLevelDict[doubleUpItemSku.item_id] = config.draw_id;
            }
            var slotMachineDrawRewardConfigAccessor = new GameConfigAccessor<ActivitySlotMachineDrawRewardConfig>(
                "slot_machine_draw_rewards", this);
            foreach (var config in slotMachineDrawRewardConfigAccessor.ConfigList)
            {
                _slotMachineDrawRewardPoolByActivityId.TryAdd(config.activity_id, new());
                _slotMachineDrawRewardPoolByActivityId[config.activity_id].Add(config);
            }

            var slotMachinePointRewardConfigAccessor = new GameConfigAccessor<ActivitySlotMachinePointRewardConfig>(
                "slot_machine_point_rewards", this);
            foreach (var config in slotMachinePointRewardConfigAccessor.ConfigList)
            {
                _slotMachinePointRewardConfigByActivityId.TryAdd(config.activity_id, new());
                _slotMachinePointRewardConfigByActivityId[config.activity_id].Add(config);
            }

            // 灵犀探宝活动
            _treasureHuntConfigByIdAccessor
                = new GameConfigByIdAccessor<TreasureHuntConfig>("treasure_hunt_config", config => config.activity_id,
                    this);
            _treasureHuntDrawConfigByActivityId = new();
            _treasureHuntPointRewardConfigByActivityId = new();
            var treasureHuntDrawConfigAccessor = new GameConfigAccessor<TreasureHuntDrawConfig>(
                "treasure_hunt_draw_config", this);
            foreach (var config in treasureHuntDrawConfigAccessor.ConfigList)
            {
                _treasureHuntDrawConfigByActivityId.TryAdd(config.activity_id, new());
                _treasureHuntDrawConfigByActivityId[config.activity_id].Add(config);
            }
            var treasureHuntPointRewardConfigAccessor = new GameConfigAccessor<TreasureHuntPointRewardConfig>(
                "treasure_hunt_point_rewards", this);
            foreach (var config in treasureHuntPointRewardConfigAccessor.ConfigList)
            {
                _treasureHuntPointRewardConfigByActivityId.TryAdd(config.activity_id, new());
                _treasureHuntPointRewardConfigByActivityId[config.activity_id].Add(config);
            }
            
            // CsgoLottery related configs
            var csgoLotteryConfigAccessor = new GameConfigAccessor<CsgoLotteryConfig>("csgo_lottery_config", this);
            _csgoLotteryConfigByActivityId = new();
            foreach (var config in csgoLotteryConfigAccessor.ConfigList)
            {
                _csgoLotteryConfigByActivityId.TryAdd(config.activity_id, new List<CsgoLotteryConfig>());
                var list = _csgoLotteryConfigByActivityId[config.activity_id];
                // ensure list is large enough so index == reward_id
                while (list.Count <= config.reward_id)
                    list.Add(new CsgoLotteryConfig());
                list[config.reward_id] = config;
            }
            
            var csgoLotteryPointConfigAccessor = new GameConfigAccessor<CsgoLotteryPointConfig>("csgo_lottery_point_config", this);
            _csgoLotteryPointConfigByActivityId = new();
            foreach (var config in csgoLotteryPointConfigAccessor.ConfigList)
            {
                _csgoLotteryPointConfigByActivityId.TryAdd(config.activity_id, new List<CsgoLotteryPointConfig>());
                var list = _csgoLotteryPointConfigByActivityId[config.activity_id];
                while (list.Count <= config.reward_id)
                    list.Add(new CsgoLotteryPointConfig());
                list[config.reward_id] = config;
            }
            
            var csgoLotteryPassConfigAccessor = new GameConfigAccessor<CsgoLotteryPassConfig>("csgo_lottery_pass_config", this);
            _csgoLotteryPassConfigByActivityId = new();
            foreach (var config in csgoLotteryPassConfigAccessor.ConfigList)
            {
                _csgoLotteryPassConfigByActivityId.TryAdd(config.activity_id, new List<CsgoLotteryPassConfig>());
                var list = _csgoLotteryPassConfigByActivityId[config.activity_id];
                // index by level field
                while (list.Count <= config.level)
                    list.Add(new CsgoLotteryPassConfig());
                list[config.level] = config;
            }
            
            var csgoLotteryTaskConfigAccessor = new GameConfigAccessor<CsgoLotteryTaskConfig>("csgo_lottery_task_config", this);
            _csgoLotteryTaskConfigByActivityId = new();
            foreach (var config in csgoLotteryTaskConfigAccessor.ConfigList)
            {
                _csgoLotteryTaskConfigByActivityId.TryAdd(config.activity_id, new());
                _csgoLotteryTaskConfigByActivityId[config.activity_id].Add(config);
            }
            
            // 一击必杀活动
            _oneShotKillMapByActivityId = new();
            _oneShotKillUltimateRewardConfigByActivityId = new();
            _oneShotKillLeaderByActivityId = new();
            _oneShotKillTaskByActivityId = new();
            var oneShotKillMapConfigAccessor = new GameConfigAccessor<ActivityOneShotKillMapConfig>(
                "one_shot_kill_map_config", this);
            var oneShotKillLeaderConfigAccessor = new GameConfigAccessor<ActivityOneShotKillLeaderConfig>(
                "one_shot_kill_leader_config", this);
            var oneShotKillTaskConfigAccessor = new GameConfigAccessor<ActivityOneShotKillTaskConfig>(
                "one_shot_kill_task_config", this);
            foreach (var config in oneShotKillMapConfigAccessor.ConfigList)
            {
                if (config.level == -1)
                {
                    GeneralReward ultimateReward = new GeneralReward();
                    for (int i = 0; i < config.conquer_reward_list.Count; i++)
                        ultimateReward.AddReward(config.conquer_reward_list[i], config.conquer_reward_count[i]);
                    _oneShotKillUltimateRewardConfigByActivityId.Add(config.activity_id, ultimateReward);
                    continue;
                }
                _oneShotKillMapByActivityId.TryAdd(config.activity_id, new());
                while (_oneShotKillMapByActivityId[config.activity_id].Count <= config.level)
                    _oneShotKillMapByActivityId[config.activity_id].Add(new());
                _oneShotKillMapByActivityId[config.activity_id][config.level] = config;
            }

            foreach (var config in oneShotKillLeaderConfigAccessor.ConfigList)
            {
                _oneShotKillLeaderByActivityId.TryAdd(config.activity_id, new());
                _oneShotKillLeaderByActivityId[config.activity_id].Add(config);
            }

            foreach (var config in oneShotKillTaskConfigAccessor.ConfigList)
            {
                _oneShotKillTaskByActivityId.TryAdd(config.activity_id, new());
                _oneShotKillTaskByActivityId[config.activity_id].Add(config);
            }
            
            _resourceConfigByKeyAccessor
                = new GameConfigByKeyAccessor<ResourceConfig>("resource_config", config => config.key, this);
            _banPlayerTimeConfigAccessor = new GameConfigAccessor<BanPlayerTimeConfig>("ban_player_time_config", this);
            _enemyConfigAccessor = new GameConfigAccessor<EnemyConfig>("enemy_config", this);
            _fightBuffConfigAccessor = new GameConfigAccessor<FightBuffConfig>("fight_buff_config", this);
            _weeklyMailConfigAccessor = new GameConfigAccessor<WeeklyMailConfig>("weekly_mail_config", this);
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public List<T>? GetConfigList<T>(string configName)
    {
        var jsonText = _configTextByKey[configName];
        var configs = jsonText.FromJson<ConfigJsonType<T>>();
        return configs?.content;
    }

    public int GetDivisionByDivisionScore(int divisionScore)
    {
        var division = 0;
        for (; division < _divisionConfigList.Count; ++division)
        {
            if (divisionScore < _divisionConfigList[division].count)
                break;
        }
        // division应该从0开始，因为第一级的要求分数是0，因此上面的for循环至少会执行两次，因此这里需要-1
        division -= 1;
        return division;
    }

    public int GetRewardsByRank(int divisionScore, int rank/* rank 0 -> 第一名 */, int userCount)
    {
        if (divisionScore < 0)
            return 0;
        var division = GetDivisionByDivisionScore(divisionScore);
        int scoreIdx = (int)(_rewardsDictionary[division].Count * 0.5f + userCount * 0.5f) - rank - 1;
        scoreIdx = Math.Clamp(scoreIdx, 0, _rewardsDictionary[division].Count - 1);
        return _rewardsDictionary[division][scoreIdx];
    }

    public int GetRankPopulationCap(int division)
    {
        return _divisionConfigList[division].population;
    }
    public DivisionConfig GetDivisionConfig(int division)
    {
        return _divisionConfigList[division];
    }

    public int TotalDivisionCount => _totalDivisionCount;
    
    // TODO 1.2.0版本上线后删除
    public List<ScoreRewardConfig> GetScoreRewardList()
    {
        return _scoreRewardAccessor.ConfigList;
    }

    public List<ScoreRewardConfig> GetScoreRewardV1List()
    {
        return _scoreRewardV1Accessor.ConfigList;
    }

    public ScoreRewardConfig? GetScoreRewardConfigByTime(int time, string version)
    {
        bool useV1 = version.CompareVersionStrServer("1.2.0") >= 0;
        var scoreRewardList = useV1 ? GetScoreRewardV1List() : GetScoreRewardList();
        var rewardIndex = -1;
        var found = false;
        var count = scoreRewardList.Count;
        for (var i = 0; i < count; ++i)
        {
            var currentRewardInfo = scoreRewardList[i];
            if (currentRewardInfo.time > time)
            {
                found = true;
                break;
            }

            rewardIndex = i;
        }

        if (!found)
            rewardIndex = count - 1;

        if (rewardIndex < 0)
            return null;
        return scoreRewardList[rewardIndex];
    }

    public EndlessRewardConfig? GetEndlessRewardConfigByScore(long score)
    {
        var scoreRewardList = GetEndlessScoreRewardList();
        var rewardIndex = -1;
        var found = false;
        var count = scoreRewardList.Count;
        for (var i = 0; i < count; ++i)
        {
            var currentRewardInfo = scoreRewardList[i];
            if (currentRewardInfo.score > score)
            {
                found = true;
                break;
            }

            rewardIndex = i;
        }

        if (!found)
            rewardIndex = count - 1;

        if (rewardIndex < 0)
            return null;
        return scoreRewardList[rewardIndex];
    }

    public WinRewardConfig? GetWinRewardConfigByDiff(int difficulty)
    {
        return _winRewardConfigByIdAccessor[difficulty];
    }

    public int GetCardIdByDetailKey(string key)
    {
        return _cardIdByDetailKeyDict.TryGetValue(key, out var id) ? id : -1;
    }

    public List<FightCardConfig>? GetFightCardConfigList(string key)
    {
        return _fightCardConfigDic.TryGetValue(key, out var value) ? value : null;
    }

    public FightCardConfig? GetFightCardConfig(UserCard card)
    {
        var itemConfig = GetItemConfigById(card.CardId);
        if (itemConfig == null)
            return null;
        var list = GetFightCardConfigList(itemConfig.detailed_key);
        if (list == null)
            return null;
        if (card.CardLevel < 0 || card.CardLevel >= list.Count)
            return null;

        return list[card.CardLevel];
    }

    public List<CommodityConfig> GetCommodityConfigList()
    {
        return _commodityConfigByIdAccessor.ConfigList;
    }

    public CommodityConfig? GetCommodityConfigById(int commodityId)
    {
        return _commodityConfigByIdAccessor[commodityId];
    }

    public ItemConfig? GetItemConfigById(int itemId)
    {
        return _itemConfigByIdAccessor[itemId];
    }

    public ItemPackageConfig? GetPackageConfigById(int packageId)
    {
        return _packageItemConfigByIdAccessor[packageId];
    }

    public ItemConfig? GetMagicCardConfigByQuality(ItemQuality quality)
    {
        return _magicCardConfigByQuality.TryGetValue(quality, out var value) ? value : null;
    }

    public List<ItemConfig> GetItemConfigList()
    {
        return _itemConfigByIdAccessor.ConfigList;
    }

    public UserLevelConfig? GetUserLevelConfig(int level)
    {
        return _userLevelConfigAccessor[level];
    }

    public bool IsLevelMaxLevel(int level, string gameVersion)
    {
        if (gameVersion.CompareVersionStrServer("1.1.0") < 0)
            return level >= 64;
        return level >= _maxLevel;
    }

    public TreasureBoxConfig? GetTreasureBoxConfigById(int itemId)
    {
        return _treasureBoxConfigByIdAccessor[itemId];
    }

    public List<ItemConfig> GetFightCardItemConfigList()
    {
        return _fightCardItemList;
    }

    public List<ItemConfig> GetBuildingCardItemConfigList()
    {
        return _buildingCardItemList;
    }

    public HashSet<int> GetBasicCardsThatCannotBeObtainedByTreasureBox()
    {
        return _basicCardHiddenInTreasureBox;
    }

    public List<ItemConfig> GetDefaultItems() => _defaultItems;

    public BuildingCardConfig? GetCurrentBuildingCardConfig(UserCard card)
    {
        var itemConfig = GetItemConfigById(card.CardId);
        if (itemConfig == null)
            return null;
        return _buildingCardConfigByKeyAccessor[itemConfig.detailed_key];
    }

    public BuildingCardConfig? GetBuildingCardConfigByKey(string key)
    {
        return _buildingCardConfigByKeyAccessor[key];
    }
    
    public static bool IsBasicBuildingCard(ItemConfig cardConfig)
    {
        return cardConfig.type == ItemType.BuildingCard && cardConfig.id / 10000 == 2;
    }

    public bool TryGetParameterString(string key, out string value)
    {
        if (_paramDictionary.TryGetValue(key, out value))
            return true;
        return false;
    }

    public bool TryGetParameterInt(string key, out int value)
    {
        if (TryGetParameterString(key, out string content))
        {
            if (int.TryParse(content, out value))
                return true;
        }

        value = 0;
        return false;
    }

    public List<float>? GetParameterFloatList(string key)
    {
        if (!TryGetParameterString(key, out string content))
            return null;
        var parts = content.Split('|');
        var result = new List<float>();
        foreach (var partStr in parts)
        {
            if (!float.TryParse(partStr, out float value))
                return null;
            result.Add(value);
        }

        return result;
    }

    public List<int>? GetParameterIntList(string key)
    {
        if (!TryGetParameterString(key, out string content))
            return null;
        var parts = content.Split('|');
        var result = new List<int>();
        foreach (var partStr in parts)
        {
            if (!int.TryParse(partStr, out int value))
                return null;
            result.Add(value);
        }

        return result;
    }

    public float GetGameMultiByDifficulty(int difficulty, int difficultyLevel)
    {
        var difficultyConfig = GetDifficultyConfig(difficulty);
        if (difficultyConfig == null)
            return -1;
        int maxLevel = difficultyConfig.score_mult.Count - 1;
        if (difficultyLevel <= maxLevel || !difficultyConfig.endless)
            return difficultyConfig.score_mult[Math.Min(difficultyLevel, maxLevel)];

        TryGetParameterFloat(Params.GameScoreMultParam, out float multiplier);
        return difficultyConfig.score_mult[^1] + multiplier * (difficultyLevel - maxLevel);
    }

    public List<DifficultySelectionConfig> GetDifficultySelectionConfigListByDifficulty(int difficulty)
    {
        var configs = _difficultySelectionConfigAccessor.ConfigList;
        return configs.Where(config => config.lock_difficulty <= difficulty).ToList();
    }

    public bool TryGetParameterFloat(string key, out float value)
    {
        if (TryGetParameterString(key, out string content))
        {
            if (float.TryParse(content, out value))
                return true;
        }

        value = 0f;
        return false;
    }

    private int CalculateIndexBy(int sequence, int nonLoopCount, int maxCount)
    {
        if (sequence < _delayBoxNonLoopCount)
            return sequence;
        return (sequence - _delayBoxNonLoopCount) % (maxCount - _delayBoxNonLoopCount) + _delayBoxNonLoopCount;
    }

    public DelayBoxDropConfig GetDelayBoxDropConfigBySequence(int sequence)
    {
        var realIndex = CalculateIndexBy(sequence, _delayBoxNonLoopCount, _delayBoxDropConfigAccessor.ConfigList.Count);
        return _delayBoxDropConfigAccessor[realIndex]!;
    }

    public EndlessDifficultyConfig? GetEndlessDifficultyConfig(int difficulty)
    {
        return _endlessDifficultyConfigAccessor[difficulty];
    }


    // 轮回挑战最大难度
    public int GetEndlessChallengeMaxDifficulty()
    {
        return _endlessDifficultyConfigAccessor.ConfigList.Count;
    }
    
    public List<EndlessRewardConfig> GetEndlessScoreRewardList()
    {
        return _endlessScoreRewardAccessor.ConfigList;
    }

    public BuildingCardConfig? GetBuildingCardConfig(UserCard card)
    {
        return GetCurrentBuildingCardConfig(card);
    }

    public int GetDeviceScoreByName(string name)
    {
        return _socScoreDictionary.GetValueOrDefault(name, -1);
    }

    public HeroConfig? GetHeroConfigById(int heroId)
    {
        return _heroConfigAccessor[heroId];
    }

    public HeroConfig? GetHeroConfigByKey(string key)
    {
        return _heroConfigByKey.TryGetValue(key, out var config) ? config : null;
    }

    public List<HeroConfig> GetHeroConfigList() => _heroConfigAccessor.ConfigList;

    public List<UserDailyStoreItem> GenerateDailyStoreItems(
        short shardId,
        long playerId,
        long currentTimeStamp,
        ref UserDailyStoreIndex userDailyStoreIndex,
        in List<UserCard> userCards)
    {
        var pathConfigList = GetDailyStorePresetConfigList();
        var needCount = _dailyCommodityConfigAccessor.ConfigList.Count;
        var presetCount = pathConfigList.Count / needCount * needCount;
        if (userDailyStoreIndex.Index <= presetCount - needCount)
        {
            // 走预设的path
            var result = new List<UserDailyStoreItem>();
            for (var i = userDailyStoreIndex.Index; i < userDailyStoreIndex.Index + needCount; ++i)
            {
                var config = pathConfigList[i];
                result.Add(new UserDailyStoreItem()
                {
                    ShardId = shardId,
                    PlayerId = playerId,
                    ItemId = config.item_id,
                    ItemCount = config.item_count,
                    Price = config.price,
                    PriceType = config.price_type,
                    Bought = false,
                    TimeStamp = currentTimeStamp
                });
            }

            userDailyStoreIndex.Index += needCount;
            return result;
        }
        else
        {
            // 随机给, 但只给玩家有的
            var cardPoolByQuality = new Dictionary<int, List<int>>();
            foreach (var card in userCards)
            {
                var itemConfig = GetItemConfigById(card.CardId);
                if (itemConfig == null)
                    continue;
                var qualityIndex = (int)itemConfig.quality;
                if (!cardPoolByQuality.ContainsKey(qualityIndex))
                    cardPoolByQuality.Add(qualityIndex, new List<int>());
                cardPoolByQuality[qualityIndex].Add(itemConfig.id);
            }

            var random = new Random();
            var generatedSet = new HashSet<int>();
            var result = new List<UserDailyStoreItem>();
            foreach (var config in _dailyCommodityConfigAccessor.ConfigList)
            {
                var qualityIndex = config.quality;
                // 尝试获取卡牌列表
                List<int> cardList = null;
                var realQualityIndex = qualityIndex;
                for (; realQualityIndex >= 0; --realQualityIndex)
                {
                    if (cardPoolByQuality.TryGetValue(realQualityIndex, out cardList))
                        break;
                }

                qualityIndex = realQualityIndex;
                // 除非玩家一张卡都没有，否则不会触发这个
                if (cardList == null)
                {
                    for (var i = 0; i < needCount; ++i)
                    {
                        var pathConfig = pathConfigList[i];
                        result.Add(new UserDailyStoreItem()
                        {
                            ItemId = pathConfig.item_id,
                            ItemCount = pathConfig.item_count,
                            Price = pathConfig.price,
                            PriceType = pathConfig.price_type,
                            ShardId = shardId,
                            PlayerId = playerId,
                            Bought = false,
                            TimeStamp = currentTimeStamp
                        });
                    }

                    return result;
                }

                //  检查cardList是否都在generatedSet里了, 如果是的话降级
                var allContains = true;
                for (var i = 0; i < cardList.Count; i++)
                {
                    var key = qualityIndex * 10000 + i;
                    if (generatedSet.Contains(key))
                        continue;
                    allContains = false;
                    break;
                }

                if (allContains)
                    cardList = cardPoolByQuality[--qualityIndex];

                var index = random.Next(0, cardList.Count);
                for (var i = 0; i < 1000 && generatedSet.Contains(qualityIndex * 10000 + index); ++i)
                    index = random.Next(0, cardList.Count);
                generatedSet.Add(qualityIndex * 10000 + index);
                var itemCount = random.Next(config.random_range[0], config.random_range[1]);
                var price = config.buying_type == DailyCommodityConfig.BuyingType.FREE
                    ? 0
                    : itemCount * config.single_price;
                result.Add(new UserDailyStoreItem()
                {
                    ItemId = cardList[index],
                    ItemCount = itemCount,
                    Price = price,
                    PriceType = (int)config.buying_type,
                    ShardId = shardId,
                    PlayerId = playerId,
                    Bought = false,
                    TimeStamp = currentTimeStamp
                });
            }

            return result;
        }
    }

    public MapConfig? GetMapConfig(int mapId) => _mapConfigAccessor[mapId];

    public AttendanceRewardConfig? GetAttendanceRewardConfig(int id) => _attendanceRewardConfigAccessor[id];

    public int AttendanceRewardCount => _attendanceRewardConfigAccessor.ConfigList.Count;

    public DivisionRewardConfig? GetDivisionRewardConfig(int division) => _divisionRewardConfigAccessor[division];

    public RankRewardConfig? GetRankRewardConfig(int division, int rank)
    {
        if (rank < 0)
            return null;
        var configList = _rankRewardConfigByDivision[division];
        for (int i = 0; i < configList.Count; i++)
        {
            var config = configList[i];
            if (rank < config.rank)
                return config;
        }

        return null;
    }

    public BuildingScoreConfig? GetBuildingScoreConfig(int id, int currentSeason)
    {
        for ( int i = _buildingScoreConfigList.Count - 1; i >= 0; i-- )
        {
            var configDict = _buildingScoreConfigList[i];
            if (configDict.TryGetValue(id, out var config) &&
                config.min_season <= currentSeason)
                return config;
        }
        return null;
    }
    
    public ExploreBoxConfig? GetExploreBoxConfigByCount(int count)
    {
        return _exploreBoxConfigAccessor[count];
    }

    public int GetMaxExploreStarCount()
    {
        return _exploreBoxConfigAccessor.ConfigList.Count - 1;
    }

    public float GetContributeFactorByRank(int rank)
    {
        if (rank < 0 || rank >= _cooperateContributeFactorList.Count)
            return 1;
        return _cooperateContributeFactorList[rank];
    }

    public BattlePassConfig? GetBattlePassConfigByLevel(int passId, int level)
    {
        if (!_battlePassConfigByPassId.TryGetValue(passId, out var battlePassConfigList))
            return null;
        if (level < 0 || level > battlePassConfigList.Count - 1)
            return null;
        return battlePassConfigList[level];
    }

    public int GetActiveBattlePassId(DateTime? now = null)
    {
        now ??= DateTime.UtcNow;
        foreach (var timeConfig in _battlePassTimeConfigAccessor.ConfigList)
        {
            var startTime = TimeUtils.ParseDateTimeStr(timeConfig.start_time);
            var endTime = TimeUtils.ParseDateTimeStr(timeConfig.end_time);
            if (now >= startTime && now < endTime)
                return timeConfig.pass_id;
        }

        return -1;
    }

    public BattlePassTimeConfig? GetBattlePassTimeConfigByPassId(int passId)
    {
        return _battlePassTimeConfigAccessor[passId];
    }

    public List<BattlePassConfig>? GetBattlePassConfigListByPassId(int passId)
    {
        if (!_battlePassConfigByPassId.TryGetValue(passId, out var battlePassConfigList))
            return null;
        return _battlePassConfigByPassId[passId];
    }

    public BattlePassConfig? GetLastBattlePassConfig(int passId)
    {
        if (!_battlePassConfigByPassId.TryGetValue(passId, out var battlePassConfigList))
            return null;
        return battlePassConfigList.Count <= 0 ? null : battlePassConfigList[^1];
    }

    public BattleExpRules? GetBattleRulesConfigByKey(string key, int currentSeason)
    {
        for ( int i = _battleExpRulesConfigList.Count - 1; i >= 0; i-- )
        {
            var dict = _battleExpRulesConfigList[i];
            if (dict.TryGetValue(key, out var rule) && rule.min_season <= currentSeason)
                return rule;
        }
        return null;
    }

    public AchievementConfig? GetAchievementConfigById(int passId)
    {
        return _achievementConfigAccessor[passId];
    }

    public AchievementConfig? GetAchievementConfigByKey(string key, string target)
    {
        if (!_achievementConfigDictionary.TryGetValue(key, out var dictionary))
            return null;
        if (dictionary.TryGetValue(target, out var config))
            return config;
        return dictionary.TryGetValue("all", out config) ? config : null;
    }

    public List<int>? GetDecorativeItemConfigListByQuality(ItemQuality quality)
    {
        if (!_decorativeItemListByQuality.TryGetValue(quality, out var itemList))
            return null;
        return itemList.ToList();
    }

    public List<BeginnerTaskConfig> GetBeginnerTaskList() => _beginnerTaskConfigAccessor.ConfigList;

    public BeginnerTaskConfig? GetBeginnerTaskConfig(int id) => _beginnerTaskConfigAccessor[id];

    public BeginnerTaskDayConfig? GetBeginnerTaskDayConfig(int day) => _beginnerTaskDayConfigAccessor[day];

    public int GetBeginnerTaskMaxDayCount() => _beginnerTaskDayConfigAccessor.ConfigList.Count;

    public List<DailyTaskConfig> GetDailyTaskList() => _dailyTaskConfigList;
    public List<ActiveScoreLevelConfig> GetActiveScoreRewardList() => _dailyActiveScoreRewardConfigList;

    public List<PresetDailyStoreItemConfig> GetDailyStorePresetConfigList()
    {
        return _presetDailyStoreConfigAccessor.ConfigList;
    }

    public DifficultyConfig? GetDifficultyConfig(int difficulty)
    {
        return _difficultyConfigAccessor[difficulty];
    }

    public List<DifficultyConfig> GetDifficultyConfigList()
    {
        return _difficultyConfigAccessor.ConfigList;
    }

    public int GetMapReRollCost(int times)
    {
        times = Math.Clamp(times, 0, _reRollMapCostList.Count - 1);
        return _reRollMapCostList[times];
    }

    public MallAdConfig? GetMallAdsConfig(int id)
    {
        return _mallAdsConfigAccessor[id];
    }

    public int GetNewCardAccumulate(int quality, int drewNewCardCount)
    {
        var config = _newCardConfigByQualityAccessor[quality];
        if (config == null)
            return -1;
        var newCardAccumulateList = config.new_card_accu_list;
        if (drewNewCardCount < newCardAccumulateList.Count)
            return newCardAccumulateList[drewNewCardCount];
        return newCardAccumulateList[^1];
    }

    public FixedMapConfig? GetFixedMapConfig(int id)
    {
        return _fixedMapConfigByIdAccessor[id];
    }

    public List<FixedMapConfig> GetFixedMapConfigList()
    {
        return _fixedMapConfigByIdAccessor.ConfigList;
    }

    public StarTask? GetStarTaskConfig(int id)
    {
        return _starTaskConfigByIdAccessor[id];
    }

    public BuildingConfig? GetBuildingConfigById(int id)
    {
        return _buildingConfigByIdAccessor[id];
    }

    public List<BuildingConfig> GetBuildingConfigList()
    {
        return _buildingConfigByIdAccessor.ConfigList;
    }

    public ResourceConfig? GetResourceConfigByKey(string key)
    {
        return _resourceConfigByKeyAccessor[key];
    }

    public List<ActivityTimeConfig> GetActivityConfigList() => _activityTimeConfigByIdAccessor.ConfigList;
    
    public ActivityTimeConfig? GetActivityConfigById(int id) => _activityTimeConfigByIdAccessor[id];

    public List<ActivityLuckyStarConfig>? GetActivityLuckyStarConfigListByActivityId(int activityId)
    {
        return _activityLuckyStarConfigByActivityId!.GetValueOrDefault(activityId, null);
    }

    public List<ActivityFortuneBagLevelConfig>? GetActivityFortuneBagLevelConfigListByActivityId(int activityId)
    {
        return _activityFortuneBagLevelConfigByActivityId!.GetValueOrDefault(activityId, null);
    }

    public ActivityFortuneBagConfig? GetActivityFortuneBagConfigByActivityId(int activityId)
    {
        return _activityFortuneBagConfigByActivityId!.GetValueOrDefault(activityId, null);
    }

    public ActivitySlotMachineConfig? GetActivitySlotMachineConfigByActivityId(int activityId)
    {
        if (_slotMachineConfigByActivityId.TryGetValue(activityId, out var config))
            return config;
        return null;
    }

    public List<ActivitySlotMachineDrawConfig>? GetActivitySlotMachineDrawConfigByActivityId(int activityId)
    {
        if (_slotMachineDrawConfigByActivityId.TryGetValue(activityId, out var configList))
            return configList;
        return null;
    }
    public ActivitySlotMachineDrawConfig? GetActivitySlotMachineDrawConfigByActivityIdAndDrawId(int activityId, int drawId)
    {
        if (_slotMachineDrawConfigByActivityId.TryGetValue(activityId, out var configList))
            return configList.Count > drawId ? configList[drawId] : null;
        return null;
    }

    public List<ActivitySlotMachineDrawRewardConfig>? GetActivitySlotMachineDrawRewardConfigByActivityId(int activityId)
    {
        if (_slotMachineDrawRewardPoolByActivityId.TryGetValue(activityId, out var configList))
            return configList;
        return null;
    }

    public int GetSlotMachineDoubleUpItemLevel(int itemId)
    {
        if (_slotMachineDoubleUpItemLevelDict.TryGetValue(itemId, out var level))
            return level;
        return -1;
    }
    
    public List<ActivitySlotMachinePointRewardConfig>? GetActivitySlotMachinePointRewardConfigByActivityId(
        int activityId)
    {
        if (_slotMachinePointRewardConfigByActivityId.TryGetValue(activityId, out var configList))
            return configList;
        return null;
    }

    public TreasureHuntConfig? GetTreasureHuntConfigByActivityId(int activityId)
    {
        return _treasureHuntConfigByIdAccessor[activityId];
    }

    public List<TreasureHuntDrawConfig>? GetTreasureHuntDrawConfigByActivityId(int activityId)
    {
        if (_treasureHuntDrawConfigByActivityId.TryGetValue(activityId, out var configList))
            return configList;
        return null;
    }

    public List<TreasureHuntPointRewardConfig>? GetTreasureHuntPointRewardConfigByActivityId(int activityId)
    {
        if (_treasureHuntPointRewardConfigByActivityId.TryGetValue(activityId, out var configList))
            return configList;
        return null;
    }
    
    public List<ActivityOneShotKillMapConfig> GetOneShotKillMapConfigListByActivityId(int activityId)
    {
        if (_oneShotKillMapByActivityId.TryGetValue(activityId, out var result))
            return result;
        return new();
    }
    public ActivityOneShotKillMapConfig? GetOneShotKillMapConfig(int activityId, int level)
    {
        if (!_oneShotKillMapByActivityId.TryGetValue(activityId, out var list))
            return null;
        return level < 0 || level >= list.Count ? null : list[level];
    }
    public List<ActivityOneShotKillLeaderConfig> GetOneShotKillLeaderConfigListByActivityId(int activityId)
    {
        if (_oneShotKillLeaderByActivityId.TryGetValue(activityId, out var result))
            return result;
        return new();
    }
    public List<ActivityOneShotKillTaskConfig> GetOneShotKillTaskConfigListByActivityId(int activityId)
    {
        if (_oneShotKillTaskByActivityId.TryGetValue(activityId, out var result))
            return result;
        return new();
    }

    public int GetOneShotKillInitialWarZoneCount(int activityId)
    {
        int result = 0;
        foreach (var map in GetOneShotKillMapConfigListByActivityId(activityId))
            if (map.default_progress > 0 && map.default_progress < map.victory_count_to_conquer)
                result++;
        return result;
    }
    public GeneralReward GetOneShotKillUltimateRewardByActivityId(int activityId)
    {
        if (_oneShotKillUltimateRewardConfigByActivityId.TryGetValue(activityId, out var result))
            return result;
        return new();
    }

    public TreasureMazeDifficultyConfig? GetTreasureMazeDifficultyConfig(int activityConfigId, int difficulty)
    {
        var targetDict =  _treasureMazeDifficultyConfigByActivityId!.GetValueOrDefault(activityConfigId, null);
        return targetDict!.GetValueOrDefault(difficulty, null);
    }
    public TreasureMazeLootConfig? GetTreasureMazeLootConfig(int activityConfigId, int lootId)
    {
        var targetDict =  _treasureMazeLootConfigByActivityId!.GetValueOrDefault(activityConfigId, null);
        return targetDict!.GetValueOrDefault(lootId, null);
    }

    public List<RewardPoolItem>? GetTreasureMazeRewardPoolByLootId(int activityId, int lootId)
    {
        var targetDict =  _treasureMazeLootRewardPoolByActivityId!.GetValueOrDefault(activityId, null);
        return targetDict!.GetValueOrDefault(lootId, null);
    }
    
    public IapCommodityConfig? GetSkuItemConfig(string iapId)
    {
        return _skuItemConfigByKeyAccessor[iapId];
    }

    public List<IapCommodityConfig> GetSkuItemConfigList()
    {
        return _skuItemConfigByKeyAccessor.ConfigList;
    }

    public IdleRewardConfig? GetIdleRewardConfigById(int id)
    {
        return _idleRewardConfigAccessor[id];
    }

    public int GetDecorationItemFromPool(int prizePoolId, Random rand)
    {
        var pool = _decorativeItemPoolAccessor[prizePoolId];
        if (pool == null) return -1;
        if (pool.item_list.Length == 0) return -1;
        var totalWeight = pool.weight_list.Sum() + pool.empty_chance;
        var randomNumber = rand.Next() % totalWeight;
        for (int i = 0; i < pool.weight_list.Length; i++)
        {
            randomNumber -= pool.weight_list[i];
            if (randomNumber < 0)
                return pool.item_list[i];
        }

        return -1;
    }

    public FixedMapStarRewardConfig? GetTopStoryStarRewardConfigByStarCount(int starCount)
    {
        FixedMapStarRewardConfig? result = null;
        foreach (var config in _fixedMapStarRewardConfigAccessor.ConfigList)
        {
            if (starCount >= config.star_required &&
                (result == null || result.star_required < config.star_required))
                result = config;
        }

        return result;
    }
    public FixedMapStarRewardConfig? GetFixedMapStarRewardConfigs(int id)
    {
        return _fixedMapStarRewardConfigAccessor[id];
    }

    public List<ActivityPiggyBankConfig> GetPiggyBankConfigList()
    {
        return _piggyBankConfigAccessor.ConfigList;
    }

    public bool IfAllPiggyBankRewardClaimed(long rewardClaimStatus)
    {
        return rewardClaimStatus == _piggyBankAllRewardClaimedStatus;
    }

    public UnrivaledGodConfig? GetUnrivaledGodConfigByActivityId(int activityId)
    {
        return _unrivaledGodConfigByIdAccessor[activityId];
    }

    public UnrivaledGodTaskConfig? GetUnrivaledGodTaskConfig(int activityId, string taskKey)
    {
        if (!_unrivaledGodTaskConfigByActivityId.TryGetValue(activityId, out var dict))
            return null;
        return dict.GetValueOrDefault(taskKey);
    }

    public UnrivaledGodExchangeConfig? GetUnrivaledGodExchangeConfig(int activityId, int exchangeId)
    {
        if (!_unrivaledGodExchangeConfigByActivityId.TryGetValue(activityId, out var dict))
            return null;
        return dict.GetValueOrDefault(exchangeId);
    }

    public List<CoopBossRewardConfig>? GetCoopBossRewardConfigListByActivityId(int activityId)
    {
        return _coopBossRewardConfigByActivityId.TryGetValue(activityId, out var list) ? list : null;
    }

    public RpgGameLevelConfig? GetRpgGameLevelConfigByLevelAndActivityId(int activityId, int level)
    {
        if (_rpgGameLevelConfigByActivityId.TryGetValue(activityId, out var list) &&
            level >= 0 && level < list.Count)
        {
            return list[level];
        }
        return null;
    }

    public LoogGameLevelConfig? GetLoogGameLevelConfigByLevelAndActivityId(int activityId, int level)
    {
        if (_loogGameLevelConfigByActivityId.TryGetValue(activityId, out var list) &&
            level >= 0 && level < list.Count)
        {
            return list[level];
        }
        return null;
    }
    
    public int GetBanedSecondsByAccumulate(int accumulate)
    {
        var configList = _banPlayerTimeConfigAccessor.ConfigList;
        int banedHour = 0;
        foreach (var config in configList)
        {
            if (accumulate < config.accumulate)
                break;
            banedHour = config.ban_hours;
        }

        // 转化为秒
        return banedHour * 3600;
    }

    public List<EnemyConfig> GetEnemyConfigList()
    {
        return _enemyConfigAccessor.ConfigList;
    }

    public List<FightBuffConfig> GetFightBuffConfigList()
    {
        return _fightBuffConfigAccessor.ConfigList;
    }
    
    // CsgoLottery getters
    public List<CsgoLotteryConfig>? GetCsgoLotteryConfigListByActivityId(int activityId)
    {
        return _csgoLotteryConfigByActivityId.TryGetValue(activityId, out var list) ? list : null;
    }
    
    public CsgoLotteryConfig? GetCsgoLotteryConfigByActivityIdAndRewardId(int activityId, int rewardId)
    {
        if (_csgoLotteryConfigByActivityId.TryGetValue(activityId, out var list) &&
            rewardId >= 0 && rewardId < list.Count)
            return list[rewardId];
        return null;
    }
    
    public List<CsgoLotteryPointConfig>? GetCsgoLotteryPointConfigListByActivityId(int activityId)
    {
        return _csgoLotteryPointConfigByActivityId.TryGetValue(activityId, out var list) ? list : null;
    }
    
    public CsgoLotteryPointConfig? GetCsgoLotteryPointConfigByActivityIdAndRewardId(int activityId, int rewardId)
    {
        if (_csgoLotteryPointConfigByActivityId.TryGetValue(activityId, out var list) &&
            rewardId >= 0 && rewardId < list.Count)
            return list[rewardId];
        return null;
    }
    
    public List<CsgoLotteryPassConfig> GetCsgoLotteryPassConfigListByActivityId(int activityId)
    {
        return _csgoLotteryPassConfigByActivityId.TryGetValue(activityId, out var list) ? list : new List<CsgoLotteryPassConfig>();
    }
    
    public CsgoLotteryPassConfig? GetCsgoLotteryPassConfigByActivityIdAndLevel(int activityId, int level)
    {
        if (_csgoLotteryPassConfigByActivityId.TryGetValue(activityId, out var list) &&
            level >= 0 && level < list.Count)
            return list[level];
        return null;
    }
    
    public CsgoLotteryPassConfig? GetCsgoLotteryPassConfigByItemId(int activityId, int itemId)
    {
        if (_csgoLotteryPassConfigByActivityId.TryGetValue(activityId, out var list))
            return list.FirstOrDefault(c => c.item_id == itemId);
        return null;
    }
    
    public List<CsgoLotteryTaskConfig> GetCsgoLotteryTaskConfigListByActivityId(int activityId)
    {
        return _csgoLotteryTaskConfigByActivityId.TryGetValue(activityId, out var list) ? list : new List<CsgoLotteryTaskConfig>();
    }

    public CsgoLotteryTaskConfig? GetCsgoLotteryTaskConfigByKey(int activityId, string taskKey)
    {
        if (_csgoLotteryTaskConfigByActivityId.TryGetValue(activityId, out var list))
            return list.FirstOrDefault(c => c.task_key == taskKey);
        return null;
    }
    
    public List<WeeklyMailConfig> GetWeeklyMailConfigList() => _weeklyMailConfigAccessor.ConfigList;
}
