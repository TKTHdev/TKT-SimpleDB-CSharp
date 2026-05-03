using System.Collections;
using DBSharp.Metadata;
using DBSharp.Parser;
using DBSharp.Transactions;

namespace DBSharp.Planner;

public class BasicQueryPlanner : IQueryPlanner
{
    private MetadataMgr _mdm;
    public BasicQueryPlanner(MetadataMgr mdm) => _mdm = mdm;

    public IPlan CreatePlan(QueryData data, Transaction tx)
    {
        // Step1: Create a plan for each mentioned table or view
        List<IPlan> plans = new();
        foreach (string tblname in data.Tables())
        {
            string viewdef = _mdm.GetViewDef(tblname, tx);
            if (viewdef != null)
            {
                Parser.Parser parser  = new Parser.Parser(viewdef);
                QueryData viewdata = parser.Query();
                plans.Add(CreatePlan(viewdata, tx));
            }
            else
                plans.Add(new TablePlan(tblname,tx,_mdm));
        }
        // Step2: Create the product of all table plans
        IPlan p = plans[0];
        plans.RemoveAt(0);
        foreach(IPlan nextplan in plans)
            p = new ProductPlan(p, nextplan);
        
        // Step3: Add a selection plan for the predicate
        p = new SelectPlan(p, data.Pred());
        
        // Step4: Project on the field names
        return new ProjectPlan(p, data.Fields());
    }
}