using GameOutside;

public static class MiscUtils
{
    public static int RandomCountRemap(int weight, int totalCount, int limitIterCount, Random random)
    {
        float p = (float) weight / 1000; // 概率
        float np = totalCount * p;       // 期望  n * p
        // ReSharper disable once InconsistentNaming  
        var np1_p = totalCount * p * (1 - p);                                           // 方差   n * p * (1 - p)
        float sd3 = Math.Max(Math.Min(limitIterCount >> 2, np), MathF.Sqrt(np1_p) * 3); // 3 * 标准差，再做一些约束
        
        // 映射到新的区间里面去随机
        int lowerBounds = Math.Max(0, (int) MathF.Floor(np - sd3));
        int upperBounds = Math.Min(totalCount, (int) MathF.Ceiling(np + sd3));
        int space = upperBounds - lowerBounds + 1;
        int randomCount = lowerBounds;
        int newP = (int) MathF.Floor((np - lowerBounds) / space * 1000); // 新区间的概率
        // 随机数量
        for (int i = lowerBounds; i < upperBounds; i++)
        {
            int rand = random.Next(0, 1000);
            if (rand < newP)
            {
                randomCount++;
            }
        }

        return randomCount;
    }
}