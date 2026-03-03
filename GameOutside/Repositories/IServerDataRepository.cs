using GameOutside.DBContext;
using GameOutside.Models;

namespace GameOutside.Repositories;

public interface IServerDataRepository
{
    public ValueTask<ServerData?> GetServerData(string key);
    public ServerData AddServerData(string key, int value);
}

public class ServerDataRepository(BuildingGameDB dbCtx) : IServerDataRepository
{
    public ValueTask<ServerData?> GetServerData(string key)
    {
        return dbCtx.ServerDataset.FindAsync(key);
    }

    public ServerData AddServerData(string key, int value)
    {
        var data = new ServerData() {Id = key, Value = value};
        dbCtx.ServerDataset.Add(data);
        return data;
    }
}