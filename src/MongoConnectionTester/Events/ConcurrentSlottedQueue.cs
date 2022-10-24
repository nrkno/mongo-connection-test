namespace MongoConnectionTester.Events;

internal class ConcurrentSlottedQueue<T>
{
    private readonly object _lock = new();
    private Queue<T> _queue;

    private int _index;
    private readonly Queue<T>[] _queuePool = { new(), new() };

    public ConcurrentSlottedQueue()
    {
        _queue = _queuePool[_index];
    }

    public void Enqueue(T item)
    {
        lock (_lock)
        {
            _queue.Enqueue(item);
        }
    }

    /// <summary>
    /// Swaps queues if current queue is not empty.
    /// </summary>
    /// <param name="queue">current queue (before swap)</param>
    /// <returns>true if swapped, false if not</returns>
    public bool TrySwapQueue(out Queue<T> queue)
    {
        queue = default;
        var dequeued = false;
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                queue = _queue;
                _queue = SwapQueue();
                dequeued = true;
            }
        }
        return dequeued;
    }

    private Queue<T> SwapQueue()
    {
        _index++;
        if (_index >= _queuePool.Length)
        {
            _index = 0;
        }

        return _queuePool[_index];
    }
}