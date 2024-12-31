using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;

namespace HighPerformanceSftp.Console;

public sealed class CompositeProgressObserver : IProgressObserver
{
    private readonly IEnumerable<IProgressObserver> _observers;

    public CompositeProgressObserver(IEnumerable<IProgressObserver> observers)
    {
        _observers = observers ?? throw new ArgumentNullException(nameof(observers));
    }

    public void OnStartDownload()
    {
        foreach (var observer in _observers)
        {
            observer.OnStartDownload();
        }
    }

    public void OnProgress(DownloadProgress progress)
    {
        foreach (var observer in _observers)
        {
            observer.OnProgress(progress);
        }
    }

    public void OnError(Exception ex)
    {
        foreach (var observer in _observers)
        {
            observer.OnError(ex);
        }
    }

    public void OnComplete(DownloadMetrics metrics)
    {
        foreach (var observer in _observers)
        {
            observer.OnComplete(metrics);
        }
    }
}
