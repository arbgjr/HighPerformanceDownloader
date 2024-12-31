using System.Text;
using System;

namespace HighPerformanceSftp.Domain.Exceptions;

[Serializable]
public class DownloadException : SftpDownloaderException
{
    public string? RemotePath { get; }
    public string? LocalPath { get; }
    public long? BytesTransferred { get; }
    public int? ChunkIndex { get; }
    public int RetryCount { get; }

    public DownloadException(string message)
        : base(message) { }

    public DownloadException(
        string message,
        string remotePath,
        string localPath,
        long bytesTransferred,
        int retryCount = 0,
        int? chunkIndex = null,
        Exception? inner = null)
        : base(message, inner)
    {
        RemotePath = remotePath;
        LocalPath = localPath;
        BytesTransferred = bytesTransferred;
        ChunkIndex = chunkIndex;
        RetryCount = retryCount;
    }

    protected DownloadException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }

    public override string ToString()
    {
        var details = new StringBuilder(base.ToString());

        if (RemotePath != null)
            details.AppendLine($"RemotePath: {RemotePath}");

        if (LocalPath != null)
            details.AppendLine($"LocalPath: {LocalPath}");

        if (BytesTransferred.HasValue)
            details.AppendLine($"BytesTransferred: {BytesTransferred:N0}");

        if (ChunkIndex.HasValue)
            details.AppendLine($"ChunkIndex: {ChunkIndex}");

        if (RetryCount > 0)
            details.AppendLine($"RetryCount: {RetryCount}");

        return details.ToString();
    }
}
