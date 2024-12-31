namespace HighPerformanceSftp.Domain.Interfaces;

public interface IConnectionObserver
{
    void OnSearchingHost(string host);
    void OnConnectingHost();
    void OnAuthenticating(string username);
    void OnConnected(string host);
    void OnConnectionError(Exception ex);
}
