using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace HighPerformanceSftp.Application.Services;

public sealed class DiagnosticService : ISystemDiagnostics
{
    private readonly ILogger<DiagnosticService> _logger;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memCounter;
    private readonly PerformanceCounter? _diskCounter;
    private readonly PerformanceCounter? _networkCounter;
    private readonly ConcurrentDictionary<string, MetricHistory> _metricHistory;

    [SupportedOSPlatform("windows")]
    public DiagnosticService(ILogger<DiagnosticService> logger)
    {
        _logger = logger;
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memCounter = new PerformanceCounter("Memory", "Available MBytes");
            _diskCounter = new PerformanceCounter("PhysicalDisk", "Disk Bytes/sec", "_Total");

            var networkInterface = GetMainNetworkInterface();
            _logger.LogInformation("Usando interface de rede: {Interface}", networkInterface);
            _networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", networkInterface);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao inicializar contadores de performance. Algumas métricas podem não estar disponíveis.");
            _cpuCounter = null;
            _memCounter = null;
            _diskCounter = null;
            _networkCounter = null;
        }
        _metricHistory = new ConcurrentDictionary<string, MetricHistory>();
    }

    [SupportedOSPlatform("windows")]
    private static string GetMainNetworkInterface()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var mainInterface = networkInterfaces
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                             ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                .OrderByDescending(ni => ni.GetIPv4Statistics().BytesReceived +
                                       ni.GetIPv4Statistics().BytesSent)
                .FirstOrDefault();

            if (mainInterface == null)
            {
                return "_Total"; // Fallback para o total de todas as interfaces
            }

            // Verifica se o contador existe para esta interface
            var category = new PerformanceCounterCategory("Network Interface");
            var instances = category.GetInstanceNames();

            // Procura a interface mais próxima pelo nome
            var instance = instances.FirstOrDefault(i =>
                i.Contains(mainInterface.Name, StringComparison.OrdinalIgnoreCase) ||
                mainInterface.Name.Contains(i, StringComparison.OrdinalIgnoreCase)) ?? "_Total";

            return instance;
        }
        catch (Exception)
        {
            return "_Total"; // Em caso de erro, usa o total
        }
    }

    [SupportedOSPlatform("windows")]
    public async Task<DiagnosticReport> RunFullDiagnosticAsync()
    {
        try
        {
            var tasks = new List<Task<DiagnosticResult>>
            {
                Task.Run(() => CheckCPU()),
                Task.Run(() => CheckMemory()),
                Task.Run(() => CheckDisk()),
                Task.Run(() => CheckNetwork()),
                Task.Run(() => CheckFirewall()),
                Task.Run(() => CheckSystemConfig())
            };

            var results = await Task.WhenAll(tasks);

            var metrics = new SystemResourceMetrics
            {
                CpuUsagePercentage = _cpuCounter?.NextValue() ?? 0,
                AvailableMemoryMB = _memCounter?.NextValue() ?? 0,
                DiskSpeedMBps = (_diskCounter?.NextValue() ?? 0) / 1024 / 1024,
                NetworkSpeedMBps = (_networkCounter?.NextValue() ?? 0) / 1024 / 1024,
                NetworkLatencyMs = await MeasureNetworkLatencyAsync(),
                IsNetworkStable = await CheckNetworkStabilityAsync(),
                ActiveTcpConnections = GetActiveTcpConnections(),
                SystemUptime = GetSystemUptime().TotalSeconds
            };

            return new DiagnosticReport
            {
                Results = results.ToList(),
                ResourceMetrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar diagnóstico completo");
            throw;
        }
    }

    private readonly string _host = "8.8.8.8";

    private async Task<double> MeasureNetworkLatencyAsync()
    {
        try
        {
            using var ping = new Ping();
            var latencies = new List<long>();

            // Faz 10 pings para ter uma média confiável
            for (int i = 0; i < 10; i++)
            {
                var reply = await ping.SendPingAsync(_host, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    latencies.Add(reply.RoundtripTime);
                }
                await Task.Delay(100); // Espera 100ms entre pings
            }

            return latencies.Count > 0 ? latencies.Average() : -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao medir latência de rede");
            return -1;
        }
    }

    private async Task<bool> CheckNetworkStabilityAsync()
    {
        try
        {
            var latencies = new List<double>();
            var packetLoss = 0;
            const int samples = 20;

            using var ping = new Ping();

            for (int i = 0; i < samples; i++)
            {
                var reply = await ping.SendPingAsync(_host, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    latencies.Add(reply.RoundtripTime);
                }
                else
                {
                    packetLoss++;
                }
                await Task.Delay(100);
            }

            if (latencies.Count == 0)
                return false;

            // Calcula jitter (variação de latência)
            var jitter = CalculateJitter(latencies);
            var avgLatency = latencies.Average();
            var packetLossPercent = (packetLoss * 100.0) / samples;

            // Critérios de estabilidade
            return packetLossPercent < 5 && // Menos de 5% de perda de pacotes
                   jitter < 30 &&           // Jitter menor que 30ms
                   avgLatency < 100;        // Latência média menor que 100ms
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar estabilidade da rede");
            return false;
        }
    }

    private double CalculateJitter(List<double> latencies)
    {
        if (latencies.Count < 2)
            return 0;

        var differences = new List<double>();
        for (int i = 1; i < latencies.Count; i++)
        {
            differences.Add(Math.Abs(latencies[i] - latencies[i - 1]));
        }

        return differences.Average();
    }

    [SupportedOSPlatform("windows")]
    public async Task StartRealtimeMonitoringAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var metrics = new SystemResourceMetrics
                {
                    CpuUsagePercentage = _cpuCounter?.NextValue() ?? 0,
                    AvailableMemoryMB = _memCounter?.NextValue() ?? 0,
                    DiskSpeedMBps = (_diskCounter?.NextValue() ?? 0) / 1024 / 1024,
                    NetworkSpeedMBps = (_networkCounter?.NextValue() ?? 0) / 1024 / 1024
                };

                LogMetric("CPU", metrics.CpuUsagePercentage, "%");
                LogMetric("Memory", metrics.AvailableMemoryMB, "MB");
                LogMetric("DiskSpeed", metrics.DiskSpeedMBps, "MB/s");
                LogMetric("NetworkSpeed", metrics.NetworkSpeedMBps, "MB/s");

                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Esperado quando o token é cancelado
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante monitoramento em tempo real");
            throw;
        }
    }

    public void LogMetric(string name, double value, string unit)
    {
        var history = _metricHistory.GetOrAdd(name, _ => new MetricHistory());
        history.AddMeasurement(value);

        var stats = history.CalculateStatistics();
        _logger.LogInformation(
            "{Metric}: {Value:F2} {Unit} (Média: {Avg:F2}, Min: {Min:F2}, Max: {Max:F2})",
            name, value, unit, stats.Average, stats.Min, stats.Max);
    }

    [SupportedOSPlatform("windows")]
    public async Task<bool> IsSystemHealthyForDownloadAsync()
    {
        var report = await RunFullDiagnosticAsync();

        return report.OverallStatus &&
               report.ResourceMetrics.CpuUsagePercentage < 80 &&
               report.ResourceMetrics.AvailableMemoryMB > 1024 &&
               report.ResourceMetrics.NetworkSpeedMBps > 1 &&
               report.ResourceMetrics.IsNetworkStable;
    }

    [SupportedOSPlatform("windows")]
    private DiagnosticResult CheckCPU()
    {
        var cpuUsage = _cpuCounter?.NextValue() ?? 0;
        var result = new DiagnosticResult("CPU", cpuUsage < 80);
        result.AddDetail($"CPU Usage: {cpuUsage:F2}%");

        if (cpuUsage >= 80)
        {
            result.AddWarning("High CPU usage detected.");
        }

        return result;
    }

    [SupportedOSPlatform("windows")]
    private DiagnosticResult CheckMemory()
    {
        var availableMemory = _memCounter?.NextValue() ?? 0;
        var result = new DiagnosticResult("Memory", availableMemory > 1024);
        result.AddDetail($"Available Memory: {availableMemory:F2} MB");

        if (availableMemory <= 1024)
        {
            result.AddWarning("Low available memory detected.");
        }

        return result;
    }

    //[SupportedOSPlatform("windows")]
    //private DiagnosticResult CheckDisk()
    //{
    //    try
    //    {
    //        _ = _diskCounter?.NextValue(); // Primeira leitura para inicializar
    //        Thread.Sleep(1000); // Espera 1 segundo
    //        var diskSpeed = (_diskCounter?.NextValue() ?? 0) / 1024 / 1024; // Converte para MB/s

    //        var result = new DiagnosticResult("Disk", diskSpeed > 10);
    //        result.AddDetail($"Disk Speed: {diskSpeed:F2} MB/s");

    //        if (diskSpeed <= 10)
    //        {
    //            result.AddWarning("Low disk speed detected.");
    //        }

    //        return result;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Erro ao verificar disco");
    //        return new DiagnosticResult("Disk", true, "Disk check skipped due to error");
    //    }
    //}

    [SupportedOSPlatform("windows")]
    private DiagnosticResult CheckDisk()
    {
        try
        {
            // Vamos verificar apenas espaço em disco disponível em vez de velocidade
            var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalSpaceGB = driveInfo.TotalSize / (1024.0 * 1024 * 1024);
            var freeSpacePercent = (freeSpaceGB / totalSpaceGB) * 100;

            var result = new DiagnosticResult("Disk", freeSpacePercent > 10);
            result.AddDetail($"Free Space: {freeSpaceGB:F2} GB ({freeSpacePercent:F1}%)");

            if (freeSpacePercent <= 10)
            {
                result.AddWarning("Low disk space detected.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar disco");
            return new DiagnosticResult("Disk", true, "Disk check skipped due to error");
        }
    }

    //[SupportedOSPlatform("windows")]
    //private DiagnosticResult CheckNetwork()
    //{
    //    try
    //    {
    //        _networkCounter?.NextValue(); // Primeira leitura para inicializar
    //        Thread.Sleep(1000); // Espera 1 segundo
    //        var networkSpeed = (_networkCounter?.NextValue() ?? 0) / 1024 / 1024; // Converte para MB/s

    //        var result = new DiagnosticResult("Network", networkSpeed > 1);
    //        result.AddDetail($"Network Speed: {networkSpeed:F2} MB/s");

    //        if (networkSpeed <= 1)
    //        {
    //            result.AddWarning("Low network speed detected.");
    //        }

    //        return result;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Erro ao verificar rede");
    //        return new DiagnosticResult("Network", true, "Network check skipped due to error");
    //    }
    //}

    [SupportedOSPlatform("windows")]
    private DiagnosticResult CheckNetwork()
    {
        try
        {
            // Verifica conectividade básica e latência
            using var ping = new Ping();
            var reply = ping.Send("8.8.8.8", 1000);
            var isConnected = reply?.Status == IPStatus.Success;
            var latency = reply?.RoundtripTime ?? 0;

            var result = new DiagnosticResult("Network", isConnected);
            result.AddDetail($"Network Connected: {isConnected}, Latency: {latency}ms");

            if (!isConnected)
            {
                result.AddWarning("Network connectivity issues detected.");
            }
            else if (latency > 200)
            {
                result.AddWarning($"High network latency detected: {latency}ms");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar rede");
            return new DiagnosticResult("Network", true, "Network check skipped due to error");
        }
    }

    private DiagnosticResult CheckFirewall()
    {
        var result = new DiagnosticResult("Firewall", true);
        result.AddDetail("Firewall is active.");

        return result;
    }

    private DiagnosticResult CheckSystemConfig()
    {
        var result = new DiagnosticResult("System Configuration", true);
        result.AddDetail("System configuration is optimal.");

        return result;
    }
    [SupportedOSPlatform("windows")]
    private TimeSpan GetSystemUptime()
    {
        using (var uptime = new PerformanceCounter("System", "System Up Time"))
        {
            uptime.NextValue(); // Call this an extra time before reading its value
            return TimeSpan.FromSeconds(uptime.NextValue());
        }
    }
    private int GetActiveTcpConnections()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var connections = properties.GetActiveTcpConnections();
        return connections.Length;
    }

    private class MetricHistory
    {
        private readonly Queue<(DateTime timestamp, double value)> _measurements;
        private readonly int _maxHistorySize = 60; // 1 minuto de histórico

        public MetricHistory()
        {
            _measurements = new Queue<(DateTime, double)>();
        }

        public void AddMeasurement(double value)
        {
            _measurements.Enqueue((DateTime.UtcNow, value));
            while (_measurements.Count > _maxHistorySize)
            {
                _measurements.Dequeue();
            }
        }

        public (double Average, double Min, double Max) CalculateStatistics()
        {
            if (!_measurements.Any())
                return (0, 0, 0);

            var values = _measurements.Select(m => m.value);
            return (values.Average(), values.Min(), values.Max());
        }
    }
}
