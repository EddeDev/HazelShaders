using Sprache;
using System.Diagnostics;

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

    /*
    enum PreprocessorDirectiveType
    {
        None = 0,

        // Macros
        Define,

        // File inclusion
        IncludeAbsolute,
        IncludeRelative,

        // Conditional compilation
        Ifdef,
        Endif,
        If,
        Else,
        Ifndef,
        Undef,

        // Example: #stage fragment or #stage compute
        Stage
    }

    internal class PreprocessorToken : Token
    {
        public string DirectiveType { get; private set; }

        public PreprocessorToken(string type, string value) : base(TokenType.PreprocessorKeyword, value)
        {
            Debug.WriteLine($"Creating PreprocessorToken (type={type}, value={value})");
            DirectiveType = type;
        }
    }
    */
}
