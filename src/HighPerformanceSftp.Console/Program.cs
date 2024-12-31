using HighPerformanceSftp.Application.Services;
using HighPerformanceSftp.Domain.Configuration;
using HighPerformanceSftp.Domain.Interfaces;
using HighPerformanceSftp.Infrastructure.Memory;
using HighPerformanceSftp.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Text.Json;

namespace HighPerformanceSftp.Console;

public static class Program
{
    [RequiresUnreferencedCode("This method requires unreferenced code.")]
    [RequiresDynamicCode("This method requires dynamic code.")]
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Limpa a tela
            System.Console.Clear();
            OptimizeRuntime();

            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                System.Console.WriteLine($"Arquivo não encontrado: {configPath}");
                return 1;
            }

            // Tenta ler e validar o JSON primeiro
            try
            {
                var jsonContent = File.ReadAllText(configPath);
                using var jsonDocument = JsonDocument.Parse(jsonContent);

                // Verifica se as seções principais existem
                var root = jsonDocument.RootElement;
                if (!root.TryGetProperty("SftpConfig", out _))
                {
                    throw new InvalidOperationException("Seção 'SftpConfig' não encontrada no JSON");
                }
            }
            catch (Exception jsonEx)
            {
                System.Console.WriteLine($"Erro ao validar JSON: {jsonEx.Message}");
                System.Console.WriteLine($"Tipo do erro: {jsonEx.GetType().Name}");
                if (jsonEx.InnerException != null)
                {
                    System.Console.WriteLine($"Erro interno: {jsonEx.InnerException.Message}");
                }
                throw;
            }

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            try
            {
                var config = configBuilder.Build();

                // Configuração do JsonSerializerOptions para o Get<T>
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var sftpConfig = config.GetSection("SftpConfig").Get<SftpConfig>();
                if (sftpConfig == null)
                {
                    throw new InvalidOperationException("Não foi possível ler a seção SftpConfig");
                }
            }
            catch (Exception configEx)
            {
                System.Console.WriteLine($"Erro ao ler configuração: {configEx.Message}");
                System.Console.WriteLine($"Pasta atual: {AppContext.BaseDirectory}");
                System.Console.WriteLine($"Arquivo: {configPath}");
                throw;
            }

            // Configura host
            using var host = CreateHostBuilder(args).Build();
            // Configura logging inicial
            SetupInitialLogging();
            // Executa a aplicação
            await host.Services.GetRequiredService<App>().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Erro fatal: {ex.Message}");
            System.Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                System.Console.WriteLine("Erro interno:");
                System.Console.WriteLine(ex.InnerException.Message);
            }
            return 1;
        }
    }

    private static void OptimizeRuntime()
    {
        // Força GC Server
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Aumenta threads disponíveis
        ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.SetMinThreads(workerThreads * 2, completionPortThreads * 2);

        // Otimiza alocação de memória
        GC.TryStartNoGCRegion(1024 * 1024 * 100); // 100MB
    }

    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Get<T>()")]
    [RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Get<T>()")]
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory)
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/sftp-download-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .ConfigureServices((hostContext, services) =>
            {
                // Registra os observers
                services.AddSingleton<ConsoleProgressObserver>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<ConsoleProgressObserver>>();
                    return new ConsoleProgressObserver(logger);
                });
                services.AddSingleton<IProgressObserver>(sp => sp.GetRequiredService<ConsoleProgressObserver>());
                services.AddSingleton<IConnectionObserver>(sp => sp.GetRequiredService<ConsoleProgressObserver>());

                // Configurações
                services.Configure<SftpConfig>(hostContext.Configuration.GetSection("SftpConfig"));
                services.Configure<DownloadConfig>(hostContext.Configuration.GetSection("DownloadConfig"));

                // Serviços core
                services.AddSingleton<IMemoryManager, PooledMemoryManager>();
                services.AddSingleton<IFileRepository>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<SftpConfig>>().Value;
                    var logger = sp.GetRequiredService<ILogger<SftpRepository>>();
                    var observer = sp.GetRequiredService<ConsoleProgressObserver>();
                    return new SftpRepository(
                        config.Host,
                        config.Username,
                        config.Password,
                        config.Port,
                        logger,
                        observer);
                });

                // Estratégias
                services.AddSingleton<IDownloadStrategy, ParallelChunkDownloadStrategy>();

                // Serviços de aplicação
                services.AddSingleton<ISystemDiagnostics, DiagnosticService>();
                services.AddSingleton<App>();

                // Certifique-se que o logging está configurado
                services.AddLogging(builder =>
                {
                    builder.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    builder.AddConsole();
                });
            });

    private static void SetupInitialLogging()
    {
        var initialLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        Log.Logger = initialLogger;
    }
}
