using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

public sealed class ConsoleProgressObserver : IProgressObserver, IConnectionObserver
{
    private readonly ILogger _logger;
    private int _lastPercentage = -1;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private const int ProgressBarWidth = 50;

    public ConsoleProgressObserver(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void OnProgress(DownloadProgress progress)
    {
        var currentPercentage = (int)progress.ProgressPercentage;

        if (currentPercentage == _lastPercentage)
            return;

        _lastPercentage = currentPercentage;

        // Limpa linha anterior
        Console.Write("\r" + new string(' ', Console.BufferWidth - 1) + "\r");

        // Escreve progresso com cores
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(GenerateProgressBar(currentPercentage));

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($" {currentPercentage}% | ");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{progress.SpeedMbps:F2} MB/s | ");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"Chunks: {progress.CompletedChunks}/{progress.TotalChunks} | ");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"ETA: {progress.EstimatedTimeRemaining:hh\\:mm\\:ss}");

        Console.ResetColor();

        // Log detalhado a cada 10%
        if (currentPercentage % 10 == 0)
        {
            _logger.LogInformation(
                "Download progresso: {Progress}% @ {Speed} MB/s, {Completed}/{Total} chunks, ETA: {ETA}",
                currentPercentage,
                progress.SpeedMbps,
                progress.CompletedChunks,
                progress.TotalChunks,
                progress.EstimatedTimeRemaining.ToString(@"hh\:mm\:ss"));
        }
    }

    public void OnError(Exception ex)
    {
        _stopwatch.Stop();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Erro durante download: {ex.Message}");
        Console.ResetColor();
        _logger.LogError(ex, "Download falhou após {Elapsed}", _stopwatch.Elapsed);
    }

    public void OnComplete(DownloadMetrics metrics)
    {
        _stopwatch.Stop();
        Console.WriteLine();

        var totalSeconds = _stopwatch.Elapsed.TotalSeconds;
        var totalMB = metrics.TotalBytesTransferred / (1024.0 * 1024.0);
        var averageMBps = totalMB / totalSeconds;

        var separator = new string('=', Console.BufferWidth - 1);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(separator);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("DOWNLOAD CONCLUÍDO COM SUCESSO!");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"Tempo Total: {metrics.Elapsed:hh\\:mm\\:ss\\.fff}");
        Console.WriteLine($"Velocidade Média: {metrics.AverageSpeedMbps:F2} MB/s");
        Console.WriteLine($"Total Transferido: {FormatBytes(metrics.TotalBytesTransferred)}");
        Console.WriteLine($"Chunks Completados: {metrics.CompletedChunks}");

        Console.WriteLine($"Taxa Média Real: {averageMBps:F2} MB/s");

        if (metrics.RetryCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Retentativas: {metrics.RetryCount}");
        }

        if (metrics.Checksum != null)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"Checksum: {metrics.Checksum}");
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(separator);
        Console.ResetColor();

        _logger.LogInformation(
            "Download concluído. Tempo: {Elapsed}, Taxa Média: {Speed:F2} MB/s, Total: {Total}, Chunks: {Chunks}",
            metrics.Elapsed,
            averageMBps,
            FormatBytes(metrics.TotalBytesTransferred),
            metrics.CompletedChunks);
    }

    private static string GenerateProgressBar(int percentage)
    {
        var completed = (int)((percentage / 100.0) * ProgressBarWidth);
        var remaining = ProgressBarWidth - completed;

        var bar = new StringBuilder("[");
        bar.Append(new string('█', completed)); // Usando caractere de bloco cheio

        if (remaining > 0)
        {
            bar.Append('▓'); // Usando caractere de bloco parcial
            bar.Append(new string('░', remaining - 1)); // Usando caractere de bloco vazio
        }

        bar.Append(']');

        return bar.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblBytes = bytes;

        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblBytes = bytes / 1024.0;
        }

        return $"{dblBytes:N2} {suffix[i]}";
    }

    public void OnSearchingHost(string host)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Procurando pelo Host {host}...");
        Console.ResetColor();
    }

    public void OnConnectingHost()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Conectando no Host...");
        Console.ResetColor();
    }

    public void OnAuthenticating(string username)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Autenticando...");
        Console.WriteLine($"Usando nome de usuário \"{username}\".");
        Console.WriteLine();
        Console.ResetColor();
    }

    public void OnConnected(string host)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Conectado com sucesso ao servidor {host}!");
        Console.WriteLine();
        Console.ResetColor();
    }

    public void OnConnectionError(Exception ex)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Erro de conexão: {ex.Message}");
        Console.WriteLine();
        Console.ResetColor();
    }
}
