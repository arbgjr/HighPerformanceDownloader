using System;
using HighPerformanceSftp.Domain.Models;

namespace HighPerformanceSftp.Domain.Interfaces;

public interface IProgressObserver
{
    void OnStartDownload();
    void OnProgress(DownloadProgress progress);
    void OnError(Exception ex);
    void OnComplete(DownloadMetrics metrics);
}
