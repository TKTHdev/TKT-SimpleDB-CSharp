using AyeAyeDB.Parser;
using AyeAyeDB.Transactions;

namespace AyeAyeDB.Planner;

public interface IUpdatePlanner
{
    public int ExecuteInsert(InsertData data, Transaction tx);
    public int ExecuteDelete(DeleteData data, Transaction tx);
    public int ExecuteModify(ModifyData data, Transaction tx);
    public int ExecuteCreateTable(CreateTableData data, Transaction tx);
    public int ExecuteCreateView(CreateViewData data, Transaction tx);
    public int ExecuteCreateIndex(CreateIndexData data, Transaction tx);
}