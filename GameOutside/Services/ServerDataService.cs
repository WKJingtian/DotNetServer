using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;

namespace GameOutside.Services;

public class ServerDataService(BuildingGameDB dbCtx, IServerDataRepository serverDataRepository)
{
    public async Task<int> GetServerDataValue(string key, int defaultValue = 0)
    {
        var serverData = await dbCtx.WithDefaultRetry(_ => serverDataRepository.GetServerData(key));
        return serverData?.Value ?? defaultValue;
    }

    public async Task<ServerData> GetOrAddServerData(string key, int defaultValue)
    {
        var serverData = await dbCtx.WithDefaultRetry(_ => serverDataRepository.GetServerData(key));
        serverData ??= serverDataRepository.AddServerData(key, defaultValue);
        return serverData;
    }
}