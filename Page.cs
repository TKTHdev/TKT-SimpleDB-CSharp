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
        BitConverter.GetBytes(val).CopyTo(_bb, offset);
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
        BitConverter.GetBytes(b.Length).CopyTo(_bb, offset);
        Array.Copy(b, 0, _bb, offset + 4, b.Length);
    }
    public string GetString(int offset)
    {
        byte[] b = GetBytes(offset);
        return CHARSET.GetString(b);
    }
    public void SetString(int offset, string s)
    {
        byte[] b = CHARSET.GetBytes(s);
        SetBytes(offset, b);
    }
    public static int MaxLength(int strlen)
    {
        int bytesPerChar = CHARSET.GetMaxByteCount(1);
        return sizeof(int) + (strlen * bytesPerChar);
    }
    internal byte[] Contents()
    {
        return _bb;
    }
}