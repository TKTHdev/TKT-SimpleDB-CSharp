namespace DBSharp.Concurrency;

/// <summary>
/// Thrown when a lock request times out because the lock could not be acquired
/// within the maximum wait time.
/// </summary>
public class LockAbortException : Exception;
