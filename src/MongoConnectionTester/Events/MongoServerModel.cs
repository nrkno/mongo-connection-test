using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace MongoConnectionTester.Events;

public class MongoServerModel
{
    public ServerId ServerId { get; }
    
    public MongoServerModel(ServerId serverId)
    {
        ServerId = serverId;
    }
    
    public bool Connected { get; set; }
    public int UsableConnectionCount => Connected ? Connections.Count : 0;
    
    public Dictionary<ConnectionId, bool> Connections { get; } = new();

    public MongoServerModel Clone()
    {
        var clone = new MongoServerModel(ServerId)
        {
            Connected = Connected
        };
        foreach (var connection in Connections)
        {
            clone.Connections[connection.Key] = connection.Value;
        }

        return clone;
    }

    public bool AddConnection(ConnectionId connectionId)
    {
        return Connections.TryAdd(connectionId, true);
    }

    public bool RemoveConnection(ConnectionId connectionId)
    {
        return Connections.Remove(connectionId);
    }
}