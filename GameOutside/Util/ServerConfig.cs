// ReSharper disable InconsistentNaming

#pragma warning disable CS8618

namespace GameExternal
{
    [Serializable]
    public class DailyCommodityConfig
    {
        public enum BuyingType
        {
            COIN,
            DIAMOND,
            FREE
        }

        public int id;
        public string key;
        public BuyingType buying_type;
        public int quality;
        public List<int> random_range;
        public int single_price;
    }

    [Serializable]
    public class WinRewardConfig
    {
        public int difficulty;
        public List<int> item_list;
        public List<int> count_list;
        public int box_id;
    }

    [Serializable]
    public class SocScoreConfig
    {
        public string soc;
        public int score;
    }

    [Serializable]
    public class DeviceScoreConfig
    {
        public string device;
        public int score;
    }

    [Serializable]
    public class DelayBoxDropConfig
    {
        public int id;
        public int box_id;
        public int quality;
        public bool loop;
        public int fixed_box_id;
    }

    [Serializable]
    public class DivisionRewardConfig
    {
        public int division;
        public List<int> item_list;
        public List<int> count_list;
        public int diamond;
        public int coin;
    }

    [Serializable]
    public class RankRewardConfig
    {
        public int division;
        public int rank;
        public List<int> item_list;
        public List<int> count_list;
    }

    [Serializable]
    public class BuildingScoreConfig
    {
        public int id;
        public int min_season;
        public int score;
        public int damage_score;
    }

    [Serializable]
    public class ScoreRewardConfig
    {
        public int time;
        public List<int> item_list;
        public List<int> count_list;
    }

    [Serializable]
    public class NewCardConfig
    {
        public int quality;
        public List<int> new_card_accu_list;
    }

    [Serializable]
    public class BuildingConfig
    {
        public enum BuildingTypeType
        {
            HEADQUARTER, LIVING, FARM, WOOD, ATTACK, WALL, WORK_SHOP, STONE, HUNTING_CABIN, LAB, WAREHOUSE, AURA,
            MILITARY_CAMP, OTHER
        }

        public int id;
        public string key;
        public BuildingTypeType building_type;
    }

    [Serializable]
    public class ResourceConfig
    {
        public int id;
        public string key;
        public bool is_static;
        public int score;
        public int cheating_accu;
        public int cheating_production;
    }

    [Serializable]
    public class BanPlayerTimeConfig
    {
        public int accumulate;
        public int ban_hours;
    }

    [Serializable]
    public class WeeklyMailConfig
    {
        public int id;
        public string send_time;
        public string expired_time;
        public int[] item_list;
        public int[] count_list;
        public string template_id;
    }
}