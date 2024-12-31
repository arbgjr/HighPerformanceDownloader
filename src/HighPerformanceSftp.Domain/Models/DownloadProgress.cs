using System;

namespace HighPerformanceSftp.Domain.Models;

public sealed record DownloadProgress
{
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public double SpeedMbps { get; init; }
    public int CompletedChunks { get; init; }
    public int TotalChunks { get; init; }
    public double ProgressPercentage => (double)BytesTransferred / TotalBytes * 100;
    public TimeSpan EstimatedTimeRemaining { get; init; }
    public int CurrentParallelDownloads { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
