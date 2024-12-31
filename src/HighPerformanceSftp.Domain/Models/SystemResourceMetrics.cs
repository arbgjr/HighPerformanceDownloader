namespace HighPerformanceSftp.Domain.Models;

public sealed record SystemResourceMetrics
{
    public double CpuUsagePercentage { get; init; }
    public double AvailableMemoryMB { get; init; }
    public double DiskSpeedMBps { get; init; }
    public double NetworkSpeedMBps { get; init; }
    public double NetworkLatencyMs { get; init; }
    public bool IsNetworkStable { get; init; }
    public int ActiveTcpConnections { get; init; }
    public double SystemUptime { get; init; }
}
