using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using System;
using System.Linq;

namespace HighPerformanceSftp.Domain.Models;

public sealed record DiagnosticReport
{
    public List<DiagnosticResult> Results { get; init; } = new();
    public bool OverallStatus => Results.All(r => r.Status);
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public SystemResourceMetrics ResourceMetrics { get; init; } = new();

    public string GenerateTextReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Relatório de Diagnóstico - {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("=====================================");

        foreach (var result in Results)
        {
            sb.AppendLine($"\n[{result.Component}] - {(result.Status ? "OK" : "PROBLEMA")}");
            foreach (var detail in result.Details)
            {
                sb.AppendLine($"  - {detail}");
            }
            foreach (var warning in result.Warnings)
            {
                sb.AppendLine($"  ! {warning}");
            }
        }

        sb.AppendLine($"\nRecursos do Sistema:");
        sb.AppendLine($"  CPU: {ResourceMetrics.CpuUsagePercentage:F1}%");
        sb.AppendLine($"  Memória Disponível: {ResourceMetrics.AvailableMemoryMB:F0} MB");
        sb.AppendLine($"  Disco: {ResourceMetrics.DiskSpeedMBps:F1} MB/s");
        sb.AppendLine($"  Rede: {ResourceMetrics.NetworkSpeedMBps:F1} MB/s");

        sb.AppendLine($"\nStatus Geral: {(OverallStatus ? "SISTEMA OK" : "PROBLEMAS DETECTADOS")}");
        return sb.ToString();
    }

    public string GenerateJsonReport()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
