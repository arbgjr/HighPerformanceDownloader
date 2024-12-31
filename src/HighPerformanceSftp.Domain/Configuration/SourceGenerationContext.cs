using System.Text.Json.Serialization;
using HighPerformanceSftp.Domain.Configuration;

namespace HighPerformanceSftp.Domain.Configuration;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SftpConfig))]
[JsonSerializable(typeof(DownloadConfig))]
[JsonSerializable(typeof(DiagnosticConfig))]
[JsonSerializable(typeof(LoggingConfig))]
[JsonSerializable(typeof(SecurityConfig))]
[JsonSerializable(typeof(RetryConfig))]
[JsonSerializable(typeof(MetricsConfig))]
[JsonSerializable(typeof(PerformanceThresholds))]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
