using System.Text.Json.Serialization;

namespace HighPerformanceSftp.Domain.Configuration;

public sealed class SftpConfig
{
    public required string Host { get; set; }
    public int Port { get; set; } = 22;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string RemoteBasePath { get; set; }
    public required string LocalBasePath { get; set; }
    public bool ValidateHostKey { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;
    public int OperationTimeout { get; set; } = 300;
    public int BufferSize { get; set; } = 1024 * 1024; // 1MB
    public int KeepAliveInterval { get; set; } = 60;
}
