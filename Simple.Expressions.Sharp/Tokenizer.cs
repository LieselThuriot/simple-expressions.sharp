namespace Simple.Expressions;

internal enum TokenKind
{
    End,
    Identifier,
    Number,
    String,
    Reference,
    OpenParen,
    CloseParen,
    Comma,
    OpenBracket,
    CloseBracket,
}

internal sealed class Tokenizer
{
    private static readonly System.Text.RegularExpressions.Regex s_numberPattern = new(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);
    private readonly string _source;

    public Tokenizer(string source)
    {
        _source = source;
        Next();
    }

    public TokenKind Kind { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public int Position { get; private set; }

    public void Next()
    {
        while (Position < _source.Length && _source[Position] <= ' ')
        {
            Position++;
        }

        if (Position == _source.Length)
        {
            Kind = TokenKind.End;
            Value = string.Empty;
            return;
        }

        int start = Position;
        switch (_source[Position])
        {
            case '(':
                Set(TokenKind.OpenParen, "(", 1);
                return;
            case ')':
                Set(TokenKind.CloseParen, ")", 1);
                return;
            case ',':
                Set(TokenKind.Comma, ",", 1);
                return;
            case '[':
                Set(TokenKind.OpenBracket, "[", 1);
                return;
            case ']':
                Set(TokenKind.CloseBracket, "]", 1);
                return;
            case '#':
                Position++;
                ReadDelimited();
                if (Position == start + 1)
                {
                    Fail("missing model reference");
                }

                Kind = TokenKind.Reference;
                Value = _source.Substring(start + 1, Position - start - 1);
                return;
            case '`':
            case '\'':
            case '"':
                ReadString(_source[Position]);
                return;
        }

        if (IsIdentifierLetter(_source[Position]))
        {
            Position++;
            while (Position < _source.Length && IsIdentifierLetter(_source[Position]))
            {
                Position++;
            }

            Kind = TokenKind.Identifier;
            Value = _source.Substring(start, Position - start);
            return;
        }

        ReadDelimited();
        Value = _source.Substring(start, Position - start);
        if (s_numberPattern.IsMatch(Value))
        {
            Kind = TokenKind.Number;
            return;
        }

        Fail("unexpected token " + Value);
    }

    private void ReadDelimited()
    {
        while (Position < _source.Length && _source[Position] is not '(' and not ')' and not ',' and not '[' and not ']' && _source[Position] > ' ')
        {
            Position++;
        }
    }

    private void ReadString(char quote)
    {
        int start = ++Position;
        int slashCount = 0;
        while (Position < _source.Length)
        {
            char character = _source[Position];
            if (character == '\\')
            { slashCount++; Position++; continue; }

            if (character == quote && slashCount % 2 == 0)
            {
                Kind = TokenKind.String;
                Value = _source.Substring(start, Position - start);
                Position++;
                return;
            }

            slashCount = 0;
            Position++;
        }

        Fail("unterminated string");
    }

    private void Set(TokenKind kind, string value, int length)
    {
        Kind = kind;
        Value = value;
        Position += length;
    }

    private static bool IsIdentifierLetter(char character) => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private void Fail(string message) => throw new ExpressionParseException(message, Position);
}
