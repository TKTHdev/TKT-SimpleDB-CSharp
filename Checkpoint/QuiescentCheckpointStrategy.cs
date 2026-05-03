using AyeAyeDB.Buffers;
using AyeAyeDB.Log;
using AyeAyeDB.Transactions;

namespace AyeAyeDB.Checkpoint;

/// <summary>
/// Quiescent checkpoint strategy: blocks new transactions, waits for all running
/// transactions to finish, then flushes all modified buffers and writes a CHECKPOINT record.
/// </summary>
public class QuiescentCheckpointStrategy : ICheckpointStrategy
{
    /// <inheritdoc/>
    public void RunCheckpoint(IBufferMgr bm, LogMgr lm)
    {
        Transaction.RunQuiescentCheckpointing(bm, lm);
    }
}
