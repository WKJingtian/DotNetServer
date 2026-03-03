namespace GameOutside.Util;

public static class CacheKey
{
    public static string MailItems(long playerId)
    {
        return $"mail:items:{playerId}";
    }

    public static string GiftCodeItems(long playerId)
    {
        return $"giftcode:items:{playerId}";
    }
}
