public static class BitsUtils
{
    public static bool GetNthBits(this int value, int index)
    {
        return ((value >> index) & 1) != 0;
    }

    public static int SetNthBits(this int value, int index, bool bit)
    {
        if (bit)
            value |= (1 << index);
        else
            value &= ~(1 << index);
        return value;
    }

    public static int NumOfOne(this int value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    public static bool GetNthBits(this long value, int index)
    {
        return ((value >> index) & 1L) != 0;
    }

    public static long SetNthBits(this long value, int index, bool bit)
    {
        if (bit)
            value |= (1L << index);
        else
            value &= ~(1L << index);
        return value;
    }
    
    public static bool GetNthBits(this List<long> value, int index)
    {
        return value.Count > index / 64 &&
               value[index / 64].GetNthBits(index % 64);
    }

    public static long SetNthBits(this List<long> value, int index, bool bit)
    {
        while (value.Count <= index / 64)
            value.Add(0);
        value[index / 64] =
            value[index / 64].SetNthBits(index % 64, bit);
        return value[index / 64];
    }
}