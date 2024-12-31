using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

public sealed class HttpRepository : IFileRepository
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly string? _authToken;
    private readonly ILogger<HttpRepository> _logger;

    public TransferProtocol Protocol => TransferProtocol.HTTPS;

    public HttpRepository(
        string baseUrl,
        string? authToken = null,
        ILogger<HttpRepository>? logger = null)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _authToken = authToken;
        _logger = logger ?? NullLogger<HttpRepository>.Instance;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 100,
            EnableMultipleHttp2Connections = true
        };

        _client = new HttpClient(handler);
        if (!string.IsNullOrEmpty(_authToken))
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authToken);
        }
    }

    public Task ConnectAsync() => Task.CompletedTask; // Não precisa para HTTP

    public Task DisconnectAsync() => Task.CompletedTask; // Não precisa para HTTP

    public async Task<bool> FileExistsAsync(string path)
    {
        try
        {
            var response = await _client.SendAsync(new HttpRequestMessage(
                HttpMethod.Head,
                new Uri(_baseUrl + path)));

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência do arquivo: {Path}", path);
            throw;
        }
    }

    public async Task<long> GetFileSizeAsync(string path)
    {
        try
        {
            var response = await _client.SendAsync(new HttpRequestMessage(
                HttpMethod.Head,
                new Uri(_baseUrl + path)));

            response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength ?? -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter tamanho do arquivo: {Path}", path);
            throw;
        }
    }

    public async Task<Stream> OpenReadAsync(string path)
    {
        try
        {
            var response = await _client.GetAsync(
                new Uri(_baseUrl + path),
                HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir arquivo para leitura: {Path}", path);
            throw;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
