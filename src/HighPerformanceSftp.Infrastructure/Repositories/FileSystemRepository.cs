using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace HighPerformanceSftp.Infrastructure.Repositories;

public sealed class FileSystemRepository
{
    private readonly ILogger<FileSystemRepository> _logger;

    public FileSystemRepository(ILogger<FileSystemRepository>? logger = null)
    {
        _logger = logger ?? NullLogger<FileSystemRepository>.Instance;
    }

    public async Task EnsureDirectoryExistsAsync(string path)
    {
        await Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return;

            try
            {
                if (!Directory.Exists(directory))
                {
                    _logger.LogDebug("Criando diretório: {Directory}", directory);
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar diretório: {Directory}", directory);
                throw;
            }
        });
    }

    public async Task<Stream> CreateFileAsync(string path)
    {
        try
        {
            await EnsureDirectoryExistsAsync(path);

            _logger.LogDebug("Criando arquivo: {Path}", path);
            return new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024, // 1MB buffer
                useAsync: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar arquivo: {Path}", path);
            throw;
        }
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public async Task<long> GetFileSizeAsync(string path)
    {
        try
        {
            return await Task.Run(() =>
            {
                var fileInfo = new FileInfo(path);
                return fileInfo.Length;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter tamanho do arquivo: {Path}", path);
            throw;
        }
    }

    public async Task DeleteFileAsync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Deletando arquivo: {Path}", path);
                await Task.Run(() => File.Delete(path));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar arquivo: {Path}", path);
            throw;
        }
    }

    public async Task<DateTime> GetLastModifiedTimeAsync(string path)
    {
        try
        {
            return await Task.Run(() =>
            {
                var fileInfo = new FileInfo(path);
                return fileInfo.LastWriteTimeUtc;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter data de modificação: {Path}", path);
            throw;
        }
    }
}
