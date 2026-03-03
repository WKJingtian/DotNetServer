// ReSharper disable InconsistentNaming

#pragma warning disable CS0618
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameExternal;

// CommonConfig文件保持与客户端一致
namespace GameExternal
{
    [Serializable]
    public class DivisionRankLine
    {
        public int level_0;
        public int level_1;
        public int level_2;
        public int level_3;
        public int level_4;
        public int level_5;
        public int level_6;
        public int level_7;
        public int level_8;
        public int level_9;
        public int level_10;
        public int level_11;
        public int level_12;
        public int level_13;
        public int level_14;
        public int level_15;
        public int level_16;
        public int level_17;
        public int level_18;
        public int level_19;
        public int level_20;
        public int level_21;
        public int level_22;
        public int level_23;
        public int level_24;
        public int level_25;
    }

    [Serializable]
    public class DivisionConfig
    {
        public int level;
        public int count;
        public int game_count_to_next_division;
        public int population;
        public int category;
        public int max_population;
        public int robot_score_min;
        public int robot_score_max;
        public int robot_level_min;
        public int robot_level_max;
        public int[] daily_progress_reward;
    }

    [Serializable]
    public class CommodityConfig
    {
        public int id { get; set; }
        public string key { get; set; }
        public string icon_key { get; set; }
        public int coin_price { get; set; }
        public int diamond_price { get; set; }
        public bool can_buy_10 { get; set; }
        public int[] item_list { get; set; }
        public int[] count_list { get; set; }
        public List<int> weight_list { get; set; }
        public int coin { get; set; }
        public int diamond { get; set; }
        public int buy_max { get; set; }
        public int require_division { get; set; }
        public int quality { get; set; }

        [NonSerialized]
        public List<int> accu_weight_list;
    }

    public enum ItemQuality
    {
        Ordinary = 0,  // 普通
        Rare = 1,      // 稀有
        Unique = 2,    // 绝世
        Unrivaled = 3, // 无双
    }

    public enum MoneyType
    {
        Diamond = 0,            // 玉璧
        Coin = 1,               // 铜币
        Exp = 2,                // 经验
        BattlePassExp = 3,      // 战令经验
        UnrivaledKey = 4,       // 无双钥匙(无双神将活动代币
        UnrivaledScore = 5,     // 无双积分
        SlotMachineScore = 6,   // 老虎机奖励翻倍
        TreasureHuntKey = 7,    // 灵犀寻宝钥匙
        TreasureHuntScore = 8,  // 灵犀寻宝积分
        CsgoStyleLottery = 9,   // CSGO开箱的钥匙
        CsgoLotteryPoint = 10,  // CSGO开箱积分
    }

    // 添加新的物品类型时，需要同时修改下列方法：
    // RefundGenericItem
    // CalculateUserAssetIncludeOptions
    // CalculateUserAssetIncludeOptionsForRefund
    public enum ItemType
    {
        Currency = 0,               // 货币
        SoldierCard = 1,            // 兵卡
        Advice = 2,                 // 建言
        TowerCard = 3,              // 防御塔
        BuildingCard = 4,           // 建筑卡牌
        TreasureBox = 5,            // 宝箱
        Common = 6,                 // 普通物品
        BattlePass = 7,             // 战令通行证
        Hero = 8,                   // 领袖
        MagicCard = 9,              // 魔法外卡
        NameCard = 10,              // 名片
        Avatar = 11,                // 头像
        AvatarFrame = 12,           // 头像框
        IdleRewardBox = 13,         // 聚宝皮肤
        MonthPass = 14,             // 月卡
        ArenaTicket = 15,           // 特殊模式门票
        OneTimeBooster = 16,        // 游戏内一次性道具
        ItemPackage = 17,           // 礼包
        FortuneBag = 18,            // 福袋
        PiggyBank = 19,             // 存钱罐（貔貅瓮） 
        SlotMachineDoubleUp = 20,   // 老虎机双倍奖励
        CsgoLotteryPass = 21,       // csgo开箱活动通行证
        MainPageModel = 22,         // 主页面模型
    }

    [Serializable]
    public class ItemConfig
    {
        public int id;
        public string detailed_key;
        public bool default_item;
        public ItemType type;
        public ItemQuality quality;
        public float camera_distance;
        public string icon;
        public string min_version;
        public float diamond_value;
    }

    [Serializable]
    public class ItemPackageConfig
    {
        public int id;
        public int[] item_list;
        public int[] item_count;
        public int coin_count;
        public int diamond_count;
        public int refund_diamond;
    }
    
    public enum IapType
    {
        DIAMOND = 0,         // 玉璧充值
        BATTLE_PASS = 1,     // 战令
        NEW_PLACER_PACK = 2, // 首充礼包
        DAILY_PACK = 3,      // 每日礼包
        ULTIMATE_PACK = 4,   // 至臻礼包
        WEEKLY_PACK = 5,     // 每周礼包
        TIME_LIMIT_PACK = 6, // 限时礼包
        PIGGY_BANK = 7,      // 貔貅瓮
        MONTH_PASS = 8,      // 月卡
        FORTUNE_BAG = 9,     // 福袋
        SLOT_MACHINE_DOUBLE_UP = 10,// 老虎机双倍奖励
    }

    [Serializable]
    public class IapCommodityConfig
    {
        public string prop_id;
        public IapType type;
        public int item_id;
        public int item_count;
        public string icon_key;
        public int buy_limit;
        public int limit_refresh_interval;
        public int sp_limit_refresh_rule;
        public bool recommended;
        public bool hide_if_sold_out;
        public bool always_hide_in_store;
        public List<string> share_limit_with;
    }

    [Serializable]
    public class UserLevelConfig
    {
        public int level;
        public int level_score;
        public int[] item_list;
        public int[] count_list;
        public int unlock_difficulty;
        public string game_version;
    }

    [Serializable]
    public class FightCardConfig
    {
        public int id;
        public string key;
        public int level;
        public int upgrade_need_exp;
        public int upgrade_need_coin;
        public double health_mul;
        public double atk_mul;
        public int b_c_add;
        public double slight_mul;
        public double atk_radiu_mul;
        public double atk_speed_mul;
        public double crit_rate;
        public double crit_mult;
        public double move_mul;
        public double weapon_dis_mul;
        public double hit_dis_mul;
        public int group_add;
        public List<string> buffs;
        public bool IsMaxLevel => upgrade_need_exp == -1;
    }


    [Serializable]
    public class BuildingCardConfig
    {
        public int id;
        public string key;
        public List<string> building_list;
        public List<int> level_exp_list;
        public List<int> level_coin_list;
        public List<double> health_buff_list;
        public List<double> pro_buff_list;
        public List<double> cap_buff_list;
        public List<double> res_mines_buff_list;
        public List<double> res_add_buff_list;
        public List<double> exp_buff_list;
        public List<double> buff_ratio_list;
    }

    [Serializable]
    public class HeroConfig
    {
        public int id;
        public string key;
        public List<string> buffs;
    }

    public enum TreasureBoxType
    {
        FightCard = 0,
        BuildingCard = 1,
        AllCard = 2,
        FixedCardPool = 3,
        FixedCardAndCount = 4,
    }

    [Serializable]
    public class TreasureBoxConfig
    {
        public int id;
        public int coin_min;
        public int coin_max;
        public int diamond_min;
        public int diamond_max;
        public TreasureBoxType box_type;
        public int card_count;
        public int different_card_count;
        public int lucky_star_level; // 福星宝箱阶数

        public List<int> guarantee_count_list;
        public List<int> weight_list;
        public List<int> fixed_card_pool;
        public List<int> fixed_card_count_list;

        public int deco_item_pool;
    }

    [Serializable]
    public class ParamContent
    {
        public string key;
        public string content;
    }

    public enum GameMapType
    {
        FreeMap = 0,
        StoryMap = 1,
        OnlineMap = 2,
        EndlessMap = 3,
        SurvivorMap = 4,
        TowerDefenceMap = 5,
        FixedMap = 6,
        TrueEndlessMap = 7,
        CoopBossMap = 8,
        TreasureMazeMap = 9,
        OneShotKillMap = 10,
        RpgGameMap = 11,
        LoogGameMap = 12,
    }

    [Serializable]
    public class MapConfig
    {
        public int id;
        public List<int> generators;
        public int environment;
        public bool free_map;
        public string custom_args;
        public string guide;
        public string predefine;
        public string boss;
        public bool endless;
        public List<int> levels;
        public List<int> tasks;
        public bool user_free_map_index;
    }

    [Serializable]
    public class AttendanceRewardConfig
    {
        public int day_count;
        public int[] item_list;
        public int[] count_list;
        public string icon;
    }

    [Serializable]
    public class ExploreEventConfig
    {
        public int id;
        public ExploreEventType type;
        public string param1;
        public string param2;
        public string param3;
        public int weight;
    }

    public enum ExploreEventType
    {
        OuterReward = 0,
        Enemy = 1,
        ResReward = 2,
    }

    [Serializable]
    public class EndlessRewardConfig
    {
        public int score;
        public int[] item_list;
        public int[] count_list;
        public int treasure_box_id;
    }

    [Serializable]
    public class BattlePassConfig
    {
        public int pass_id;
        public int level;
        public int exp;
        public int[] item_list;
        public int[] count_list;
        public int refund_diamond;
    }

    [Serializable]
    public class BattleExpRulesConfig
    {
        public string key;
        public float ratio;
        public int max;
        public int max_score;
        public int min_season;
        public string eval_equation;
    }

    // 用于运行时存储编译后的lambda表达式
    public class BattleExpRules
    {
        public string key;
        public float ratio;
        public int max;
        public int max_score;
        public int min_season;
        public Func<double, double, double> eval_lambda;
    }

    public enum AchievementType
    {
        MAP = 0,      // 地形
        BUFF = 1,     // 增益
        CARD = 2,     // 卡牌
        HERO = 3,     // 领袖
        ENEMY = 4,    // 敌人
        BUILDING = 5, // 建筑
        DEVELOP = 6,  // 养成
    }

    [Serializable]
    public class AchievementConfig
    {
        public int id;
        public string key;
        public string target;
        public AchievementType type;
        public int[] progress_list;
        public int[] reward_list;
        public int[] count_list;
        public bool cycling; // 成就奖励是否可循环
    }
}

[Serializable]
public class BeginnerTaskDayConfig
{
    public int day;
    public int reward_item;
    public int reward_count;
    public int task_count;
    public int[] predefined_tasks;
}

[Serializable]
public class BeginnerTaskConfig
{
    public int id;
    public string key;
    public int target_progress;
    public string target_key;
    public int coin;
    public int diamond;
}

public enum DailyTaskType
{
    LOG_IN = 0, // 登录游戏
    PLAY_STORY_MAP = 1, // 挑战战役模式
    PLAY_ARENA_MAP = 2, // 挑战竞技场模式
    CLAIM_IDLE_REWARD = 3, // 领取聚宝
    KILL_ENEMY = 4, // 击败敌人（1000个）
    UPGRADE_CARD = 5, // 升级卡牌
    JOIN_ACTIVITY = 6, // 参与限时活动
    OPEN_TREASURE_BOX = 7, // 打开宝箱
    CHARGE_MONEY = 8, // 充值
}

[Serializable]
public class DailyTaskConfig
{
    public int id;
    public DailyTaskType daily_task_type;
    public int target_progress;
    public int active_score_reward;
}

[Serializable]
public class ActiveScoreLevelConfig
{
    public int id;
    public int score_required;
    public int item_id;
    public int item_count;
}

[Serializable]
public class PresetDailyStoreItemConfig
{
    public int id;
    public int item_id;
    public int item_count;
    public int price;
    public int price_type;
}

[Serializable]
public class DifficultyConfig
{
    public int level;
    public List<int> elit_enemy_list;
    public List<int> boss_list;
    public List<string> map_list;
    public bool endless;
    public List<double> enemy_hp;
    public List<double> enemy_attack;
    public List<double> enemy_moving;
    public List<float> score_mult;
    public List<float> card_level;
    public List<int> need_stars;
    public string enemy_prefab;
    public string enemy_mode;
    public int time;
    public int unlock_fixed_map;
    public int unlock_user_level;
}

[Serializable]
public class ModeAvailabilityConfig
{
    public string mode;
    public List<int> open_day_list;
}

[Serializable]
public class DifficultySelectionConfig
{
    public int id;
    public string key;
    public int lock_difficulty;
    public float score_mult;
    public bool show;
}

[Serializable]
public class ExploreBoxConfig
{
    public int count;
    public int convert_time;
}

[Serializable]
public class MallAdConfig
{
    public int id;
    public int[] item_list;
    public int[] count_list;
    public string icon;
    public ItemQuality quality;
    public int count;
    public string ad_name;
}

[Serializable]
public class CommonCardPool
{
    public int id;
    public List<int> card_list;
}

public enum GameplayType
{
    Normal = 0,
    Train = 1,
    Survivor = 2,
    TowerDefence = 3,
}

[Serializable]
public class FixedMapConfig
{
    public int id;
    public string custom_args;
    public string guide;
    public string predefine;
    public GameplayType gameplay_type;
    public int generator;
    public int environment;
    public List<string> elite_enemy_list;
    public List<int> tasks;
    public string tag;
    public string enemy_mode;
    public double enemy_atk;
    public double enemy_health;
    public double enemy_speed;
    public string boss;
    public float score_mult;
    public int star_prerequisite;
    public List<int> star_tasks;
    public int[] item_list;
    public int[] item_count_list;
    public string icon;
    public string desc_key;
    public string unlock_info;
    public int special_pre_level;
    public int max_military_unit;
    public bool use_free_map_index;

    public bool IsTrainLevel => gameplay_type == GameplayType.Train;
}

public static class StarTaskKeys
{
    public const string GameWin = "game_win";
    public const string BuildingListCountBiggerOrEqualNotIncludeWall =
        "building_list_count_bigger_or_equal_not_include_wall";
    public const string DamagedBuildingLessOrEqualNotIncludeWall = "damaged_building_less_or_equal_not_include_wall";
    public const string UnlockTileCountBiggerOrEqual = "unlock_tile_count_bigger_or_equal";
    public const string TrainCountBiggerOrEqual = "train_count_bigger_or_equal";
    public const string BuildingTypeCountBiggerOrEqual = "buliding_type_count_bigger_or_equal";
    public const string ResAccuCountBiggerOrEqual = "res_accu_count_bigger_or_equal";
    public const string ResProductionBiggerOrEqual = "res_production_bigger_or_equal";
    public const string HeadquarterHpPercentBiggerOrEqual = "headquarter_hp_percent_bigger_or_equal";
    public const string WinAndFightUnitCountLessEqual = "win_and_fight_unit_count_less_or_equal";
    public const string FightUnitTotalDamageBiggerOrEqual = "fight_unit_total_damage_bigger_or_equal";
    public const string DamagedBuildingTypeCountLessOrEqual = "damaged_building_type_count_less_or_equal";
}

[Serializable]
public class StarTask
{
    public int id;
    public string key;
    public string arg_0;
    public string arg_1;
    public bool loc_arg_0;
}

[Serializable]
public class ActivityTimeConfig
{
    public int id;
    public string activity_type;
    public string start_time;
    public string end_time;
    public string lock_time;
    public int unlock_user_level;
    public string min_version;
}

[Serializable]
public class ActivityLuckyStarConfig
{
    public int activityId;
    public int id;
    public int cost_diamond;
    public int box_id;
    public int free_probability;
}

[Serializable]
public class ActivityFortuneBagLevelConfig
{
    public int activityId;
    public int id;
    public int fortune_bag_required;
    public List<int> item_list;
    public List<int> count_list;
}

[Serializable]
public class ActivityFortuneBagConfig
{
    public int activityId;
    public int fortune_bag_max_stack;
    public List<int> fortune_bag_diamond_count;
    public int max_fortune_bag_reward;
}

public enum IdleRewardAcquireMethod : byte
{
    FREE = 0,
    SOLD_OUT = 1,
    PURCHASE_BY_DIAMOND = 2,
    PURCHASE_BY_COIN = 3,
    FROM_TREASURE_BOX = 4,
    FROM_FORTUNE_BAG = 5,
}

[Serializable]
public class IdleRewardConfig
{
    public int id;
    public string prefab;
    public int fill_time;
    public int steal_player_count;
    public bool no_steal_effect;
    public int box_id;
    public int steal_box_id;
    public float output_multiplier;
    public IdleRewardAcquireMethod acquire_method;
    public int price;
}

[Serializable]
public class DecorativeItemPoolConfig
{
    public int prize_pool_id;
    public int[] item_list;
    public int[] weight_list;
    public int empty_chance;
}

[Serializable]
public class FixedMapStarRewardConfig
{
    public int id;
    public int star_required;
    public int[] item_list;
    public int[] count_list;
    public string icon;
    public int idle_reward_extra_coin;
    public int idle_reward_extra_card;
}

[Serializable]
public class ActivityPiggyBankConfig
{
    public int id;
    public List<int> item_list;
    public List<int> count_list;
}

[Serializable]
public class UnrivaledGodConfig
{
    public int activity_id;
    public int cost_diamond;
    public int score_min;
    public int score_max;
    public int[] item_list;
    public int[] weight_list;
    public int guarantee_count;
    public int unrivaled_card_id;
}

public static class ActivityTaskKeys
{
    public const string Login = "login";
    
    public const string LoginDaily = "login_daily";

    public const string ExplorePoint = "explore_point";

    public const string AccuUnrivaledGodDrawReward = "accu_draw_reward";
    
    public const string EndlessChallenge = "endless_challenge";
    
    public const string CoopBoss = "coop_boss";
    
    public const string TreasureMaze = "treasure_maze";
    
    public const string OneShotKill = "one_shot_kill";
    
    public const string RpgGameChallenge = "rpg_game_challenge";
    
    public const string DrawCsgoLottery = "draw_csgo_lottery";

    public const string LoogGameChallenge = "loog_game_challenge";
}

[Serializable]
public class UnrivaledGodTaskConfig
{
    public int activity_id;
    public string task_key;
    public int target_progress;
    public int reward_item;
    public int reward_count;
    public bool is_daily;
}

[Serializable]
public class UnrivaledGodExchangeConfig
{
    public int activity_id;
    public int id;
    public int item_id;
    public int item_count;
    public int limit_count;
    public int price;
}

[Serializable]
public class CoopBossRewardConfig
{
    public int activity_id;
    public int id;
    public List<int> item_list;
    public List<int> count_list; 
    public string enemy_generator_tag;
}

[Serializable]
public class RpgGameLevelConfig
{
    public int activity_id;
    public int id;
    public List<int> item_list;
    public List<int> count_list;
    public List<int> first_victory_item_list;
    public List<int> first_victory_count_list;
    public List<int> map_list;
    public float score_mult;
}

[Serializable]
public class LoogGameLevelConfig
{
    public int activity_id;
    public int id;
    public List<int> item_list;
    public List<int> count_list;
    public List<int> first_victory_item_list;
    public List<int> first_victory_count_list;
    public List<int> map_list;
    public float score_mult; 
}

[Serializable]
public class EndlessDifficultyConfig
{
    public int difficulty;
    public float score_mult;
    public int unlock_day;
}

[Serializable]
public class BattlePassTimeConfig
{
    public int pass_id;
    public string start_time;
    public string end_time;
}

[Serializable]
public class ActivitySlotMachineConfig
{
    public int activity_id;
    public int max_draw_time_per_day;
    public int max_reroll_base_cost;
    public int reroll_base_cost;
    public int reroll_base_cost_increment;
    public List<int> guarantee_count_list;
}

[Serializable]
public class ActivitySlotMachineDrawConfig
{
    public int activity_id;
    public int draw_id;
    public int diamond_cost;
    public int reward_count_multiplier;
    public string double_up_iap_id;
    public int double_up_diamond_price;
}

[Serializable]
public class ActivitySlotMachineDrawRewardConfig
{
    public int activity_id;
    public int reward_id;
    public int quality;
    public int item_id;
    public int item_count;
    public int point;
    public int weight;
}

[Serializable]
public class ActivitySlotMachinePointRewardConfig
{
    public int activity_id;
    public int reward_id;
    public int point_required;
    public int item_id;
    public int item_count;
}

public enum OneShotKillRegionState
{
    Fallen = 0,
    WarZone = 1,
    Freed = 2,
}

public enum OneShotKillEventType
{
    TaskCompleted = 1,
    RegionFell = 2,
    RegionWarBegin = 3,
    RegionFreed = 4,
    TaskStarted = 5,
    TaskFailed = 6,
    LeaderJoined = 7,
    ActivityBegin = 8,
    StageOneBegin = 9,
    StageTwoBegin = 10,
    StageThreeBegin = 11,
    AllRegionsFreed = 12,
}

[Serializable]
public class ActivityOneShotKillMapConfig
{
    public int activity_id;
    public int level;
    public List<int> first_time_reward_list;
    public List<int> first_time_reward_count;
    public int victory_count_to_conquer;
    public List<int> conquer_reward_list;
    public List<int> conquer_reward_count;
    public List<int> region_rgb;
    public float score_multiplier;
    public List<int> adjacent_region;
    public int default_progress;
    public List<string> region_buff;
    public int progress_per_victory;
    public int progress_per_challenge_mode_victory;
    public List<int> normal_reward_list;
    public List<int> normal_reward_count;
    public List<int> challenge_reward_list;
    public List<int> challenge_reward_count;
}

[Serializable]
public class ActivityOneShotKillLeaderConfig
{
    public int activity_id;
    public int leader_id;
    public string leader_key;
    public string leader_enable_time;
    public List<string> leader_buff;
    public string leader_icon;
    public int progress_add;
}

[Serializable]
public class ActivityOneShotKillTaskConfig
{
    public int activity_id;
    public int task_id;
    public string target_type;
    public string target_key;
    public int count_to_complete;
    public int progress_add;
    public string begin_time;
    public string end_time;
    public List<int> reward_list;
    public List<int> reward_count;
}

public class EnemyConfig
{
    public int id;
    public string enemy_group;
}

[Serializable]
public class FightBuffConfig
{
    public string key;
    public string achievement_group;
    public ItemQuality quality;
}

[Serializable]
public class TreasureMazeDifficultyConfig
{
    public int activity_id;
    public int difficulty;
    public List<int> first_time_reward_list;
    public List<int> first_time_reward_count;
    public float coin_reward_mult;
    public float score_multiplier;
}

[Serializable]
public class TreasureMazeLootConfig
{
    public int activity_id;
    public int loot_id;
    public List<string> resource_reward_list;
    public List<int> resource_count_min_list;
    public List<int> resource_count_max_list;
    public List<int> reward_pool;
    public List<int> reward_weight;
    public List<int> difficulty_list;
    public int enemy_id;
}

[Serializable]
public class TreasureHuntConfig
{
    public int activity_id;
    public int draw_diamond_base;
    public int draw_diamond_n;
    public int refresh_diamond_base;
    public int refresh_diamond_n;
    public int refresh_diamond_max;
    public int daily_max_refresh_time;
    public int refresh_lock_seconds;
    public int[] pool_count_by_quality;
    public float p_variant;
}

[Serializable]
public class TreasureHuntDrawConfig
{
    public int activity_id;
    public int reward_id;
    public int quality;
    public int item_id;
    public int item_count;
    public int point;
    public int weight;
}

[Serializable]
public class TreasureHuntPointRewardConfig
{
    public int activity_id;
    public int reward_id;
    public int point_required;
    public int item_id;
    public int item_count;
}

[Serializable]
public class CsgoLotteryConfig
{
    public int activity_id;
    public int reward_id;
    public int quality;
    public List<int> item_list;
    public List<int> count_list;
    public int probability;
    public int point_reward;
}

[Serializable]
public class CsgoLotteryPointConfig
{
    public int activity_id;
    public int reward_id;
    public List<int> item_list;
    public List<int> count_list;
    public int point_required;
}

[Serializable]
public class CsgoLotteryTaskConfig
{
    public int activity_id;
    public string task_key;
    public int target_progress;
    public int reward_item;
    public int reward_count;
    public bool is_daily;
}

[Serializable]
public class CsgoLotteryPassConfig
{
    public int activity_id;
    public string iap_id;
    public int item_id;
    public int level;
    public List<int> reward_item;
    public List<int> reward_count;
    public List<int> daily_reward_item;
    public List<int> daily_reward_count;
}