namespace DBSharp.Buffers;

/// <summary>
/// Thrown when a buffer pin request times out because no buffer became available
/// within the maximum wait time.
/// </summary>
public class BufferAbortException : Exception;
