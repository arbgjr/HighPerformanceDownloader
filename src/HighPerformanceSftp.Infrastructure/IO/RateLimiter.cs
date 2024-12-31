using System.Collections.Concurrent;
using System.Diagnostics;

namespace HighPerformanceSftp.Infrastructure.IO;

public sealed class RateLimiter
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _bytesProcessedInCurrentSecond;
    private DateTime _currentSecondStart = DateTime.UtcNow;
    private readonly ConcurrentQueue<(DateTime timestamp, long bytes)> _throughputHistory;
    private readonly object _syncLock = new();
    private const int HistorySeconds = 5;

    public RateLimiter()
    {
        _throughputHistory = new ConcurrentQueue<(DateTime, long)>();
    }

    public async Task<T> ThrottleAsync<T>(
        Func<Task<T>> action,
        int maxBytesPerSecond)
    {
        while (true)
        {
            lock (_syncLock)
            {
                var now = DateTime.UtcNow;
                if ((now - _currentSecondStart).TotalSeconds >= 1)
                {
                    _bytesProcessedInCurrentSecond = 0;
                    _currentSecondStart = now;
                }

                if (_bytesProcessedInCurrentSecond < maxBytesPerSecond)
                {
                    _bytesProcessedInCurrentSecond += maxBytesPerSecond / 10;
                    var result = action().Result;

                    // Registra throughput
                    _throughputHistory.Enqueue((DateTime.UtcNow, maxBytesPerSecond / 10));

                    // Limpa histórico antigo
                    while (_throughputHistory.TryPeek(out var oldest) &&
                           (DateTime.UtcNow - oldest.timestamp).TotalSeconds > HistorySeconds)
                    {
                        _throughputHistory.TryDequeue(out _);
                    }

                    return result;
                }
            }

            await Task.Delay(100);
        }
    }

    public double GetCurrentSpeedMbps()
    {
        var recentBytes = _throughputHistory.Sum(x => x.bytes);
        return recentBytes / HistorySeconds / 1024.0 / 1024.0;
    }
}
