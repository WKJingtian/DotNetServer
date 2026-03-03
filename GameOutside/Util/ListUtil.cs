using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using GameOutside.Util;

namespace GameOutside;

public static class ListUtil
{
    public static T? WeightedRandomSelectOne<T>([NotNull] this List<T> list, [NotNull] Func<T, int> weightFunc)
        where T : class
    {
        if (list.IsNullOrEmpty())
            return null;
        int totalWeight = list.Sum(weightFunc);
        var random = new Random();
        int randomWeight = random.Next(0, totalWeight);
        int currentSum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            int weight = weightFunc(list[i]);
            currentSum += weight;
            if (randomWeight < currentSum)
                return list[i];
        }

        return default;
    }
    
    // 按照权重从一个列表里面随机n个元素（允许重复）
    public static Dictionary<T, int> WeightedRandomSelectAllowDuplicate<T>([NotNull] this List<T> source, int count, [NotNull] Func<T, int> weightFunc)
    {
        if (count == 0)
            return new Dictionary<T, int>();
        Dictionary<T, int> result = new Dictionary<T, int>();
        int totalWeight = source.Sum(weightFunc);
        for (int i = 0; i < count; i++)
        {
            var random = new Random();
            int randomWeight = random.Next(0, totalWeight);
            int currentSum = 0;
            foreach (var element in source)
            {
                int weight = weightFunc(element);
                currentSum += weight;
                if (randomWeight >= currentSum)
                    continue;
                // find one
                result.TryAdd(element, 0);
                result[element] += 1;
                break;
            }
        }
        
        return result;
    }
    // 按照权重从一个列表里面随机n个元素
    public static List<T> WeightedRandomSelectNoDuplicate<T>([NotNull] this List<T> source, int count, [NotNull] Func<T, int> weightFunc)
    {
        if (source.Count <= count)
            return source;
        if (count == 0)
            return new List<T>();
        HashSet<T> result = new HashSet<T>();
        int totalWeight = source.Sum(weightFunc);
        for (int i = 0; i < count; i++)
        {
            var random = new Random();
            int randomWeight = random.Next(0, totalWeight);
            int currentSum = 0;
            foreach (var element in source)
            {
                if (result.Contains(element))
                    continue;
                int weight = weightFunc(element);
                currentSum += weight;
                if (randomWeight >= currentSum)
                    continue;
                // find one
                result.Add(element);
                totalWeight -= weight;
                break;
            }
        }
        
        return result.ToList();
    }

    public delegate int Compare<T>(T value0, T value1);

    // 查找第一个不小于指定值的下标，如果不存在则返回list.count
    public static int LowerBound<T>([NotNull] this List<T> list, T value, Compare<T> compareFunc)
    {
        int mid;
        int left = 0;
        int right = list.Count;
        while (left < right)
        {
            mid = left + (right - left) / 2;
            if (compareFunc(value, list[mid]) <= 0)
            {
                right = mid;
            }
            else
            {
                left = mid + 1;
            }
        }

        if (left < list.Count && compareFunc(list[left], value) < 0)
        {
            left++;
        }

        // Return the lower_bound index
        return left;
    }

    // 查找第一个大于指定值的下标，如果不存在则返回list.count
    public static int UpperBound<T>([NotNull] this List<T> list, T value, Compare<T> compareFunc)
    {
        int mid;
        int left = 0;
        int right = list.Count;

        while (left < right)
        {
            mid = left + (right - left) / 2;
            if (compareFunc(value, list[mid]) >= 0)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        if (left < list.Count && compareFunc(list[left], value) <= 0)
        {
            left++;
        }

        // Return the upper_bound index
        return left;
    }

    public static int BinarySearchBiggerOrEqual<T>([NotNull] this List<T> list, T item, Compare<T> compareFunc)
    {
        if (list.Count <= 0)
            return -1;
        if (list.Count == 1)
            return 0;
        var left = 0;
        var right = list.Count - 1;
        if (compareFunc(item, list[right]) >= 0)
            return right;
        if (compareFunc(item, list[left]) <= 0)
            return left;
        while (left < right)
        {
            var mid = (left + right) / 2;
            var compareResult = compareFunc(item, list[mid]);
            if (compareResult == 0)
                return mid;
            if (compareResult < 0)
            {
                right = mid - 1;
                right = right < 0 ? 0 : right;
            }
            else
            {
                left = mid + 1;
                left = left > list.Count - 1 ? list.Count - 1 : left;
            }
        }

        if (compareFunc(item, list[right]) > 0)
            return right + 1;
        return right;
    }

    public static string ConcatToString<T>(this List<T> list, char split)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            sb.Append(item);
            if (i != list.Count - 1)
                sb.Append(split);
        }

        return sb.ToString();
    }

    public static bool IsNullOrEmpty<T>(this List<T>? list)
    {
        return list == null || list.Count == 0;
    }
    
    public static int[] SplitIntToIntArray(int totalCount, int splitCount)
    {
        if (totalCount < splitCount)
            return null;
        int[] array = new int[splitCount];
        int baseCount = totalCount / splitCount;
        // 初始化数组内容
        for (int i = 0; i < array.Length; i++)
            array[i] = baseCount;

        // 数组内随机倒一下
        for (int j = 0; j < 2; j++) // 迭代2次
        {
            for (int k = 0; k < splitCount; k++)
            {
                if (array[k] < 2)
                    continue;
                int randomIndex = Random.Shared.Next(0, array.Length);
                int num = Random.Shared.Next(1, array[k] / 2);
                if (array[k] - num > 0)
                {
                    array[k] -= num;
                    array[randomIndex] += num;
                }
            }
        }
        // 剩下的随机装一下
        int remainNum = totalCount % splitCount;
        for (int i = 0; i < remainNum; i++)
        {
            int randomIndex = Random.Shared.Next(0, array.Length);
            array[randomIndex]++;
        }
        return array;
    }
    
    private static Random rng = new Random();
    public static void Shuffle<T>(this IList<T> list)
    {
        int num = list.Count;
        while (num > 1)
        {
            num--;
            int index = rng.Next(num + 1);
            (list[index], list[num]) = (list[num], list[index]);
        }
    }
}