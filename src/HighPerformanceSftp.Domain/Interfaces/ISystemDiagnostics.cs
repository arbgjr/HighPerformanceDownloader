using System.Threading.Tasks;
using System.Threading;
using HighPerformanceSftp.Domain.Models;

namespace HighPerformanceSftp.Domain.Interfaces;

public interface ISystemDiagnostics
{
    Task<DiagnosticReport> RunFullDiagnosticAsync();
    Task StartRealtimeMonitoringAsync(CancellationToken token);
    void LogMetric(string name, double value, string unit);
    Task<bool> IsSystemHealthyForDownloadAsync();
}
