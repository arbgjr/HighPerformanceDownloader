using System.Threading.Tasks;
using HighPerformanceSftp.Domain.Models;

namespace HighPerformanceSftp.Domain.Interfaces;

public interface IDownloadStrategy
{
    Task DownloadAsync(DownloadContext context);
}
