namespace DBSharp.Parser;

public class StreamTokenizer
{
    public const int TT_EOF = -1;
    public const int TT_EOL = '\n';
    public const int TT_NUMBER = -2;
    public const int TT_WORD = -3;

    public int ttype = TT_EOF;
    public string? sval;
    public double nval;

    private readonly TextReader _reader;
    private int _peekc = -2;

    private const byte CT_WHITESPACE = 1;
    private const byte CT_ALPHA = 2;
    private const byte CT_NUMERIC = 4;
    private const byte CT_QUOTE = 8;

    private readonly byte[] _ct = new byte[256];
    private bool _lowerCaseMode;
    private bool _slashSlashComments;
    private bool _slashStarComments;
    private bool _eolIsSignificant;

    public StreamTokenizer(TextReader reader)
    {
        _reader = reader;
        WhitespaceChars(0, ' ');
        WordChars('a', 'z');
        WordChars('A', 'Z');
        WordChars(160, 255);
        OrdinaryChars('!', '/');
        OrdinaryChars(':', '@');
        OrdinaryChars('[', '`');
        OrdinaryChars('{', (char)127);
        QuoteChar('"');
        QuoteChar('\'');
        ParseNumbers();
    }

    public StreamTokenizer(string s) : this(new StringReader(s)) { }

    public void OrdinaryChar(int ch)
    {
        if (ch >= 0 && ch < _ct.Length) _ct[ch] = 0;
    }

    public void OrdinaryChars(int low, int hi)
    {
        for (int i = Math.Max(low, 0); i <= Math.Min(hi, _ct.Length - 1); i++)
            _ct[i] = 0;
    }

    public void WordChars(int low, int hi)
    {
        for (int i = Math.Max(low, 0); i <= Math.Min(hi, _ct.Length - 1); i++)
            _ct[i] |= CT_ALPHA;
    }

    public void WhitespaceChars(int low, int hi)
    {
        for (int i = Math.Max(low, 0); i <= Math.Min(hi, _ct.Length - 1); i++)
            _ct[i] = CT_WHITESPACE;
    }

    public void QuoteChar(int ch)
    {
        if (ch >= 0 && ch < _ct.Length) _ct[ch] = CT_QUOTE;
    }

    public void ParseNumbers()
    {
        for (int i = '0'; i <= '9'; i++) _ct[i] |= CT_NUMERIC;
        _ct['.'] |= CT_NUMERIC;
        _ct['-'] |= CT_NUMERIC;
    }

    public void LowerCaseMode(bool fl) => _lowerCaseMode = fl;
    public void SlashSlashComments(bool flag) => _slashSlashComments = flag;
    public void SlashStarComments(bool flag) => _slashStarComments = flag;
    public void EolIsSignificant(bool flag) => _eolIsSignificant = flag;

    private int Read()
    {
        if (_peekc != -2)
        {
            int c = _peekc;
            _peekc = -2;
            return c;
        }
        return _reader.Read();
    }

    private int PeekNext()
    {
        if (_peekc == -2) _peekc = _reader.Read();
        return _peekc;
    }

    public int NextToken()
    {
        sval = null;
        int c = Read();

        while (c != -1)
        {
            int ct = c < _ct.Length ? _ct[c] : CT_ALPHA;
            if ((ct & CT_WHITESPACE) == 0) break;
            if (_eolIsSignificant && (c == '\r' || c == '\n'))
            {
                if (c == '\r' && PeekNext() == '\n') Read();
                return ttype = TT_EOL;
            }
            c = Read();
        }

        if (c == -1) return ttype = TT_EOF;

        int ctype = c < _ct.Length ? _ct[c] : CT_ALPHA;

        if ((ctype & CT_ALPHA) != 0)
        {
            var sb = new System.Text.StringBuilder();
            do
            {
                sb.Append((char)c);
                c = Read();
                ctype = c < 0 || c >= _ct.Length ? 0 : _ct[c];
            } while (c != -1 && (ctype & (CT_ALPHA | CT_NUMERIC)) != 0);
            if (c != -1) _peekc = c;
            sval = _lowerCaseMode ? sb.ToString().ToLowerInvariant() : sb.ToString();
            return ttype = TT_WORD;
        }

        if ((ctype & CT_NUMERIC) != 0)
        {
            bool negative = false;
            if (c == '-')
            {
                int next = PeekNext();
                bool nextIsNumeric = next >= 0 && next < _ct.Length && (_ct[next] & CT_NUMERIC) != 0 && next != '-';
                if (!nextIsNumeric) return ttype = c;
                negative = true;
                c = Read();
            }

            double v = 0;
            bool decimalSeen = c == '.';
            double decimalScale = 0.1;

            while (c != -1)
            {
                if (c >= '0' && c <= '9')
                {
                    if (decimalSeen) { v += (c - '0') * decimalScale; decimalScale /= 10; }
                    else v = v * 10 + (c - '0');
                    c = Read();
                }
                else if (c == '.' && !decimalSeen)
                {
                    decimalSeen = true;
                    c = Read();
                }
                else break;
            }
            if (c != -1) _peekc = c;
            nval = negative ? -v : v;
            return ttype = TT_NUMBER;
        }

        if ((ctype & CT_QUOTE) != 0)
        {
            int quoteChar = c;
            var sb = new System.Text.StringBuilder();
            c = Read();
            while (c != -1 && c != quoteChar)
            {
                if (c == '\\')
                {
                    int escaped = Read();
                    if (escaped == -1) break;
                    c = escaped switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => escaped };
                }
                sb.Append((char)c);
                c = Read();
            }
            sval = sb.ToString();
            return ttype = quoteChar;
        }

        if (c == '/')
        {
            if (_slashSlashComments && PeekNext() == '/')
            {
                Read();
                while ((c = Read()) != -1 && c != '\n' && c != '\r') { }
                return NextToken();
            }
            if (_slashStarComments && PeekNext() == '*')
            {
                Read();
                int prev = 0;
                while ((c = Read()) != -1 && !(prev == '*' && c == '/')) prev = c;
                return NextToken();
            }
        }

        return ttype = c;
    }

    public override string ToString() => ttype switch
    {
        TT_EOF => "EOF",
        TT_EOL => "EOL",
        TT_NUMBER => $"n={nval}",
        TT_WORD => $"sval={sval}",
        _ when ttype < 0 => $"ttype={ttype}",
        _ => $"'{(char)ttype}'"
    };
}
