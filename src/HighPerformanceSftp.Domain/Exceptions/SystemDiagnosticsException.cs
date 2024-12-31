using System.Collections.Generic;
using System.Text;
using System;

namespace HighPerformanceSftp.Domain.Exceptions;

[Serializable]
public class SystemDiagnosticsException : SftpDownloaderException
{
    public string Component { get; }
    public Dictionary<string, string> Metrics { get; }

    public SystemDiagnosticsException(string message)
        : base(message) { }

    public SystemDiagnosticsException(
        string message,
        string component,
        Dictionary<string, string> metrics,
        Exception? inner = null)
        : base(message, inner)
    {
        Component = component;
        Metrics = metrics;
    }

    protected SystemDiagnosticsException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }

    public override string ToString()
    {
        var details = new StringBuilder(base.ToString());
        details.AppendLine($"Component: {Component}");
        details.AppendLine("Metrics:");

        foreach (var metric in Metrics)
        {
            details.AppendLine($"  {metric.Key}: {metric.Value}");
        }

        return details.ToString();
    }
}
