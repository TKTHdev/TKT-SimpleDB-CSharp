using DBSharp.Record;

namespace DBSharp.Scan;
using DBSharp.TmpClass;


/*
 * We assume that underlying scan is also updatable:
 * in particular, they assume that they can cast
 * the underlying scan to UpdateScan without
 * causing a ClassCastException
 */

public interface IUpdateScan: IScan
{
    public void SetInt(string fidname, int val);
    public void SetString(string fldname, string val);
    public void SetVal(string fldname, Constant val);
    public void Insert();
    public void Delete();
    public RID GetRid();
    public void MoveToRid(RID rid);
}