using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public static class QueryExtensions
{
    public static IQueryable<T> WithTrackingOptions<T>(this IQueryable<T> query, TrackingOptions trackingOptions)
        where T : class
    {
        if (trackingOptions == TrackingOptions.NoTracking)
            query = query.AsNoTracking();
        return query;
    }
}