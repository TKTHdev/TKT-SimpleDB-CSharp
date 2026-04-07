namespace DBSharp.File;

using System.Text;

/// <summary>
/// Provides typed read/write access to an in-memory byte buffer that represents a disk block.
/// </summary>
public class Page
{
    private byte[] _bb;

    /// <summary>
    /// The character encoding used for string serialization.
    /// </summary>
    public static readonly Encoding CHARSET = Encoding.ASCII;

    /// <summary>
    /// Creates a new page backed by a zero-filled buffer of the given block size.
    /// </summary>
    /// <param name="blocksize">The size of the page in bytes.</param>
    public Page(int blocksize)
    {
        _bb = new byte[blocksize];
    }

    /// <summary>
    /// Creates a new page backed by an existing byte array.
    /// </summary>
    /// <param name="b">The byte array to wrap.</param>
    public Page(byte[] b)
    {
        _bb = b;
    }

    /// <summary>
    /// Reads a 32-bit integer from the specified byte offset.
    /// </summary>
    /// <param name="offset">The byte offset to read from.</param>
    public int GetInt(int offset)
    {
        int val = BitConverter.ToInt32(_bb, offset);
        return val;
    }

    /// <summary>
    /// Writes a 32-bit integer at the specified byte offset.
    /// </summary>
    /// <param name="offset">The byte offset to write to.</param>
    /// <param name="val">The integer value to write.</param>
    /// <exception cref="ArgumentException">Thrown when the write would exceed the page boundary.</exception>
    public void SetInt(int offset, int val)
    {
        if (offset + sizeof(int) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
    }

    /// <summary>
    /// Reads a 16-bit short from the specified byte offset, returned as an int.
    /// </summary>
    /// <param name="offset">The byte offset to read from.</param>
    public int GetShort(int offset)
    {
        short val = BitConverter.ToInt16(_bb, offset);
        return val;
    }

    /// <summary>
    /// Writes a 16-bit short at the specified byte offset.
    /// </summary>
    /// <param name="offset">The byte offset to write to.</param>
    /// <param name="val">The short value to write.</param>
    /// <exception cref="ArgumentException">Thrown when the write would exceed the page boundary.</exception>
    public void SetShort(int offset, short val)
    {
        if (offset + sizeof(short) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
    }

    /// <summary>
    /// Reads a boolean from the specified byte offset.
    /// </summary>
    /// <param name="offset">The byte offset to read from.</param>
    public bool GetBool(int offset)
    {
        bool val = BitConverter.ToBoolean(_bb, offset);
        return val;
    }

    /// <summary>
    /// Writes a boolean at the specified byte offset.
    /// </summary>
    /// <param name="offset">The byte offset to write to.</param>
    /// <param name="val">The boolean value to write.</param>
    /// <exception cref="ArgumentException">Thrown when the write would exceed the page boundary.</exception>
    public void SetBool(int offset, bool val)
    {
        if (offset + sizeof(bool) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
    }

    /// <summary>
    /// Reads a <see cref="DateTime"/> from the specified byte offset, stored as ticks (Int64).
    /// </summary>
    /// <param name="offset">The byte offset to read from.</param>
    public DateTime GetDateTime(int offset)
    {
        long ticks = BitConverter.ToInt64(_bb, offset);
        return new DateTime(ticks);
    }

    /// <summary>
    /// Writes a <see cref="DateTime"/> at the specified byte offset, stored as ticks (Int64).
    /// </summary>
    /// <param name="offset">The byte offset to write to.</param>
    /// <param name="val">The DateTime value to write.</param>
    /// <exception cref="ArgumentException">Thrown when the write would exceed the page boundary.</exception>
    public void SetDateTime(int offset, DateTime val)
    {
        if (offset + sizeof(long) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val.Ticks).CopyTo(_bb, offset);
    }

    /// <summary>
    /// Reads a length-prefixed byte array from the specified offset.
    /// The first 4 bytes encode the length, followed by the data.
    /// </summary>
    /// <param name="offset">The byte offset to read from.</param>
    public byte[] GetBytes(int offset)
    {
        int length = BitConverter.ToInt32(_bb, offset);
        byte[] b = new byte[length];
        Array.Copy(_bb, offset + 4, b, 0, length);
        return b;
    }

    /// <summary>
    /// Writes a length-prefixed byte array at the specified offset.
    /// </summary>
    /// <param name="offset">The byte offset to write to.</param>
    /// <param name="b">The byte array to write.</param>
    /// <exception cref="ArgumentException">Thrown when the data would exceed the page boundary.</exception>
    public void SetBytes(int offset, byte[] b)
    {
        if (offset + sizeof(int) + b.Length > _bb.Length)
            throw new ArgumentException("data exceeds page size");
        BitConverter.GetBytes(b.Length).CopyTo(_bb, offset);
        Array.Copy(b, 0, _bb, offset + 4, b.Length);
    }

    /// <summary>
    /// Reads a null-terminated ASCII string from the specified offset.
    /// </summary>
    /// <param name="offset">The byte offset to read from.</param>
    public string GetString(int offset)
    {
        int end = offset;
        while (end < _bb.Length && _bb[end] != 0)
            end++;

        return CHARSET.GetString(_bb, offset, end - offset);
    }

    /// <summary>
    /// Writes a null-terminated ASCII string at the specified offset.
    /// </summary>
    /// <param name="offset">The byte offset to write to.</param>
    /// <param name="s">The string to write.</param>
    /// <exception cref="ArgumentException">Thrown when the string would exceed the page boundary.</exception>
    public void SetString(int offset, string s)
    {
        byte[] b = CHARSET.GetBytes(s);
        if (offset + b.Length + 1 > _bb.Length)
            throw new ArgumentException("data exceeds page size");
        Array.Copy(b, 0, _bb, offset, b.Length);
        _bb[offset + b.Length] = (byte)'\0';
    }

    /// <summary>
    /// Returns the maximum number of bytes needed to store a string of the given character length,
    /// including the null terminator.
    /// </summary>
    /// <param name="strlen">The number of characters in the string.</param>
    public static int MaxLength(int strlen)
    {
        int bytesPerChar = CHARSET.GetMaxByteCount(1);
        return (strlen + 1) * bytesPerChar;
    }

    /// <summary>
    /// Returns the underlying byte array of this page.
    /// </summary>
    internal byte[] Contents()
    {
        return _bb;
    }
}
