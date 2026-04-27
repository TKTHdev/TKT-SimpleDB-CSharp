using System.Collections.ObjectModel;
namespace DBSharp.Parser;

using Predicate = global::DBSharp.Predicate.Predicate;

public class QueryData
{
    private List<string> _fields;
    private Collection<string> _tables;
    private Predicate _pred;

    public QueryData(List<string> fields, Collection<string> tables, Predicate pred)
    {
        _fields = fields;
        _tables = tables;
        _pred = pred;
    }

    public List<string> Fields() => _fields;
    public Collection<string> Tables() => _tables;
    public Predicate Pred() => _pred;

    public override string ToString()
    {
        string result = "select ";
        foreach (string fldname in _fields)
            result += fldname + ", ";
        result = result.Substring(0, result.Length - 2);
        result += " from ";
        foreach (string tblname in _tables)
            result += tblname + ", ";
        result = result.Substring(0, result.Length - 2);
        string predstring = _pred.ToString() ?? "";
        if (!predstring.Equals(""))
            result += " where " + predstring;
        return result;
    }
}