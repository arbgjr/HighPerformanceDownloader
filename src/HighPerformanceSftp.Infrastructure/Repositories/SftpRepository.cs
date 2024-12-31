using System.Net.Sockets;
using System.Text;
using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Infrastructure.Observers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Renci.SshNet;
using Renci.SshNet.Security;
using Renci.SshNet.Security.Cryptography;
using Renci.SshNet.Security.Cryptography.Ciphers;

namespace HighPerformanceSftp.Infrastructure.Repositories;

public sealed class SftpRepository : ISftpRepository
{
    private readonly string _host;
    private readonly string _username;
    private readonly string _password;
    private readonly int _port;
    private readonly ILogger<SftpRepository> _logger;
    private SftpClient? _client;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private readonly IConnectionObserver _connectionObserver;

    public SftpRepository(
        string host,
        string username,
        string password,
        int port = 22,
        ILogger<SftpRepository>? logger = null,
        IConnectionObserver? connectionObserver = null)

    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _port = port;
        _logger = logger ?? NullLogger<SftpRepository>.Instance;
        _connectionObserver = connectionObserver ?? new DefaultConnectionObserver();
    }

    public async Task ConnectAsync()
    {
        ThrowIfDisposed();

        await _connectionLock.WaitAsync();
        try
        {
            if (_client?.IsConnected == true)
                return;

            if (string.IsNullOrWhiteSpace(_host))
                throw new InvalidOperationException("Host não pode ser nulo ou vazio");
            if (_port <= 0 || _port > 65535)
                throw new InvalidOperationException("Porta inválida");
            if (string.IsNullOrWhiteSpace(_username))
                throw new InvalidOperationException("Username não pode ser nulo ou vazio");
            if (string.IsNullOrWhiteSpace(_password))
                throw new InvalidOperationException("Password não pode ser nulo ou vazio");

            _connectionObserver?.OnSearchingHost(_host);
            _logger.LogDebug("Iniciando conexão SFTP com {Host}:{Port}", _host, _port);

            var connectionInfo = new ConnectionInfo(_host, _port, _username,
                new PasswordAuthenticationMethod(_username, _password))
            {
                RetryAttempts = 3,
                Timeout = TimeSpan.FromMinutes(2),
                MaxSessions = 10,
                Encoding = Encoding.UTF8
            };

            _client = new SftpClient(connectionInfo);
            _client.KeepAliveInterval = TimeSpan.FromSeconds(5);
            _client.BufferSize = 1024 * 1024 * 4;
            _client.ConnectionInfo.Timeout = TimeSpan.FromMinutes(2);
            _client.OperationTimeout = TimeSpan.FromMinutes(5);

            ConfigureSocket();

            _connectionObserver?.OnConnectingHost();

            int retryCount = 0;
            const int maxRetries = 3;
            var baseDelay = TimeSpan.FromSeconds(5);

            while (retryCount < maxRetries)
            {
                try
                {
                    var connectTask = Task.Run(() =>
                    {
                        _connectionObserver?.OnAuthenticating(_username);
                        _client.Connect();
                    });

                    // Timeout maior para primeira tentativa
                    var timeoutDelay = retryCount == 0 ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(30);

                    if (await Task.WhenAny(connectTask, Task.Delay(timeoutDelay)) == connectTask)
                    {
                        await connectTask; // Propaga exceção se houver
                        break; // Conexão bem sucedida
                    }
                    else
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            _logger.LogWarning("Timeout na tentativa {Attempt} de {MaxRetries}. Aguardando {Delay}s antes de tentar novamente...",
                                retryCount, maxRetries, baseDelay.TotalSeconds * retryCount);

                            await Task.Delay(TimeSpan.FromSeconds(baseDelay.TotalSeconds * retryCount));
                            continue;
                        }
                        throw new TimeoutException($"Timeout ao conectar com {_host}:{_port} após {maxRetries} tentativas");
                    }
                }
                catch (Exception ex) when (ex is not TimeoutException)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        _logger.LogWarning(ex, "Erro na tentativa {Attempt} de {MaxRetries}. Aguardando {Delay}s antes de tentar novamente...",
                            retryCount, maxRetries, baseDelay.TotalSeconds * retryCount);

                        await Task.Delay(TimeSpan.FromSeconds(baseDelay.TotalSeconds * retryCount));
                        continue;
                    }
                    throw;
                }
            }

            _connectionObserver?.OnConnected(_host);
            _logger.LogInformation("Conectado com sucesso ao servidor SFTP {Host}:{Port}", _host, _port);
        }
        catch (Exception ex)
        {
            _connectionObserver?.OnConnectionError(ex);
            _logger.LogError(ex, "Erro ao conectar com servidor SFTP {Host}:{Port}", _host, _port);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<Stream> OpenReadAsync(string path)
    {
        ThrowIfDisposed();

        await EnsureConnectedAsync();

        try
        {
            _logger.LogDebug("Abrindo arquivo para leitura: {Path}", path);

            var stream = _client!.OpenRead(path);

            // Wrapper para garantir que o stream seja não-blocante
            return new BufferedStream(stream, 1024 * 1024 * 4); // 2MB buffer
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir arquivo para leitura: {Path}", path);
            throw;
        }
    }

    public async Task<long> GetFileSizeAsync(string path)
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync();

        try
        {
            _logger.LogDebug("Verificando existência do arquivo: {Path}", path);
            if (!await FileExistsAsync(path))
            {
                throw new FileNotFoundException($"Arquivo não encontrado no caminho: {path}");
            }

            _logger.LogDebug("Obtendo tamanho do arquivo: {Path}", path);
            var fileInfo = _client!.Get(path);
            return fileInfo.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter tamanho do arquivo: {Path}", path);
            throw;
        }
    }

    public async Task ListRemoteDirectoryStructureAsync(string startPath = "/")
    {
        ThrowIfDisposed();
        await EnsureConnectedAsync();

        try
        {
            _logger.LogInformation("Listando estrutura de diretórios a partir de: {Path}", startPath);
            var files = _client!.ListDirectory(startPath);

            foreach (var file in files)
            {
                _logger.LogInformation("{Type}: {Path}",
                    file.IsDirectory ? "DIR " : "FILE",
                    file.FullName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar diretório: {Path}", startPath);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        ThrowIfDisposed();

        await EnsureConnectedAsync();

        try
        {
            return _client!.Exists(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência do arquivo: {Path}", path);
            throw;
        }
    }

    bool ISftpRepository.FileExists(string path)
    {
        return FileExistsAsync(path).GetAwaiter().GetResult();
    }

    public async Task<IEnumerable<string>> ListDirectoryAsync(string path)
    {
        ThrowIfDisposed();

        await EnsureConnectedAsync();

        try
        {
            _logger.LogDebug("Listando diretório: {Path}", path);

            return _client!.ListDirectory(path)
                .Where(file => !file.IsDirectory)
                .Select(file => file.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar diretório: {Path}", path);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed || _client == null)
            return;

        await _connectionLock.WaitAsync();
        try
        {
            if (_client.IsConnected)
            {
                _logger.LogDebug("Desconectando do servidor SFTP {Host}:{Port}", _host, _port);
                _client.Disconnect();
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_client?.IsConnected != true)
        {
            await ConnectAsync();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SftpRepository));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
            _client?.Dispose();
            _connectionLock.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer dispose do SftpRepository");
        }
    }

    private void ConfigureSocket()
    {
        if (_client == null)
            return;

        // Configurações de timeout e keep-alive
        _client.KeepAliveInterval = TimeSpan.FromSeconds(15);  // Mais agressivo
        _client.ConnectionInfo.Timeout = TimeSpan.FromMinutes(2);
        _client.OperationTimeout = TimeSpan.FromMinutes(5);

        // Aumenta o buffer para melhor performance
        _client.BufferSize = 1024 * 1024 * 2; // 2MB buffer

        // Logging das configurações
        _logger.LogInformation(
            "Configurações SFTP - Buffer: {BufferSize}MB, KeepAlive: {KeepAlive}s, Timeout: {Timeout}min, OperationTimeout: {OpTimeout}min",
            _client.BufferSize / (1024 * 1024),
            _client.KeepAliveInterval.TotalSeconds,
            _client.ConnectionInfo.Timeout.TotalMinutes,
            _client.OperationTimeout.TotalMinutes);
    }
}
