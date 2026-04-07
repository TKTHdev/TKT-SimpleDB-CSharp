using DBSharp.File;

namespace DBSharp.Buffers;

/// <summary>
/// Defines the contract for a buffer manager that pins/unpins disk blocks
/// in an in-memory buffer pool.
/// </summary>
public interface IBufferMgr
{
    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    int Available();

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    void FlushAll(int txnum);

    /// <summary>
    /// Pins the buffer holding the specified block, blocking until a buffer is
    /// available or the wait times out.
    /// </summary>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    Buffer Pin(BlockId blk);

    /// <summary>
    /// Unpins the given buffer, making it eligible for replacement once fully unpinned.
    /// </summary>
    void Unpin(Buffer buff);
}
