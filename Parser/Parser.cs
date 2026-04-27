using System.Collections.ObjectModel;
namespace DBSharp.Parser;
using DBSharp.Predicate;

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
        if (_lex.MatchKeyword("delete"))
            return Delete();
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
}
