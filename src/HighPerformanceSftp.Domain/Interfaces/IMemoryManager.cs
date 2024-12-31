using System;

namespace HighPerformanceSftp.Domain.Interfaces;

public interface IMemoryManager : IDisposable
{
    Memory<byte> Rent(int size);
    void Return(Memory<byte> memory);
    long GetTotalAllocatedBytes();
}
