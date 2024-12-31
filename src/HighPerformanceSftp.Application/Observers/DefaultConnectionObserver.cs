using HighPerformanceSftp.Domain.Interfaces;

namespace HighPerformanceSftp.Infrastructure.Observers;

public sealed class DefaultConnectionObserver : IConnectionObserver
{
    public void OnSearchingHost(string host) { }
    public void OnConnectingHost() { }
    public void OnAuthenticating(string username) { }
    public void OnConnected(string host) { }
    public void OnConnectionError(Exception ex) { }
}
