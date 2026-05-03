using DBSharp.Parser;
using DBSharp.Transactions;

namespace DBSharp.Planner;

public interface IQueryPlanner
{
    public IPlan CreatePlan(QueryData data, Transaction tx);
}