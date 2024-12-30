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

        // TODO: WIP
        Function,
        
        Identifier,
        Operator,

        Keyword,
        Variable,
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

    struct FunctionParameter
    {
        public string Type;
        public string Name;
    }

    internal class FunctionToken : Token
    {
        public string ReturnType { get; private set; }
        public string Name { get; private set; }
        public FunctionParameter[] Parameters { get; private set; }

        public FunctionToken(string returnType, string name, FunctionParameter[] parameters) : base(TokenType.Function, name)
        {
            ReturnType = returnType;
            Name = name;
            Parameters = parameters;
        }
    }

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
            // Debug.WriteLine($"Creating PreprocessorToken (type={type}, value={value})");
            DirectiveType = type;
        }
    }
}
