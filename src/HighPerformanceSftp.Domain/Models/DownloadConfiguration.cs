using HighPerformanceSftp.Domain.Configuration;

namespace HighPerformanceSftp.Domain.Models;

public sealed record DownloadConfiguration
{
    public int ChunkSize { get; init; } = 1024 * 1024; // 1MB default
    public int MaxParallelChunks { get; init; } = 4;
    public int MaxBytesPerSecond { get; init; } = 1_000_000; // 1MB/s default
    public int BufferSize { get; init; } = 81920;
    public bool UseDirectMemory { get; init; } = true;
    public int RetryCount { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 1000;
    public bool EnableCompression { get; init; } = false;
    public bool ValidateChecksum { get; init; } = true;

    public static implicit operator DownloadConfiguration(DownloadConfig v) => throw new NotImplementedException();
}
