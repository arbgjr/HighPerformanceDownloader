using HighPerformanceSftp.Domain.Configuration;
using HighPerformanceSftp.Domain.Exceptions;
using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using HighPerformanceSftp.Infrastructure.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.IO.Pipelines;

public sealed class ParallelChunkDownloadStrategy : IDownloadStrategy
{
    private readonly ISftpRepository _repository;
    private readonly IMemoryManager _memoryManager;
    private readonly ILogger<ParallelChunkDownloadStrategy> _logger;
    private readonly DownloadConfig _config;
    private readonly ConcurrentDictionary<int, Memory<byte>> _completedChunks;
    private int _activeTasks;
    private readonly Pipe _pipe;  // Adicione isto

    public ParallelChunkDownloadStrategy(
        ISftpRepository repository,
        IMemoryManager memoryManager,
        ILogger<ParallelChunkDownloadStrategy> logger,
        IOptions<DownloadConfig> downloadConfig)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = downloadConfig?.Value ?? throw new ArgumentNullException(nameof(downloadConfig));
        _config = new DownloadConfig
        {
            ChunkSize = Math.Max(1024 * 1024 * 4, config.ChunkSize), // Mínimo 4MB
            MaxParallelChunks = Math.Max(8, config.MaxParallelChunks), // Mínimo 8 paralelos
            MaxBytesPerSecond = config.MaxBytesPerSecond,
            BufferSize = config.BufferSize,
            UseDirectMemory = config.UseDirectMemory,
            RetryCount = config.RetryCount,
            RetryDelayMs = config.RetryDelayMs,
            EnableCompression = config.EnableCompression,
            ValidateChecksum = config.ValidateChecksum
        };

        _completedChunks = new ConcurrentDictionary<int, Memory<byte>>();
        _pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: 1024 * 1024 * 4,
            resumeWriterThreshold: 1024 * 1024 * 2,
            minimumSegmentSize: 4096));
    }

    public async Task DownloadAsync(DownloadContext context)
    {
        try
        {
            var fileSize = await _repository.GetFileSizeAsync(context.RemotePath);
            var chunkSize = Math.Max(1024 * 1024 * 4, context.Config.ChunkSize); // Mínimo 4MB por chunk
            var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);

            var maxParallelChunks = Math.Max(8, context.Config.MaxParallelChunks); // Mínimo 8 chunks paralelos

            _logger.LogInformation(
                "Iniciando download paralelo: {TotalChunks} chunks de {ChunkSize:N0} bytes",
                totalChunks,
                context.Config.ChunkSize);

            using var semaphore = new SemaphoreSlim(maxParallelChunks);
            var tasks = new List<Task>();
            var failedChunks = new ConcurrentBag<int>();
            var retryCount = new ConcurrentDictionary<int, int>();

            for (var i = 0; i < totalChunks; i++)
            {
                await semaphore.WaitAsync(context.CancellationToken);
                var chunkIndex = i;
                Interlocked.Increment(ref _activeTasks);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await DownloadChunkAsync(
                            chunkIndex,
                            context,
                            fileSize,
                            failedChunks,
                            retryCount,
                            semaphore);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _activeTasks);
                        semaphore.Release();
                    }
                }, context.CancellationToken);

                tasks.Add(task);
            }

            // Aguarda todos os chunks ou falha
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha no download. {FailedChunks} chunks falharam",
                    failedChunks.Count);
                throw;
            }

            // Se algum chunk falhou, falha todo o download
            if (failedChunks.Any())
            {
                throw new DownloadException(
                    $"Download falhou. {failedChunks.Count} chunks não puderam ser baixados após todas as tentativas.");
            }

            // Combina os chunks e salva o arquivo
            await SaveCompleteFileAsync(context, totalChunks);
            // Garante que o OnComplete seja chamado
            var finalMetrics = context.MetricsCollector.GetCurrentMetrics();
            context.ProgressObserver.OnComplete(finalMetrics);
        }
        catch (Exception)
        {
            // Se falhar, ainda tenta chamar OnComplete com os dados que temos
            try
            {
                var metrics = context.MetricsCollector.GetCurrentMetrics();
                context.ProgressObserver.OnComplete(metrics);
            }
            catch
            {
                // Ignora erro ao tentar reportar métricas finais
            }
            throw;
        }
    }

    private async Task SaveCompleteFileAsync(DownloadContext context, int totalChunks)
    {
        _logger.LogInformation("Combinando {TotalChunks} chunks no arquivo final", totalChunks);

        // Garante que o diretório existe
        var directory = Path.GetDirectoryName(context.LocalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var outputFile = new FileStream(
            context.LocalPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            for (var i = 0; i < totalChunks; i++)
            {
                if (_completedChunks.TryRemove(i, out var chunk))
                {
                    try
                    {
                        await outputFile.WriteAsync(chunk, context.CancellationToken);
                    }
                    finally
                    {
                        _memoryManager.Return(chunk);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Chunk {i} não encontrado ao combinar arquivo");
                }
            }
        }

        _logger.LogInformation("Arquivo final salvo com sucesso: {Path}", context.LocalPath);
    }

    private async Task DownloadChunkAsync(
        int chunkIndex,
        DownloadContext context,
        long fileSize,
        ConcurrentBag<int> failedChunks,
        ConcurrentDictionary<int, int> retryCount,
        SemaphoreSlim semaphore)
    {
        var offset = (long)chunkIndex * context.Config.ChunkSize;
        var chunkSize = (int)Math.Min(context.Config.ChunkSize, fileSize - offset);
        var attempts = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (attempts < context.Config.RetryCount)
        {
            try
            {
                var memory = _memoryManager.Rent(chunkSize);
                try
                {
                    using var stream = await _repository.OpenReadAsync(context.RemotePath);
                    stream.Position = offset;

                    var totalBytesRead = 0;
                    const int bufferSize = 8 * 1024 * 1024; // 8MB por leitura
                    var buffer = new byte[bufferSize]; // Adicione esta linha

                    while (totalBytesRead < chunkSize)
                    {
                        var bytesToRead = Math.Min(bufferSize, chunkSize - totalBytesRead);
                        var bytesRead = await stream.ReadAsync(
                            memory.Slice(totalBytesRead, bytesToRead),
                            context.CancellationToken);

                        if (bytesRead == 0)
                            break;
                        totalBytesRead += bytesRead;

                        context.MetricsCollector.RecordBytesTransferred(bytesRead);
                    }

                    if (totalBytesRead > 0)
                    {
                        _completedChunks[chunkIndex] = memory[..totalBytesRead];
                        context.MetricsCollector.RecordChunkCompleted();

                        var progress = new DownloadProgress
                        {
                            BytesTransferred = _completedChunks.Values.Sum(chunk => chunk.Length),
                            TotalBytes = fileSize,
                            SpeedMbps = CalculateSpeed(totalBytesRead),
                            CompletedChunks = _completedChunks.Count,
                            TotalChunks = (int)Math.Ceiling((double)fileSize / context.Config.ChunkSize),
                            CurrentParallelDownloads = _activeTasks,
                            EstimatedTimeRemaining = CalculateETA(
                                _completedChunks.Values.Sum(chunk => chunk.Length),
                                fileSize,
                                CalculateSpeed(totalBytesRead))
                        };

                        context.ProgressObserver.OnProgress(progress);
                        return;
                    }

                    throw new IOException($"Chunk {chunkIndex}: Leitura retornou 0 bytes");
                }
                catch
                {
                    _memoryManager.Return(memory);
                    throw;
                }
            }
            catch (Exception ex)
            {
                attempts++;
                retryCount.AddOrUpdate(chunkIndex, 1, (_, count) => count + 1);

                if (attempts >= context.Config.RetryCount)
                {
                    _logger.LogError(
                        ex,
                        "Chunk {Index} falhou após {Attempts} tentativas",
                        chunkIndex,
                        attempts);
                    failedChunks.Add(chunkIndex);
                    throw;
                }

                _logger.LogWarning(
                    ex,
                    "Erro ao baixar chunk {Index}. Tentativa {Attempt}/{MaxRetries}",
                    chunkIndex,
                    attempts,
                    context.Config.RetryCount);

                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }
    }

    private TimeSpan CalculateETA(long bytesTransferred, long totalBytes, double speedMbps)
    {
        if (speedMbps <= 0)
            return TimeSpan.MaxValue;

        var remainingBytes = totalBytes - bytesTransferred;
        var remainingSeconds = remainingBytes / (speedMbps * 1024 * 1024);

        return TimeSpan.FromSeconds(remainingSeconds);
    }

    private double CalculateSpeed(long bytesTransferred)
    {
        const double megabyte = 1024 * 1024;
        return bytesTransferred / megabyte;
    }
}
