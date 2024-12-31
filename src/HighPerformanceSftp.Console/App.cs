using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using HighPerformanceSftp.Application.Metrics;
using HighPerformanceSftp.Application.Observers;
using HighPerformanceSftp.Domain.Configuration;
using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Domain.Models;
using HighPerformanceSftp.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HighPerformanceSftp.Console;

public sealed class App
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<App> _logger;
    private readonly ISftpRepository _sftpRepository;
    private readonly IDownloadStrategy _downloadStrategy;
    private readonly ISystemDiagnostics _diagnostics;
    private readonly IMemoryManager _memoryManager;

    public App(
        IConfiguration configuration,
        ILogger<App> logger,
        ISftpRepository sftpRepository,
        IDownloadStrategy downloadStrategy,
        ISystemDiagnostics diagnostics,
        IMemoryManager memoryManager)
    {
        _configuration = configuration;
        _logger = logger;
        _sftpRepository = sftpRepository;
        _downloadStrategy = downloadStrategy;
        _diagnostics = diagnostics;
        _memoryManager = memoryManager;
    }

    [RequiresUnreferencedCode("Este método pode não funcionar corretamente com o trimming de código.")]
    [RequiresDynamicCode("Este método pode não funcionar corretamente com o trimming de código.")]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando aplicação...");

        try
        {
            // Carrega configurações
            var sftpConfig = LoadConfiguration<SftpConfig>("SftpConfig");
            var downloadConfig = LoadConfiguration<DownloadConfiguration>("DownloadConfig");

            if (sftpConfig == null || downloadConfig == null)
            {
                throw new InvalidOperationException("Configurações inválidas ou ausentes");
            }

            // Executa diagnóstico inicial
            _logger.LogInformation("Executando diagnóstico inicial do sistema...");
            var diagnosticReport = await _diagnostics.RunFullDiagnosticAsync();

            if (!diagnosticReport.OverallStatus)
            {
                _logger.LogWarning("Sistema não está em condições ideais:");
                _logger.LogWarning(diagnosticReport.GenerateTextReport());

                System.Console.WriteLine("\nDeseja continuar mesmo assim? (S/N)");
                if (System.Console.ReadKey().Key != ConsoleKey.S)
                {
                    return;
                }
                System.Console.WriteLine();
            }

            if (_sftpRepository is SftpRepository sftp)
            {
                await sftp.ListRemoteDirectoryStructureAsync("/");
                // Pode adicionar mais níveis específicos se necessário
                // await sftp.ListRemoteDirectoryStructureAsync("/Clientes");
            }

            // Prepara observadores de progresso
            var consoleObserver = new ConsoleProgressObserver(_logger);
            var fileObserver = new FileProgressObserver("download_progress.csv", _logger);
            var multiObserver = new CompositeProgressObserver(new IProgressObserver[] { consoleObserver, fileObserver });

            // Configura e executa o download
            var context = new DownloadContext
            {
                RemotePath = sftpConfig.RemoteBasePath,
                LocalPath = sftpConfig.LocalBasePath,
                Config = downloadConfig,
                ProgressObserver = multiObserver,
                MetricsCollector = new DefaultMetricsCollector(),
                CancellationToken = cancellationToken,
                Diagnostics = _diagnostics
            };

            // Inicia monitoramento em tempo real
            using var diagnosticCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var monitoringTask = _diagnostics.StartRealtimeMonitoringAsync(diagnosticCts.Token);

            try
            {
                await _downloadStrategy.DownloadAsync(context);
            }
            finally
            {
                diagnosticCts.Cancel();
                try
                {
                    await monitoringTask;
                }
                catch (OperationCanceledException)
                {
                    // Esperado
                }
            }

            _logger.LogInformation("Download concluído com sucesso!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal durante execução");
            throw;
        }
    }

    [RequiresUnreferencedCode("Este método pode não funcionar corretamente com o trimming de código.")]
    [RequiresDynamicCode("Este método pode não funcionar corretamente com o trimming de código.")]
    private T? LoadConfiguration<T>(string sectionName) where T : class
    {
        try
        {
            var section = _configuration.GetSection(sectionName);
            return section.Get<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar configuração {SectionName}", sectionName);
            throw;
        }
    }
}
