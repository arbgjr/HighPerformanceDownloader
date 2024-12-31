using System;

namespace HighPerformanceSftp.Domain.Exceptions;

[Serializable]
public class SftpDownloaderException : Exception
{
    public SftpDownloaderException() { }
    
    public SftpDownloaderException(string message)
        : base(message) { }
    
    public SftpDownloaderException(string message, Exception inner)
        : base(message, inner) { }
    
    protected SftpDownloaderException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
}
