using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System;
using System.Linq;

namespace HighPerformanceSftp.Application.Metrics;

public sealed class DefaultMetricsCollector : IMetricsCollector
{
    private long _totalBytesTransferred;
    private int _completedChunks;
    private int _retryCount;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly ConcurrentQueue<(DateTime timestamp, long bytes)> _throughputHistory = new();
    private readonly int _historyWindowSeconds = 5;

    public void RecordBytesTransferred(long bytes)
    {
        Interlocked.Add(ref _totalBytesTransferred, bytes);
        _throughputHistory.Enqueue((DateTime.UtcNow, bytes));

        // Limpa histórico antigo
        var cutoff = DateTime.UtcNow.AddSeconds(-_historyWindowSeconds);
        while (_throughputHistory.TryPeek(out var oldest) && oldest.timestamp < cutoff)
        {
            _throughputHistory.TryDequeue(out _);
        }
    }

    public void RecordChunkCompleted()
    {
        Interlocked.Increment(ref _completedChunks);
    }

    public void RecordRetry()
    {
        Interlocked.Increment(ref _retryCount);
    }

    public DownloadMetrics GetCurrentMetrics()
    {
        var elapsed = _stopwatch.Elapsed;
        var totalBytes = Interlocked.Read(ref _totalBytesTransferred);
        var chunks = _completedChunks;
        var retries = _retryCount;

        // Calcula velocidade média dos últimos segundos
        var recentBytes = _throughputHistory.Sum(x => x.bytes);
        var speedMbps = recentBytes / _historyWindowSeconds / 1024.0 / 1024.0;

        return new DownloadMetrics
        {
            TotalBytesTransferred = totalBytes,
            CompletedChunks = chunks,
            Elapsed = elapsed,
            AverageSpeedMbps = speedMbps,
            RetryCount = retries,
            StartTime = DateTime.Now.AddTicks(-elapsed.Ticks),
            EndTime = DateTime.Now,
            PeakMemoryUsageBytes = Process.GetCurrentProcess().PeakWorkingSet64,
            AverageCpuUsage = GetCpuUsage(),
            NetworkLatencyMs = GetNetworkLatency(),
            IsSuccess = true
        };
    }

    private static double GetCpuUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                return cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }
        return 0;
    }
    private static double GetNetworkLatency()
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("8.8.8.8", 1000);
            return reply?.RoundtripTime ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
