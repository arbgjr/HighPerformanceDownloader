using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;

namespace HighPerformanceSftp.Domain.Interfaces;

public interface ISftpRepository : IDisposable
{
    Task<Stream> OpenReadAsync(string path);
    Task<long> GetFileSizeAsync(string path);
    bool FileExists(string path);
    Task<IEnumerable<string>> ListDirectoryAsync(string path);
    Task ConnectAsync();
    Task DisconnectAsync();
}
