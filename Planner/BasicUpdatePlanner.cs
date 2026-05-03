using AyeAyeDB.Metadata;
using AyeAyeDB.Parser;
using AyeAyeDB.Predicate;
using AyeAyeDB.Scan;
using AyeAyeDB.Transactions;

namespace AyeAyeDB.Planner;

public class BasicUpdatePlanner : IUpdatePlanner
{
    private MetadataMgr _mdm;
    public BasicUpdatePlanner(MetadataMgr mdm) => _mdm  = mdm;

    public int ExecuteDelete(DeleteData data, Transaction tx)
    {
        IPlan p = new TablePlan(data.TableName(), tx, _mdm);
        p = new SelectPlan(p, data.Pred());
        IUpdateScan us = (IUpdateScan)p.Open();
        int count = 0;
        while (us.Next())
        {
            us.Delete();
            count++;
        }
        us.Close();
        return count;
    }

    public int ExecuteModify(ModifyData data, Transaction tx)
    {
        IPlan p = new TablePlan(data.TableName(), tx, _mdm);
        p = new SelectPlan(p, data.Pred());
        IUpdateScan us = (IUpdateScan)p.Open();
        int count = 0;
        while (us.Next())
        {
            Constant val = data.NewValue().Evaluate(us);
            us.SetVal(data.TargetField(), val);
            count++;
        }
        us.Close();
        return count;
    }

    public int ExecuteInsert(InsertData data, Transaction tx)
    {
        IPlan p = new TablePlan(data.TableName(), tx, _mdm);
        IUpdateScan us = (IUpdateScan)p.Open();
        us.Insert();
        IEnumerator<Constant> iter = data.Vals().GetEnumerator();
        foreach (string fldname in data.Fields())
        {
            iter.MoveNext();
            us.SetVal(fldname, iter.Current);
        }
        us.Close();
        return 1;
    }

    public int ExecuteCreateTable(CreateTableData data, Transaction tx)
    {
        _mdm.CreateTable(data.TableName(), data.NewSchema(), tx);
        return 0;
    }

    public int ExecuteCreateView(CreateViewData data, Transaction tx)
    {
        _mdm.CreateView(data.ViewName(), data.ViewDef(), tx);
        return 0;
    }

    public int ExecuteCreateIndex(CreateIndexData data, Transaction tx)
    {
        _mdm.CreateIndex(data.IndexName(), data.TableName(), data.FieldName(), tx);
        return 0;
    }
}