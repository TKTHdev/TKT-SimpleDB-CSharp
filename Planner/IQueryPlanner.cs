using AyeAyeDB.Parser;
using AyeAyeDB.Transactions;

namespace AyeAyeDB.Planner;

public interface IQueryPlanner
{
    public IPlan CreatePlan(QueryData data, Transaction tx);
}