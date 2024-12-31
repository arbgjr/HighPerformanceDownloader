using System;
using System.Threading;
using System.Threading.Tasks;
using HighPerformanceSftp.Application.Metrics;
using HighPerformanceSftp.Application.Observers;
using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HighPerformanceSftp.Application.Services;

public sealed class DownloadService : IDisposable
{
    private readonly IDownloadStrategy _strategy;
    private readonly IMemoryManager _memoryManager;
    private readonly ISystemDiagnostics _diagnostics;
    private readonly ILogger<DownloadService> _logger;
    private bool _disposed;

    public DownloadService(
        IDownloadStrategy strategy,
        IMemoryManager memoryManager,
        ISystemDiagnostics diagnostics,
        ILogger<DownloadService> logger)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DownloadAsync(
        string remotePath,
        string localPath,
        DownloadConfiguration? config = null,
        IProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation("Iniciando download de {RemotePath} para {LocalPath}", remotePath, localPath);

        try
        {
            // Verifica saúde do sistema
            if (!await _diagnostics.IsSystemHealthyForDownloadAsync())
            {
                var report = await _diagnostics.RunFullDiagnosticAsync();
                _logger.LogWarning("Sistema não está em condições ideais para download:\n{Report}", 
                    report.GenerateTextReport());
                
                throw new InvalidOperationException("Sistema não está em condições ideais para download. Verifique o relatório de diagnóstico.");
            }

            // Configuração padrão se não fornecida
            config ??= new DownloadConfiguration();

            // Observer padrão se não fornecido
            progressObserver ??= new ConsoleProgressObserver(_logger);

            // Coletor de métricas
            var metricsCollector = new DefaultMetricsCollector();

            // Contexto do download
            var context = new DownloadContext
            {
                RemotePath = remotePath,
                LocalPath = localPath,
                Config = config,
                ProgressObserver = progressObserver,
                MetricsCollector = metricsCollector,
                CancellationToken = cancellationToken,
                Diagnostics = _diagnostics
            };

            // Inicia monitoramento em tempo real
            using var diagnosticCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var monitoringTask = _diagnostics.StartRealtimeMonitoringAsync(diagnosticCts.Token);

            try
            {
                // Executa o download
                await _strategy.DownloadAsync(context);

                // Obtém métricas finais
                var metrics = metricsCollector.GetCurrentMetrics();
                progressObserver.OnComplete(metrics);

                _logger.LogInformation(
                    "Download concluído. Velocidade média: {Speed:F2} MB/s, Tempo total: {Time}",
                    metrics.AverageSpeedMbps,
                    metrics.Elapsed);
            }
            finally
            {
                // Para o monitoramento
                diagnosticCts.Cancel();
                try
                {
                    await monitoringTask;
                }
                catch (OperationCanceledException)
                {
                    // Esperado ao cancelar o monitoramento
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o download de {RemotePath}", remotePath);
            progressObserver?.OnError(ex);
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DownloadService));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _memoryManager.Dispose();
            _disposed = true;
        }
    }
}
