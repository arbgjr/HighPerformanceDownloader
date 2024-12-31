using System;

namespace HighPerformanceSftp.Domain.Models;

public sealed record DownloadMetrics
{
    public long TotalBytesTransferred { get; init; }
    public int CompletedChunks { get; init; }
    public TimeSpan Elapsed { get; init; }
    public double AverageSpeedMbps { get; init; }
    public int RetryCount { get; init; }
    public long PeakMemoryUsageBytes { get; init; }
    public double AverageCpuUsage { get; init; }
    public double NetworkLatencyMs { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public string? Checksum { get; init; }
}
