using DBSharp.TmpClass;

namespace DBSharp.Scan;

public interface IScan
{
    public void BeforeFirst();
    public bool Next();
    public int GetInt(string fldname);
    public string GetString(string fldname);
    public Constant GetVal(string fldname);
    public bool HasField(string fldname);
    public void Close();
}