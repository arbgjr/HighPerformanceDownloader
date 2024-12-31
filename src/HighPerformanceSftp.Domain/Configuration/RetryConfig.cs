using System.Collections.Generic;

namespace HighPerformanceSftp.Domain.Configuration;

public sealed class RetryConfig
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int MaxDelayMs { get; set; } = 30000;
    public bool UseExponentialBackoff { get; set; } = true;
    public List<string> RetryableExceptions { get; set; } = new()
    {
        "System.IO.IOException",
        "System.Net.Sockets.SocketException",
        "Renci.SshNet.Common.SshOperationTimeoutException"
    };
}
