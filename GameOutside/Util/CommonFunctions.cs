using GameOutside.Models;
using UserCard = GameOutside.Models.UserCard;
using UserItem = GameOutside.Models.UserItem;
using UserTreasureBox = GameOutside.Models.UserTreasureBox;

// ReSharper disable CollectionNeverQueried.Global

namespace GameOutside.Util;

[Serializable]
public class RewardItemData
{
    public int Id { get; set; }
    public int Count { get; set; }
}

public struct ItemChange()
{
    public List<UserItem> AddList { get; set; } = new();
    public List<UserItem> ModifyList { get; set; } = new();
    public List<int> RemoveList { get; set; } = new();

    // 一些有特殊意义的物品
    public List<UserItem> SpecialItemList { get; set; } = new();
}

public struct CardChange()
{
    public List<UserCard> AddList { get; set; } = new();
    public List<UserCard> ModifyList { get; set; } = new();
}

public struct TreasureBoxChange()
{
    public List<UserTreasureBox> AddList { get; set; } = new();
    public List<Guid> RemoveList { get; set; } = new();
    public List<UserTreasureBox> ModifyList { get; set; } = new();
}

public class GeneralReward
{
    public List<int> ItemList { get; set; } = [];
    public List<int> CountList { get; set; } = [];

    public void AddReward(int itemId, int count)
    {
        ItemList.Add(itemId);
        CountList.Add(count);
    }

    public void AddRewards(List<int> itemIds, List<int> counts)
    {
        if (itemIds.Count != counts.Count)
            throw new Exception("GeneralReward AddReward error: itemIds.Count != counts.Count");

        ItemList.AddRange(itemIds);
        CountList.AddRange(counts);
    }
}

public class RewardPoolItem
{
    public int ItemId { get; set; }
    public int Weight { get; set; }
}

public class DifficultyChange
{
    public int Difficulty { get; set; }
    public int Level { get; set; }
    public int Star { get; set; }
}

public class UserAssetsChange
{
    public int NewCoin { get; set; }
    public int NewDiamond { get; set; }
    public int LevelScore { get; set; }
    public int Level { get; set; }
    public List<int>? HeroAdditions { get; set; } = [];
    public ItemChange ItemChange { get; set; } = new();
    public CardChange CardChange { get; set; } = new();
    public TreasureBoxChange TreasureBoxChange { get; set; } = new();

    public List<UserAchievement> AchievementChanges { get; set; } = new();

    public void FillAssetInfo(UserAssets assets)
    {
        NewCoin = assets.CoinCount;
        NewDiamond = assets.DiamondCount;
        Level = assets.LevelData.Level;
        LevelScore = assets.LevelData.LevelScore;
    }
}

public class UnpackItemListResult
{
    /// <summary>
    /// 新增的卡牌列表
    /// </summary>
    public readonly List<UserCard> NewCardList = [];

    /// <summary>
    /// 变化的卡牌列表，包含新增的和变化的
    /// </summary>
    public readonly HashSet<UserCard> CardChangeSet = [];

    /// <summary>
    /// 变化的物品列表，包含新增的和变化的
    /// </summary>
    public readonly HashSet<UserItem> ItemChangeSet = [];
}

public class TakeRewardResult
{
    public required UserAssetsChange AssetsChange { get; set; }
    public required GeneralReward Reward { get; set; }
}

public class PaymentAndPromotionStatusReply
{
    public required string LastPromotedPackageId { get; set; }
    public required long WhenPromoted { get; set; }
    public required int IceBreakingPromotionStatus { get; set; }
    public required HashSet<string> DoubleDiamondBonusTriggerRecords { get; set; }
}

public static class CommonFunctions
{
    public static void DistinctAndMerge(this GeneralReward reward)
    {
        var distinctList = new List<int>();
        var distinctCount = new List<int>();
        for (var i = 0; i < reward.ItemList.Count; ++i)
        {
            var index = distinctList.IndexOf(reward.ItemList[i]);
            if (index == -1)
            {
                distinctList.Add(reward.ItemList[i]);
                distinctCount.Add(reward.CountList[i]);
            }
            else
            {
                distinctCount[index] += reward.CountList[i];
            }
        }

        reward.ItemList = distinctList;
        reward.CountList = distinctCount;
    }

    public static void DistinctAndMerge(this List<RewardItemData> rewards)
    {
        for (int i = rewards.Count - 1; i >= 0; i--)
        {
            int itemId = rewards[i].Id;
            for (int j = 0; j < i; j++)
            {
                if (rewards[j].Id == itemId)
                {
                    rewards[j].Count += rewards[i].Count;
                    rewards.RemoveAt(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 比较版本号
    /// </summary>
    /// <param name="current"></param>
    /// <param name="target"></param>
    /// <returns>
    /// 0: 意味着两个版本号相等
    /// 大于0: current版本号更高
    /// 小于0: target版本号更高 
    /// </returns>
    public static int CompareVersionStrServer(this string current, string target)
    {
        var currentCodeList = new List<int>();
        var parts = current.Split(".");
        foreach (var intStr in parts)
            currentCodeList.Add(int.Parse(intStr));
        var targetCodeList = new List<int>();
        parts = target.Split(".");
        foreach (var intStr in parts)
            targetCodeList.Add(int.Parse(intStr));
        if (targetCodeList.Count != currentCodeList.Count)
            return currentCodeList.Count - targetCodeList.Count;
        var count = currentCodeList.Count;
        for (var i = 0; i < count; ++i)
        {
            if (currentCodeList[i] == targetCodeList[i])
                continue;
            return currentCodeList[i] - targetCodeList[i];
        }

        return 0;
    }
}