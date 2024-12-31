using System.Collections.Generic;

namespace HighPerformanceSftp.Domain.Configuration;

public sealed class SecurityConfig
{
    public bool EnableSslVerification { get; set; } = true;
    public string MinTlsVersion { get; set; } = "1.2";
    public List<string> AllowedCipherSuites { get; set; } = new()
    {
        "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
        "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256"
    };
    public bool ValidateServerCertificate { get; set; } = true;
    public string? TrustedCertificatePath { get; set; }
    public bool EnableKeyPinning { get; set; } = false;
    public List<string> PinnedPublicKeys { get; set; } = new();
}
