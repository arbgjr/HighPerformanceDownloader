public enum TransferProtocol
{
    SFTP,
    HTTPS
}

public interface IFileRepository : IDisposable
{
    TransferProtocol Protocol { get; }
    Task<Stream> OpenReadAsync(string path);
    Task<long> GetFileSizeAsync(string path);
    Task<bool> FileExistsAsync(string path);
    Task ConnectAsync();
    Task DisconnectAsync();
}
