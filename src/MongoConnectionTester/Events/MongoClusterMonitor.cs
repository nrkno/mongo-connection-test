using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;

namespace MongoConnectionTester.Events;

internal sealed class MongoClusterMonitor : IEventSubscriber, IDisposable
{
    public event Action<int> PrimaryConnectionCountUpdated;
    public event Action<MongoClusterModel> ClusterUpdated;
    
    public MongoClusterModel Cluster => _cluster.Value;
    
    private bool _isDisposed;
    private readonly ILogger _logger;
    private readonly Locked<MongoClusterModel> _cluster = new(new MongoClusterModel());
    private readonly ConcurrentSlottedQueue<Action<MongoClusterModel>> _events = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _runLoop;
    private readonly ReflectionEventSubscriber _inner;

    public MongoClusterMonitor(ILogger logger)
    {
        _logger = logger;
        _inner = new ReflectionEventSubscriber(this, "Handle", BindingFlags.Instance | BindingFlags.NonPublic);
        _runLoop = HandleEventsAsync(_cts.Token);
    }
    
    public int GetPrimaryConnectionCount() => _cluster.Value.PrimaryConnectionCount;

    private async Task HandleEventsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event handler loop started");
        while (!cancellationToken.IsCancellationRequested)
        {
            var nextRun = Utc.Now.AddSeconds(5);
            
            try
            {
                var lastCluster = Cluster;
                var updatedCluster = lastCluster.Clone();
                if (_events.TrySwapQueue(out var events))
                {
                    while (events.TryDequeue(out var action) && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            action(updatedCluster);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError("Unhandled exception", e);
                        }
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    _cluster.Value = updatedCluster;
                    Diff(lastCluster, updatedCluster);    
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Unhandled exception in HandleEventsAsync", e);
            }
            
            try
            {
                var waitTime = nextRun - Utc.Now;
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);    
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
        _logger.LogInformation("Event handler loop stopped");
    }

    private void Diff(MongoClusterModel last, MongoClusterModel updated)
    {
        if (updated.IsEqualTo(last))
        {
            return;
        }
        var primaryConnectionCount = updated.PrimaryConnectionCount;
        if (primaryConnectionCount != last.PrimaryConnectionCount)
        {
            OnPrimaryConnectionCountUpdated(primaryConnectionCount);
        }

        if (updated.IsEqualTo(last))
        {
            return;
        }
        
        OnClusterUpdated(updated);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var builder = new StringBuilder("MongoConnections:").AppendLine();
            foreach (var server in updated.Servers.Values)
            {
                builder.AppendLine($"- {server.ServerId.EndPoint}: {server.UsableConnectionCount}");
            }
            _logger.LogDebug(builder.ToString());
        }
    }

    // ReSharper disable UnusedMember.Local
    private void Handle(ConnectionOpenedEvent e)
    {
        _events.Enqueue(cluster =>
        {
            if (!cluster.AddConnection(e.ConnectionId))
            {
                return;
            }
            _logger.LogDebug($"Connection opened: {e.ConnectionId}");
        });
    }

    private void Handle(ConnectionClosedEvent e)
    {
        _events.Enqueue(cluster =>
        {
            if (cluster.RemoveConnection(e.ConnectionId))
            {
                _logger.LogDebug($"Connection closed: {e.ConnectionId}");
            }
        });
    }

    private void Handle(ConnectionFailedEvent e)
    {
        _events.Enqueue(cluster =>
        {
            if (cluster.RemoveConnection(e.ConnectionId))
            {
                _logger.LogDebug($"Connection failed: {e.ConnectionId}");
            }
        });
    }

    private void Handle(ClusterRemovedServerEvent e)
    {
        _events.Enqueue(cluster =>
        {
            if (cluster.RemoveServer(e.ServerId))
            {
                _logger.LogDebug($"Server removed: {e.ServerId}");
            }
        });
    }

    private void Handle(ClusterDescriptionChangedEvent e)
    {
        var oldPrimary = e.OldDescription.Servers.FirstOrDefault(s => s.Type is ServerType.ReplicaSetPrimary or ServerType.Standalone);
        var newPrimary = e.NewDescription.Servers.FirstOrDefault(s => s.Type is ServerType.ReplicaSetPrimary or ServerType.Standalone);
        
        if (newPrimary == null || newPrimary.ServerId.Equals(oldPrimary?.ServerId))
        {
            return;
        }
        
        _events.Enqueue(cluster =>
        {
            if (cluster.SetPrimary(newPrimary.ServerId))
            {
                _logger.LogDebug($"Primary changed: {oldPrimary?.ServerId.EndPoint} -> {newPrimary.ServerId.EndPoint}");
            }
        });
    }

    private void Handle(ServerDescriptionChangedEvent e)
    {
        if (e.OldDescription.Type != e.NewDescription.Type && e.NewDescription.Type == ServerType.ReplicaSetPrimary)
        {
            _events.Enqueue(cluster =>
            {
                if (cluster.SetPrimary(e.NewDescription.ServerId))
                {
                    _logger.LogDebug($"Setting primary: {e.ServerId.EndPoint}");
                }    
            });
        }
        
        _events.Enqueue(cluster =>
        {
            var connected = e.NewDescription.State == ServerState.Connected;
            if (cluster.SetServerConnected(e.ServerId, connected))
            {
                _logger.LogDebug($"Server state changed: {e.ServerId.EndPoint}: {e.OldDescription.State} -> {e.NewDescription.State}");
            }
        });
    }

    private void OnClusterUpdated(MongoClusterModel cluster)
    {
        try
        {
            ClusterUpdated?.Invoke(cluster);
        }
        catch (Exception e)
        {
            _logger.LogError("Unhandled exception in ClusterUpdated", e);
        }
    }
    
    private void OnPrimaryConnectionCountUpdated(int connectionCount)
    {
        try
        {
            PrimaryConnectionCountUpdated?.Invoke(connectionCount);
        }
        catch(Exception e)
        {
            _logger.LogError("Unhandled exception in PrimaryConnectionCountUpdated", e);
        }
    }

    public bool TryGetEventHandler<TEvent>(out Action<TEvent> handler) => _inner.TryGetEventHandler(out handler);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}