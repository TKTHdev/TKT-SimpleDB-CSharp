using DBSharp.Record;
using DBSharp.Transactions;

namespace DBSharp.Metadata;

public class StatMgr
{
    private TableMgr _tblMgr;
    private Dictionary<string, StatInfo> _tablestats;
    private int _numcalls;

    public StatMgr(TableMgr tblMgr, Transaction tx)
    {
        _tblMgr = tblMgr;
        RefreshStatistics(tx);
    }

    public StatInfo GetStatInfo(string tblname, Layout layout, Transaction tx)
    {
        lock (this)
        {
            _numcalls++;
            if (_numcalls > 100)
                RefreshStatistics(tx);
            StatInfo? si;
            if (!_tablestats.TryGetValue(tblname, out si))
            {
                si = CalcTableStats(tblname, layout, tx);
                _tablestats[tblname] = si;
            }
            return si;
        }
    }

    private void RefreshStatistics(Transaction tx)
    {
        lock (this)
        {
            _tablestats = new Dictionary<string, StatInfo>();
            _numcalls = 0;
            Layout tcatlayout = _tblMgr.GetLayout("tblcat", tx);
            var tcat = new TableScan(tx, "tblcat", tcatlayout);
            while (tcat.Next())
            {
                string tblname = tcat.GetString("tblname");
                Layout layout = _tblMgr.GetLayout(tblname, tx);
                StatInfo si = CalcTableStats(tblname, layout, tx);
                _tablestats[tblname] = si;
            }
            tcat.Close();
        }
    }

    private StatInfo CalcTableStats(string tblname, Layout layout, Transaction tx)
    {
        lock (this)
        {
            int numRecs = 0;
            int numblocks = 0;
            var ts = new TableScan(tx, tblname, layout);
            while (ts.Next())
            {
                numRecs++;
                numblocks = ts.GetRid().BlockNumber() + 1;
            }
            ts.Close();
            return new StatInfo(numblocks, numRecs);
        }
    }
}
