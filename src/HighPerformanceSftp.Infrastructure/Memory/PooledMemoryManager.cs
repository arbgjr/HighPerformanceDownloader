using HighPerformanceSftp.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;

namespace HighPerformanceSftp.Infrastructure.Memory;

public sealed class PooledMemoryManager : IMemoryManager
{
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Create(
        maxArrayLength: 32 * 1024 * 1024, // Permitir arrays de até 32MB
        maxArraysPerBucket: 100);          // Manter mais arrays em pool
    private readonly ConcurrentDictionary<Memory<byte>, byte[]> _rentedMemory;
    private readonly ILogger<PooledMemoryManager> _logger;
    private long _totalAllocated;
    private bool _disposed;

    private static void OptimizeRuntime()
    {
        ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.SetMinThreads(
            workerThreads * 2,        // Dobra número de worker threads
            completionPortThreads * 4  // Quadruplica completion ports
        );
    }

    public PooledMemoryManager(ILogger<PooledMemoryManager>? logger = null)
    {
        // Usar um pool customizado com tamanhos maiores de buffer
        _arrayPool = ArrayPool<byte>.Create(maxArrayLength: 100 * 1024 * 1024, maxArraysPerBucket: 50);
        _rentedMemory = new ConcurrentDictionary<Memory<byte>, byte[]>();
        _logger = logger ?? NullLogger<PooledMemoryManager>.Instance;
    }

    public Memory<byte> Rent(int size)
    {
        ThrowIfDisposed();

        try
        {
            // Arredondar para cima para o próximo múltiplo de 4KB
            var alignedSize = (size + 4095) & ~4095;

            // Alugar do pool
            var array = _arrayPool.Rent(alignedSize);
            var memory = new Memory<byte>(array, 0, size);

            if (!_rentedMemory.TryAdd(memory, array))
            {
                _arrayPool.Return(array);
                throw new InvalidOperationException("Falha ao registrar memória alugada");
            }

            Interlocked.Add(ref _totalAllocated, size);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    "Memória alugada: {Size:N0} bytes (alinhado: {AlignedSize:N0}). Total alocado: {Total:N0} bytes",
                    size,
                    alignedSize,
                    _totalAllocated);
            }

            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alugar memória de {Size:N0} bytes", size);
            throw;
        }
    }

    public void Return(Memory<byte> memory)
    {
        if (_disposed)
            return;

        try
        {
            if (_rentedMemory.TryRemove(memory, out var array))
            {
                // Limpa dados sensíveis antes de devolver
                if (array.Length > 1024 * 1024) // Só limpa arrays grandes
                {
                    Array.Clear(array);
                }

                _arrayPool.Return(array, clearArray: false); // Já limpamos se necessário
                Interlocked.Add(ref _totalAllocated, -memory.Length);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(
                        "Memória devolvida: {Size:N0} bytes. Total alocado: {Total:N0} bytes",
                        memory.Length,
                        _totalAllocated);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao devolver memória de {Size:N0} bytes", memory.Length);
        }
    }

    public long GetTotalAllocatedBytes() => Interlocked.Read(ref _totalAllocated);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PooledMemoryManager));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            foreach (var (_, array) in _rentedMemory)
            {
                // Limpa dados sensíveis
                Array.Clear(array);
                _arrayPool.Return(array);
            }

            _rentedMemory.Clear();

            _logger.LogInformation(
                "MemoryManager disposed. Total alocado final: {Total:N0} bytes",
                _totalAllocated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante dispose do MemoryManager");
        }
    }
}
