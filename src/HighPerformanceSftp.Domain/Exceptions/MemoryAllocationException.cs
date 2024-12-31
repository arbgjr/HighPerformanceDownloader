using System;

namespace HighPerformanceSftp.Domain.Exceptions;

[Serializable]
public class MemoryAllocationException : SftpDownloaderException
{
    public long RequestedBytes { get; }
    public long AvailableMemory { get; }

    public MemoryAllocationException(string message)
        : base(message) { }

    public MemoryAllocationException(
        string message,
        long requestedBytes,
        long availableMemory,
        Exception? inner = null)
        : base(message, inner)
    {
        RequestedBytes = requestedBytes;
        AvailableMemory = availableMemory;
    }

    protected MemoryAllocationException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
}
