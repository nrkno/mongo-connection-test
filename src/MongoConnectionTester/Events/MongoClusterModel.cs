using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace MongoConnectionTester.Events;

public class MongoClusterModel
{
    public ServerId PrimaryServerId { get; private set; }
    public Dictionary<ServerId, MongoServerModel> Servers { get; } = new();

    public int PrimaryConnectionCount => GetPrimary()?.UsableConnectionCount ?? 0;

    private MongoServerModel GetPrimary() => PrimaryServerId == null
        ? null
        : Servers.TryGetValue(PrimaryServerId, out var server) ? server : null;

    public MongoClusterModel Clone()
    {
        var clone = new MongoClusterModel
        {
            PrimaryServerId = PrimaryServerId
        };
        foreach (var server in Servers)
        {
            clone.Servers[server.Key] = server.Value.Clone();
        }

        return clone;
    }

    public bool AddConnection(ConnectionId connectionId)
    {
        var server = Servers.GetOrAdd(connectionId.ServerId, id => new MongoServerModel(id));
        return server.AddConnection(connectionId);
    }

    public bool RemoveConnection(ConnectionId connectionId)
    {
        return Servers.TryGetValue(connectionId.ServerId, out var server) &&
               server.RemoveConnection(connectionId);
    }

    public bool RemoveServer(ServerId serverId)
    {
        return Servers.Remove(serverId);
    }

    public bool SetPrimary(ServerId serverId)
    {
        if (PrimaryServerId != null && PrimaryServerId.Equals(serverId))
        {
            return false;
        }
        PrimaryServerId = serverId;
        return true;
    }

    public bool SetServerConnected(ServerId serverId, bool connected)
    {
        var server = Servers.GetOrAdd(serverId, id => new MongoServerModel(id));
        if (server.Connected == connected)
        {
            return false;
        }

        server.Connected = connected;
        return true;
    }

    public bool IsEqualTo(MongoClusterModel other)
    {
        return AreEqual(PrimaryServerId, other.PrimaryServerId) &&
               PrimaryConnectionCount == other.PrimaryConnectionCount &&
               AreEqual(Servers, other.Servers);
    }

    private static bool AreEqual(Dictionary<ServerId, MongoServerModel> first, Dictionary<ServerId, MongoServerModel> second)
    {
        foreach (var (serverId, server) in first)
        {
            if (!second.TryGetValue(serverId, out var otherServer))
            {
                return false;
            }

            if (server.Connected != otherServer.Connected)
            {
                return false;
            }

            if (server.Connections.Count != otherServer.Connections.Count)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEqual(ServerId first, ServerId second)
    {
        return first == null && second == null ||
               first != null && first.Equals(second);
    }
}