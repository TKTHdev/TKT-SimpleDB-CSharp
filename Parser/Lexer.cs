namespace DBSharp.Parser;

public class Lexer
{
   private ICollection<string> _keywords;
   private StreamTokenizer _tok;

   public Lexer(string s)
   {
      InitKeywords();
      _tok = new StreamTokenizer(s);
      _tok.OrdinaryChar('.');
      _tok.WordChars('_', '_');
      _tok.LowerCaseMode(true);
   }

   public bool MatchDelim(char d)
   {
      return d == (char)_tok.ttype;
   }

   public bool MatchIntconstant()
   {
      return _tok.ttype == StreamTokenizer.TT_NUMBER;
   }

   public bool MatchStringConstant()
   {
      return '\'' ==  (char)_tok.ttype;
   }

   public bool MatchKeyword(string w)
   {
      return _tok.ttype == StreamTokenizer.TT_WORD && _tok.sval.Equals(w);
   }

   public bool MatchId()
   {
      return _tok.ttype == StreamTokenizer.TT_WORD && !_keywords.Contains(_tok.sval);
   }

   public void EatDelim(char d)
   {
      if (!MatchDelim(d))
         throw new BadSyntaxException();
      NextToken();
   }

   public int EatIntConstant()
   {
      if (!MatchIntconstant())
         throw new BadSyntaxException();
      int i = (int)_tok.nval;
      NextToken();
      return i;
   }

   public string EatStringConstant()
   {
      if (!MatchStringConstant())
         throw new BadSyntaxException();
      string s = _tok.sval;
      NextToken();
      return s;
   }

   public void EatKeyword(string w)
   {
      if(!MatchKeyword(w))
         throw new  BadSyntaxException();
      NextToken();
   }

   public string EatId()
   {
      if (!MatchId())
         throw new BadSyntaxException();
      string s = _tok.sval;
      NextToken();   
      return s;
   }

   private void NextToken()
   {
      try
      {
         _tok.NextToken();
      }
      catch (IOException e)
      {
         throw new BadSyntaxException();
      }
   }

   private void InitKeywords()
   {
      _keywords = new List<string>()
      {
         "select", 
         "from",
         "where",
         "and",
         "insert",
         "into",
         "values",
         "delete",
         "update",
         "set",
         "create",
         "table",
         "varchar",
         "int",
         "view",
         "as",
         "index",
         "on",
      };
   }
}