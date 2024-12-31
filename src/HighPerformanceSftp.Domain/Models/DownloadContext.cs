using HighPerformanceSftp.Domain.Interfaces;
using System.Collections.Generic;
using System.Threading;

namespace HighPerformanceSftp.Domain.Models;

public sealed record DownloadContext
{
    public required string RemotePath { get; init; }
    public required string LocalPath { get; init; }
    public required DownloadConfiguration Config { get; init; }
    public required IProgressObserver ProgressObserver { get; init; }
    public required IMetricsCollector MetricsCollector { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public ISystemDiagnostics? Diagnostics { get; init; }
    public IDictionary<string, object>? CustomParameters { get; init; }
}
