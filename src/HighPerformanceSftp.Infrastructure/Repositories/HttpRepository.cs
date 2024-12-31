using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

public sealed class HttpRepository : IFileRepository
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly ILogger _logger;

    public TransferProtocol Protocol => TransferProtocol.HTTPS;

    public HttpRepository(string baseUrl, string username, string password, ILogger logger)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 100,
            EnableMultipleHttp2Connections = true
        };

        _client = new HttpClient(handler);

        // Configura autenticação básica
        var authToken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
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
