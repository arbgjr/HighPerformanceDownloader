# High Performance SFTP Downloader

Uma solução de alta performance para download de arquivos grandes via SFTP, com limitação de velocidade e monitoramento em tempo real.

## Características

- Download paralelo em chunks
- Controle preciso de velocidade
- Monitoramento de performance em tempo real
- Diagnóstico completo do sistema
- Otimização de memória e I/O
- Suporte a arquivos grandes (200MB+)
- Performance otimizada para .NET 8

## Pré-requisitos

- .NET 8 SDK
- Visual Studio 2022 (v17.8+)
- Acesso ao servidor SFTP

## Estrutura da Solução

```
HighPerformanceSftp/
├── src/
│   ├── HighPerformanceSftp.Domain/        # Entidades e interfaces core
│   ├── HighPerformanceSftp.Application/   # Lógica de aplicação
│   ├── HighPerformanceSftp.Infrastructure/# Implementações e serviços
│   └── HighPerformanceSftp.Console/       # Aplicação console
└── tests/
    ├── HighPerformanceSftp.UnitTests/     # Testes unitários
    └── HighPerformanceSftp.IntegrationTests/ # Testes de integração
```

## Instalação

1. Clone o repositório
```bash
git clone https://github.com/seu-usuario/HighPerformanceSftp.git
```

2. Navegue até a pasta do projeto
```bash
cd HighPerformanceSftp
```

3. Restaure os pacotes NuGet
```bash
dotnet restore
```

4. Build do projeto
```bash
dotnet build
```

## Configuração

1. Abra o arquivo `appsettings.json` na pasta do projeto Console
2. Configure suas credenciais SFTP:
```json
{
  "SftpConfig": {
    "Host": "sftp.sinqia.com.br",
    "Username": "seu_usuario",
    "Password": "sua_senha"
  }
}
```

## Uso

1. Execute o projeto Console:
```bash
cd src/HighPerformanceSftp.Console
dotnet run
```

2. Para build otimizado de performance:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishAot=true
```

## Testes

### Executar Testes Unitários
```bash
dotnet test tests/HighPerformanceSftp.UnitTests
```

### Executar Testes de Integração
```bash
dotnet test tests/HighPerformanceSftp.IntegrationTests
```

### Executar Benchmarks
```bash
cd tests/HighPerformanceSftp.UnitTests
dotnet run -c Release --filter *Benchmark*
```

## Diagnóstico de Performance

O sistema inclui diagnóstico completo para identificar gargalos:

- CPU/Memória/Disco
- Rede e Latência
- Firewall e DNS
- Configurações do Sistema

Para visualizar diagnósticos:
1. Execute o download
2. Verifique logs em `diagnostic_report.json`
3. Monitore métricas em tempo real no console

## Otimização de Performance

O projeto utiliza várias técnicas para maximizar performance:

- Native AOT compilation
- Chunks paralelos
- Memory pooling
- I/O pipelines
- TCP optimizations
- Direct memory access

## Contribuição

1. Fork o projeto
2. Crie uma branch (`git checkout -b feature/sua-feature`)
3. Commit suas mudanças (`git commit -am 'Adicionando feature'`)
4. Push para a branch (`git push origin feature/sua-feature`)
5. Crie um Pull Request

## Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

## Suporte

Para reportar bugs ou solicitar features, por favor abra uma [issue](https://github.com/seu-usuario/HighPerformanceSftp/issues).
