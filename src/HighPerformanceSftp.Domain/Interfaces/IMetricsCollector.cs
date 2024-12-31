using HighPerformanceSftp.Domain.Models;

namespace HighPerformanceSftp.Domain.Interfaces;

public interface IMetricsCollector
{
    void RecordBytesTransferred(long bytes);
    void RecordChunkCompleted();
    DownloadMetrics GetCurrentMetrics();
}
