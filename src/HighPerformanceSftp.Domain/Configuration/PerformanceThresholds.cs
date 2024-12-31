namespace HighPerformanceSftp.Domain.Configuration;

public sealed class PerformanceThresholds
{
    public double CpuWarningPercent { get; set; } = 80;
    public int MemoryWarningMb { get; set; } = 1024;
    public double DiskSpeedWarningMbps { get; set; } = 50;
    public double NetworkLatencyWarningMs { get; set; } = 100;
    public double NetworkBandwidthWarningMbps { get; set; } = 1;
    public int MaxTcpConnections { get; set; } = 100;
    public double MaxDiskUsagePercent { get; set; } = 90;
    public int MinFreeMemoryMb { get; set; } = 512;
}
