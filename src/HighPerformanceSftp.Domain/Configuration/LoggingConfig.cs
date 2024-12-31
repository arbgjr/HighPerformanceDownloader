namespace HighPerformanceSftp.Domain.Configuration;

public sealed class LoggingConfig
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/sftp-download-.log";
    public bool EnableStructuredLogging { get; set; } = true;
    public int RetainedFileCountLimit { get; set; } = 31;
    public long FileSizeLimitBytes { get; set; } = 1073741824; // 1GB
    public bool CompressLogFiles { get; set; } = true;
}
