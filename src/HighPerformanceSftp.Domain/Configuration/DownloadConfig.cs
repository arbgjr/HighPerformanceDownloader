using HighPerformanceSftp.Domain.Models;

namespace HighPerformanceSftp.Domain.Configuration;

public sealed class DownloadConfig
{
    public int ChunkSize { get; init; } = 1024 * 1024;
    public int MaxParallelChunks { get; init; } = 4;
    public int MaxBytesPerSecond { get; init; } = 1_000_000;
    public int BufferSize { get; init; } = 81920;
    public bool UseDirectMemory { get; init; } = true;
    public int RetryCount { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 1000;
    public bool EnableCompression { get; init; } = false;
    public bool ValidateChecksum { get; init; } = true;
    public string? Protocol { get; set; } = "SFTP";
    public string? HttpBaseUrl { get; set; }
    public string? HttpAuthToken { get; set; }

    public static implicit operator DownloadConfiguration(DownloadConfig config) => new()
    {
        ChunkSize = config.ChunkSize,
        MaxParallelChunks = config.MaxParallelChunks,
        MaxBytesPerSecond = config.MaxBytesPerSecond,
        BufferSize = config.BufferSize,
        UseDirectMemory = config.UseDirectMemory,
        RetryCount = config.RetryCount,
        RetryDelayMs = config.RetryDelayMs,
        EnableCompression = config.EnableCompression,
        ValidateChecksum = config.ValidateChecksum
    };
}
