using Sprache;

namespace HazelShaders
{
    public enum TokenType
    {
        Comment,
        PreprocessorKeyword,
        QuotedString,
        Number,
        Identifier,
        Operator,

        Keyword,
        Variable,
        Function,
        Statement
    }

    public interface IToken
    {
        int Start { get; }
        int Length { get; }
        string Value { get; }
        TokenType Type { get; }
    }

    internal class Token : IPositionAware<Token>, IToken
    {
        public int Start { get; private set; }
        public int Length { get; private set; }
        public string Value { get; private set; }
        public TokenType Type { get; private set; }

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public Token SetPos(Position startPos, int length)
        {
            Start = startPos.Pos;
            Length = length;
            return this;
        }
    }

    internal class PreprocessorToken : Token
    {
        public string Identifier { get; private set; }
        public string Replacement { get; private set; }

        public PreprocessorToken(string identifier, string replacement) : base(TokenType.PreprocessorKeyword, identifier /*TODO*/)
        {
            Identifier = identifier;
            Replacement = replacement;
        }
    }
}
