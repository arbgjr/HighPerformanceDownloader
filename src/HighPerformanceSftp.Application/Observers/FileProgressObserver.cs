using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System;
using System.IO;

namespace HighPerformanceSftp.Application.Observers;

public sealed class FileProgressObserver : IProgressObserver
{
    private readonly string _logPath;
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly object _lockObject = new();

    public FileProgressObserver(string logPath, ILogger logger)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Cria cabeçalho do log
        var header = "Timestamp,ElapsedTime,ProgressPercent,SpeedMbps,CompletedChunks,TotalChunks,BytesTransferred,TotalBytes";
        File.WriteAllText(_logPath, header + Environment.NewLine);
    }

    public void OnProgress(DownloadProgress progress)
    {
        try
        {
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
                          $"{_stopwatch.Elapsed:hh\\:mm\\:ss\\.fff}," +
                          $"{progress.ProgressPercentage:F2}," +
                          $"{progress.SpeedMbps:F2}," +
                          $"{progress.CompletedChunks}," +
                          $"{progress.TotalChunks}," +
                          $"{progress.BytesTransferred}," +
                          $"{progress.TotalBytes}";

            lock (_lockObject)
            {
                File.AppendAllText(_logPath, logEntry + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar progresso no arquivo");
        }
    }

    public void OnError(Exception ex)
    {
        try
        {
            var errorEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},ERROR,\"{ex.Message.Replace("\"", "\"\"")}\"";

            lock (_lockObject)
            {
                File.AppendAllText(_logPath, errorEntry + Environment.NewLine);
            }

            _logger.LogError(ex, "Download falhou");
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Erro ao registrar erro no arquivo");
        }
    }

    public void OnComplete(DownloadMetrics metrics)
    {
        try
        {
            var summaryEntry = new StringBuilder();
            summaryEntry.AppendLine("--- RESUMO DO DOWNLOAD ---");
            summaryEntry.AppendLine($"Concluído em: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            summaryEntry.AppendLine($"Tempo Total: {metrics.Elapsed:hh\\:mm\\:ss\\.fff}");
            summaryEntry.AppendLine($"Velocidade Média: {metrics.AverageSpeedMbps:F2} MB/s");
            summaryEntry.AppendLine($"Total Transferido: {metrics.TotalBytesTransferred:N0} bytes");
            summaryEntry.AppendLine($"Chunks Completados: {metrics.CompletedChunks}");
            summaryEntry.AppendLine($"Retentativas: {metrics.RetryCount}");
            summaryEntry.AppendLine($"Uso Máximo de Memória: {metrics.PeakMemoryUsageBytes:N0} bytes");
            summaryEntry.AppendLine($"Uso Médio de CPU: {metrics.AverageCpuUsage:F2}%");
            summaryEntry.AppendLine($"Latência de Rede: {metrics.NetworkLatencyMs:F2}ms");

            if (metrics.Checksum != null)
                summaryEntry.AppendLine($"Checksum: {metrics.Checksum}");

            lock (_lockObject)
            {
                File.AppendAllText(_logPath, summaryEntry.ToString());
            }

            _logger.LogInformation("Resumo do download salvo em: {LogPath}", _logPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar conclusão no arquivo");
        }
    }
}
