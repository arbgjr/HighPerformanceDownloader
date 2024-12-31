using System;

namespace HighPerformanceSftp.Domain.Exceptions;

[Serializable]
public class ConnectionException : SftpDownloaderException
{
    public string Host { get; }
    public int Port { get; }
    public int AttemptCount { get; }
    public TimeSpan ElapsedTime { get; }

    public ConnectionException(string message)
        : base(message) { }

    public ConnectionException(
        string message,
        string host,
        int port,
        int attemptCount,
        TimeSpan elapsedTime,
        Exception? inner = null)
        : base(message, inner)
    {
        Host = host;
        Port = port;
        AttemptCount = attemptCount;
        ElapsedTime = elapsedTime;
    }

    protected ConnectionException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
}
