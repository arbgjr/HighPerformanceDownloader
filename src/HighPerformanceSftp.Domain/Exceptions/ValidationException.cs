using System.Collections.Generic;
using System.Text;
using System;

namespace HighPerformanceSftp.Domain.Exceptions;

[Serializable]
public class ValidationException : SftpDownloaderException
{
    public Dictionary<string, string[]> ValidationErrors { get; }

    public ValidationException(string message)
        : base(message) { }

    public ValidationException(
        string message,
        Dictionary<string, string[]> validationErrors,
        Exception? inner = null)
        : base(message, inner)
    {
        ValidationErrors = validationErrors;
    }

    protected ValidationException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }

    public override string ToString()
    {
        var details = new StringBuilder(base.ToString());
        details.AppendLine("Validation Errors:");

        foreach (var error in ValidationErrors)
        {
            details.AppendLine($"  {error.Key}:");
            foreach (var message in error.Value)
            {
                details.AppendLine($"    - {message}");
            }
        }

        return details.ToString();
    }
}
