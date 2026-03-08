namespace DBSharp;

using System.Text;

public class Page
{
    private byte[] _bb;
    public static readonly Encoding CHARSET = Encoding.ASCII;
    public Page(int blocksize)
    {
        _bb = new byte[blocksize];
    }
    public int GetInt(int offset)
    {
        int val = BitConverter.ToInt32(_bb, offset);
        return val;
    }
    public void SetInt(int offset, int val)
    {
        if (offset + sizeof(int) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
    }
    public int GetShort(int offset)
    {
        short val = BitConverter.ToInt16(_bb, offset);
        return val;
    }
    public void SetShort(int offset, short val)
    {
        if (offset + sizeof(short) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
    }
    public bool GetBool(int offset)
    {
        bool val = BitConverter.ToBoolean(_bb, offset);
        return val;
    }
    public void SetBool(int offset, bool val)
    {
        if (offset + sizeof(bool) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
    }
    public DateTime GetDateTime(int offset)
    {
        long ticks = BitConverter.ToInt64(_bb, offset);
        return new DateTime(ticks);
    }
    public void SetDateTime(int offset, DateTime val)
    {
        if (offset + sizeof(long) > _bb.Length)
            throw new ArgumentException("offset exceeds page size");
        BitConverter.GetBytes(val.Ticks).CopyTo(_bb, offset);
    }
    public byte[] GetBytes(int offset)
    {
        int length = BitConverter.ToInt32(_bb, offset);
        byte[] b = new byte[length];
        Array.Copy(_bb, offset + 4, b, 0, length);
        return b;
    }
    public void SetBytes(int offset, byte[] b)
    {
        if (offset + sizeof(int) + b.Length > _bb.Length)
            throw new ArgumentException("data exceeds page size");
        BitConverter.GetBytes(b.Length).CopyTo(_bb, offset);
        Array.Copy(b, 0, _bb, offset + 4, b.Length);
    }
    public string GetString(int offset)
    {
        int end = offset;
        while (end < _bb.Length && _bb[end] != 0)
            end++;

        return CHARSET.GetString(_bb, offset, end - offset);
    }
    public void SetString(int offset, string s)
    {
        byte[] b = CHARSET.GetBytes(s);
        if (offset + b.Length + 1 > _bb.Length)
            throw new ArgumentException("data exceeds page size");
        Array.Copy(b, 0, _bb, offset, b.Length);
        _bb[offset + b.Length] = (byte)'\0';
    }
    public static int MaxLength(int strlen)
    {
        int bytesPerChar = CHARSET.GetMaxByteCount(1);
        return (strlen + 1) * bytesPerChar;
    }
    internal byte[] Contents()
    {
        return _bb;
    }
}
