using DBSharp.Parser;
using DBSharp.Transactions;
using SqlParser = DBSharp.Parser.Parser;

namespace DBSharp.Planner;

public class Planner
{
    private IQueryPlanner _qplanner;
    private IUpdatePlanner _uplanner;

    public Planner(IQueryPlanner qplanner, IUpdatePlanner uplanner)
    {
        _qplanner = qplanner;
        _uplanner = uplanner;
    }

    public IPlan CreateQueryPlan(string query, Transaction tx)
    {
        SqlParser parser = new SqlParser(query);
        QueryData data = parser.Query();
        return _qplanner.CreatePlan(data, tx);
    }

    public int ExecuteUpdate(string cmd, Transaction tx)
    {
        SqlParser parser = new SqlParser(cmd);
        object obj = parser.UpdateCmd();
        if (obj is InsertData insertData)
            return _uplanner.ExecuteInsert(insertData, tx);
        else if (obj is DeleteData deleteData)
            return _uplanner.ExecuteDelete(deleteData, tx);
        else if (obj is ModifyData modifyData)
            return _uplanner.ExecuteModify(modifyData, tx);
        else if (obj is CreateTableData createTableData)
            return _uplanner.ExecuteCreateTable(createTableData, tx);
        else if (obj is CreateViewData createViewData)
            return _uplanner.ExecuteCreateView(createViewData, tx);
        else if (obj is CreateIndexData createIndexData)
            return _uplanner.ExecuteCreateIndex(createIndexData, tx);
        else
            return 0;
    }
}
