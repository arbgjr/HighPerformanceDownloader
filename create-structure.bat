@echo off
echo Criando estrutura do projeto High Performance SFTP Downloader...

:: Criar diretórios principais
mkdir src 2>nul
mkdir tests 2>nul

:: Criar solution
dotnet new sln -n HighPerformanceSftp

:: Criar projetos
cd src
dotnet new classlib -n HighPerformanceSftp.Domain -f net8.0
dotnet new classlib -n HighPerformanceSftp.Application -f net8.0
dotnet new classlib -n HighPerformanceSftp.Infrastructure -f net8.0
dotnet new console -n HighPerformanceSftp.Console -f net8.0

:: Criar projetos de teste
cd ..\tests
dotnet new xunit -n HighPerformanceSftp.UnitTests -f net8.0
dotnet new xunit -n HighPerformanceSftp.IntegrationTests -f net8.0

:: Adicionar projetos à solution
cd ..
dotnet sln add src/HighPerformanceSftp.Domain/HighPerformanceSftp.Domain.csproj
dotnet sln add src/HighPerformanceSftp.Application/HighPerformanceSftp.Application.csproj
dotnet sln add src/HighPerformanceSftp.Infrastructure/HighPerformanceSftp.Infrastructure.csproj
dotnet sln add src/HighPerformanceSftp.Console/HighPerformanceSftp.Console.csproj
dotnet sln add tests/HighPerformanceSftp.UnitTests/HighPerformanceSftp.UnitTests.csproj
dotnet sln add tests/HighPerformanceSftp.IntegrationTests/HighPerformanceSftp.IntegrationTests.csproj

:: Adicionar referências entre projetos
cd src\HighPerformanceSftp.Application
dotnet add reference ..\HighPerformanceSftp.Domain\HighPerformanceSftp.Domain.csproj

cd ..\HighPerformanceSftp.Infrastructure
dotnet add reference ..\HighPerformanceSftp.Domain\HighPerformanceSftp.Domain.csproj
dotnet add reference ..\HighPerformanceSftp.Application\HighPerformanceSftp.Application.csproj

cd ..\HighPerformanceSftp.Console
dotnet add reference ..\HighPerformanceSftp.Domain\HighPerformanceSftp.Domain.csproj
dotnet add reference ..\HighPerformanceSftp.Application\HighPerformanceSftp.Application.csproj
dotnet add reference ..\HighPerformanceSftp.Infrastructure\HighPerformanceSftp.Infrastructure.csproj

:: Adicionar referências aos projetos de teste
cd ..\..\tests\HighPerformanceSftp.UnitTests
dotnet add reference ..\..\src\HighPerformanceSftp.Domain\HighPerformanceSftp.Domain.csproj
dotnet add reference ..\..\src\HighPerformanceSftp.Application\HighPerformanceSftp.Application.csproj
dotnet add reference ..\..\src\HighPerformanceSftp.Infrastructure\HighPerformanceSftp.Infrastructure.csproj

cd ..\HighPerformanceSftp.IntegrationTests
dotnet add reference ..\..\src\HighPerformanceSftp.Domain\HighPerformanceSftp.Domain.csproj
dotnet add reference ..\..\src\HighPerformanceSftp.Application\HighPerformanceSftp.Application.csproj
dotnet add reference ..\..\src\HighPerformanceSftp.Infrastructure\HighPerformanceSftp.Infrastructure.csproj

:: Instalar pacotes NuGet necessários
cd ..\..\src\HighPerformanceSftp.Infrastructure
dotnet add package SSH.NET
dotnet add package System.Diagnostics.PerformanceCounter
dotnet add package System.Management
dotnet add package Microsoft.Extensions.Logging
dotnet add package System.IO.Pipelines

cd ..\HighPerformanceSftp.Console
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Logging.Console
dotnet add package Serilog
dotnet add package Serilog.Extensions.Logging
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File

cd ..\..\tests\HighPerformanceSftp.UnitTests
dotnet add package FluentAssertions
dotnet add package Moq
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package coverlet.collector
dotnet add package BenchmarkDotNet

cd ..\HighPerformanceSftp.IntegrationTests
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Testcontainers
dotnet add package WireMock.Net

:: Criar diretórios de estrutura
cd ..\..\src\HighPerformanceSftp.Domain
mkdir Interfaces
mkdir Models
mkdir Configuration
mkdir Exceptions

cd ..\HighPerformanceSftp.Application
mkdir Services
mkdir Observers
mkdir Metrics

cd ..\HighPerformanceSftp.Infrastructure
mkdir Repositories
mkdir Strategies
mkdir Memory
mkdir IO
mkdir Extensions

:: Voltar para o diretório raiz
cd ..\..

:: Criar arquivo .gitignore
echo # .NET Core > .gitignore
echo *.swp >> .gitignore
echo *.user >> .gitignore
echo bin/ >> .gitignore
echo obj/ >> .gitignore
echo .vs/ >> .gitignore
echo .vscode/ >> .gitignore
echo TestResults/ >> .gitignore
echo *.log >> .gitignore

:: Criar pasta para logs
mkdir logs 2>nul

echo.
echo Estrutura do projeto criada com sucesso!
echo.
echo Para abrir no Visual Studio, execute: HighPerformanceSftp.sln
pause
