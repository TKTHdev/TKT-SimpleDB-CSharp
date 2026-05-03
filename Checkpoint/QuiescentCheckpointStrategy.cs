using DBSharp.Buffers;
using DBSharp.Log;
using DBSharp.Transactions;

namespace DBSharp.Checkpoint;

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
