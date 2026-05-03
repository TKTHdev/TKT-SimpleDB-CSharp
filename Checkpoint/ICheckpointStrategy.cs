using AyeAyeDB.Buffers;
using AyeAyeDB.Log;

namespace AyeAyeDB.Checkpoint;

/// <summary>
/// Defines a strategy for performing a database checkpoint.
/// </summary>
public interface ICheckpointStrategy
{
    /// <summary>
    /// Performs a checkpoint using the given buffer and log managers.
    /// </summary>
    /// <param name="bm">The buffer manager used to flush dirty buffers.</param>
    /// <param name="lm">The log manager used to write checkpoint records.</param>
    void RunCheckpoint(IBufferMgr bm, LogMgr lm);
}
