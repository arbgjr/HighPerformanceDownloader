using System.Collections.Generic;

namespace HighPerformanceSftp.Domain.Configuration;

public sealed class MetricsConfig
{
    public bool EnablePerformanceCounters { get; set; } = true;
    public bool TrackMemoryAllocation { get; set; } = true;
    public bool TrackGarbageCollection { get; set; } = true;
    public bool TrackThreadPool { get; set; } = true;
    public int MetricsRetentionHours { get; set; } = 24;
    public string MetricsPath { get; set; } = "metrics";
    public List<string> ExtraCounters { get; set; } = new();
}
