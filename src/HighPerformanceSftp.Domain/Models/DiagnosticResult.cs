using System.Collections.Generic;

namespace HighPerformanceSftp.Domain.Models;

public sealed record DiagnosticResult
{
    public string Component { get; init; }
    public bool Status { get; set; }
    public List<string> Details { get; } = new();
    public List<string> Warnings { get; } = new();

    public DiagnosticResult(string component, bool status, string? initialDetail = null)
    {
        Component = component;
        Status = status;
        if (initialDetail != null)
            Details.Add(initialDetail);
    }

    public void AddDetail(string detail) => Details.Add(detail);
    public void AddWarning(string warning) => Warnings.Add(warning);
}
