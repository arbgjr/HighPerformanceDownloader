namespace HighPerformanceSftp.Domain.Configuration;

public sealed class DiagnosticConfig
{
    public bool EnableRealTimeMonitoring { get; set; } = true;
    public int MonitoringIntervalMs { get; set; } = 1000;
    public bool SaveDetailedReport { get; set; } = true;
    public string ReportPath { get; set; } = "diagnostic_report.json";

    public PerformanceThresholds Thresholds { get; set; } = new();
    public MetricsConfig Metrics { get; set; } = new();
}
