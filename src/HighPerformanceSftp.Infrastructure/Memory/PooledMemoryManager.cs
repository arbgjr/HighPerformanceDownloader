using HighPerformanceSftp.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;

namespace HighPerformanceSftp.Infrastructure.Memory;

public sealed class PooledMemoryManager : IMemoryManager
{
    private readonly MemoryPool<byte> _pool;
    private readonly ConcurrentDictionary<Memory<byte>, IMemoryOwner<byte>> _rentedMemory;
    private readonly ILogger<PooledMemoryManager> _logger;
    private long _totalAllocated;
    private bool _disposed;

    public PooledMemoryManager(ILogger<PooledMemoryManager>? logger = null)
    {
        _pool = MemoryPool<byte>.Shared;
        _rentedMemory = new ConcurrentDictionary<Memory<byte>, IMemoryOwner<byte>>();
        _logger = logger ?? NullLogger<PooledMemoryManager>.Instance;
    }

    public Memory<byte> Rent(int size)
    {
        ThrowIfDisposed();

        try
        {
            var owner = _pool.Rent(size);
            var memory = owner.Memory;

            if (!_rentedMemory.TryAdd(memory, owner))
            {
                owner.Dispose();
                throw new InvalidOperationException("Falha ao registrar memória alugada");
            }

            Interlocked.Add(ref _totalAllocated, size);

            _logger.LogTrace(
                "Memória alugada: {Size:N0} bytes. Total alocado: {Total:N0} bytes",
                size,
                _totalAllocated);

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
            if (_rentedMemory.TryRemove(memory, out var owner))
            {
                owner.Dispose();
                Interlocked.Add(ref _totalAllocated, -memory.Length);

                _logger.LogTrace(
                    "Memória devolvida: {Size:N0} bytes. Total alocado: {Total:N0} bytes",
                    memory.Length,
                    _totalAllocated);
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
            foreach (var owner in _rentedMemory.Values)
            {
                owner.Dispose();
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
