using System.Collections.ObjectModel;
namespace DBSharp.Parser;
using DBSharp.Predicate;
using DBSharp.Record;

public class Parser
{
    private Lexer _lex;

    public Parser(string s)
    {
        _lex = new Lexer(s);
    }

    // Methods for parsing predicates and their components
    public string Field()
    {
        return _lex.EatId();
    }

    public Constant Constant()
    {
        if (_lex.MatchStringConstant())
            return new Constant(_lex.EatStringConstant());
        else
            return new Constant(_lex.EatIntConstant());
    }

    public Expression Expression()
    {
        if (_lex.MatchId())
            return new Expression(Field());
        else
            return new Expression(Constant());
    }

    public Term Term()
    {
        Expression lhs = Expression();
        _lex.EatDelim('=');
        Expression rhs = Expression();
        return new Term(lhs, rhs);
    }

    public Predicate Predicate()
    {
        Predicate pred = new Predicate(Term());
        if (_lex.MatchKeyword("and"))
        {
            _lex.EatKeyword("and");
            pred.ConjoinWith(Predicate());
        }
        return pred;
    }

    // Methods for parsing queries
    public QueryData Query()
    {
        _lex.EatKeyword("select");
        List<string> fields = SelectList();
        _lex.EatKeyword("from");
        Collection<string> tables = TableList();
        Predicate pred = new Predicate();
        if (_lex.MatchKeyword("where"))
        {
            _lex.EatKeyword("where");
            pred = Predicate();
        }
        return new QueryData(fields, tables, pred);
    }

    private List<string> SelectList()
    {
        List<string> L = new();
        L.Add(Field());
        if (_lex.MatchDelim(','))
        {
            _lex.EatDelim(',');
            L.AddRange(SelectList());
        }
        return L;
    }

    private Collection<string> TableList()
    {
        Collection<string> L = new();
        L.Add(_lex.EatId());
        if (_lex.MatchDelim(','))
        {
            _lex.EatDelim(',');
            foreach (var t in TableList()) L.Add(t);
        }
        return L;
    }

    // Methods for parsing the various update commands
    public Object UpdateCmd()
    {
        if (_lex.MatchKeyword("insert"))
            return Insert();
        else if (_lex.MatchKeyword("delete"))
            return Delete();
        else if (_lex.MatchKeyword("update"))
            return Modify();
        else
            return Create();
    }

    private Object Create()
    {
        _lex.EatKeyword("create");
        if (_lex.MatchKeyword("table"))
            return CreateTable();
        throw new NotImplementedException();
    }

    // Method for parsing delete commands
    public DeleteData Delete()
    {
        _lex.EatKeyword("delete");
        _lex.EatKeyword("from");
        string tblname = _lex.EatId();
        Predicate pred = new Predicate();
        if (_lex.MatchKeyword("where"))
        {
            _lex.EatKeyword("where");
            pred = Predicate();
        }
        return new DeleteData(tblname, pred);
    }

    // Methods for parsing insert commands
    public InsertData Insert()
    {
        _lex.EatKeyword("insert");
        _lex.EatKeyword("into");
        string tblname = _lex.EatId();
        _lex.EatDelim('(');
        List<string> flds = FieldList();
        _lex.EatDelim(')');
        _lex.EatKeyword("values");
        _lex.EatDelim('(');
        List<Constant> vals = ConstList();
        _lex.EatDelim(')');
        return new InsertData(tblname, flds, vals);
    }

    private List<string> FieldList()
    {
        List<string> L = new();
        L.Add(Field());
        if (_lex.MatchDelim(','))
        {
            _lex.EatDelim(',');
            L.AddRange(FieldList());
        }
        return L;
    }

    private List<Constant> ConstList()
    {
        List<Constant> L = new();
        L.Add(Constant());
        if (_lex.MatchDelim(','))
        {
            _lex.EatDelim(',');
            L.AddRange(ConstList());
        }
        return L;
    }

    // Method for parsing modify commands
    public ModifyData Modify()
    {
        _lex.EatKeyword("update");
        string tblname = _lex.EatId();
        _lex.EatKeyword("set");
        string fldname = Field();
        _lex.EatDelim('=');
        Expression newval = Expression();
        Predicate pred = new Predicate();
        if (_lex.MatchKeyword("where"))
        {
            _lex.EatKeyword("where");
            pred = Predicate();
        }
        return new ModifyData(tblname, fldname, newval, pred);
    }

    // Methods for parsing CREATE TABLE
    private CreateTableData CreateTable()
    {
        _lex.EatKeyword("table");
        string tblname = _lex.EatId();
        _lex.EatDelim('(');
        Schema sch = FieldDefs();
        _lex.EatDelim(')');
        return new CreateTableData(tblname, sch);
    }

    private Schema FieldDefs()
    {
        Schema schema = FieldDef();
        if (_lex.MatchDelim(','))
        {
            _lex.EatDelim(',');
            schema.AddAll(FieldDefs());
        }
        return schema;
    }

    private Schema FieldDef()
    {
        string fldname = Field();
        return FieldType(fldname);
    }

    private Schema FieldType(string fldname)
    {
        Schema schema = new Schema();
        if (_lex.MatchKeyword("int"))
        {
            _lex.EatKeyword("int");
            schema.AddIntField(fldname);
        }
        else
        {
            _lex.EatKeyword("varchar");
            _lex.EatDelim('(');
            int strLen = _lex.EatIntConstant();
            _lex.EatDelim(')');
            schema.AddStringField(fldname, strLen);
        }
        return schema;
    }
}
