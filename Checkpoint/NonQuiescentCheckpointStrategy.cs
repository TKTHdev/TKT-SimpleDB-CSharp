using AyeAyeDB.Buffers;
using AyeAyeDB.Log;
using AyeAyeDB.Transactions;

namespace AyeAyeDB.Checkpoint;

/// <summary>
/// Non-quiescent checkpoint strategy: takes a snapshot of currently active transactions,
/// writes a NQCHECKPOINT record containing their IDs, and flushes all dirty buffers
/// without blocking new or existing transactions.
/// </summary>
public class NonQuiescentCheckpointStrategy : ICheckpointStrategy
{
    /// <inheritdoc/>
    public void RunCheckpoint(IBufferMgr bm, LogMgr lm)
    {
        // snapshot of currently active transaction numbers
        var activeTxns = Transaction.RunningTxns.ToList();
        // write a non-quiescent checkpoint record with the active txn list
        int lsn = NQCheckpointRecord.WriteToLog(lm, activeTxns);
        lm.Flush(lsn);
        // flush all dirty buffers to disk
        bm.FlushAll();
    }
}
