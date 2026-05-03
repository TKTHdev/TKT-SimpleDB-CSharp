using DBSharp.Parser;
using DBSharp.Transactions;

namespace DBSharp.Planner;

public interface QueryPlanner
{
    public IPlan CreatePlan(QueryData data, Transaction tx);
}