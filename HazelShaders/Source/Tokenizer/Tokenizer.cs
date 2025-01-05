using Newtonsoft.Json.Linq;
using Sprache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HazelShaders
{
    public enum TokenType
    {
        // TODO
        Comment,
        PreprocessorKeyword,
        QuotedString,
        Number,
        Identifier,
        Operator,
        NewLine,

        FunctionName,
        MissingFunctionName,
        Keyword,
        TypeName,
        BaseTypeName,
        Qualifier,
        Variable,
        Statement
    }

    public static class TokenTypeExtensions
    {
        public static bool IsTypeName(this TokenType type)
        {
            return type == TokenType.TypeName || type == TokenType.BaseTypeName;
        }

        public static bool IsFunctionName(this TokenType type)
        {
            return type == TokenType.FunctionName || type == TokenType.MissingFunctionName;
        }
    }

    public interface IToken
    {
        Position StartPos { get; set; }
        int Length { get; set; }
        string Value { get; set; }
        TokenType Type { get; set; }
    }

    internal class Token : IPositionAware<Token>, IToken
    {
        public Position StartPos { get; set; }
        public int Length { get; set; }
        public string Value { get; set; }
        public TokenType Type { get; set; }

        public Token PreviousToken { get; set; }
        public int LineNumber { get; set; }

        public Token(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }

        public Token SetPos(Position startPos, int length)
        {
            StartPos = startPos;
            Length = length;
            return this;
        }
    }

    internal class PreprocessorToken : Token
    {
        public string DirectiveType { get; private set; }

        public PreprocessorToken(string type) : base(TokenType.PreprocessorKeyword)
        {
            DirectiveType = type;
        }
    }

    internal class NewlineToken : Token
    {
        public bool HasCarriageReturn { get; private set; }
        public bool HasNewline { get; private set; }

        public NewlineToken(bool hasCarriageReturn, bool hasNewline) : base(TokenType.NewLine)
        {
            HasCarriageReturn = hasCarriageReturn;
            HasNewline = hasNewline;
        }
    }

    static class ParserExtensions
    {
        public static T Update<T>(this T token, Tokenizer tokenizer) where T : Token
        {
            token = tokenizer.OnParseToken(token);
            token.PreviousToken = tokenizer.PreviousToken;
            tokenizer.PreviousToken = token;
            return token;
        }
    }

    internal class Tokenizer
    {
        private static Parser<string> HexNumberParser = from prefix in Parse.IgnoreCase("0x")
                                                        from digits in Parse.Chars("0123456789ABCDEFabcdef").AtLeastOnce().Text()
                                                        select prefix + digits;

        private static Parser<string> ExponentPartParser = from e in Parse.IgnoreCase("e")
                                                           from sign in Parse.Char('-').Or(Parse.Char('+')).Optional()
                                                           from digits in Parse.Digit.AtLeastOnce().Text()
                                                           select $"e{sign.GetOrDefault()}{digits}";

        private static Parser<string> DecimalNumberParser = from d in Parse.DecimalInvariant
                                                            from e in ExponentPartParser.Optional()
                                                            select d + e;
                                                            
        public Token PreviousToken { get; set; }

        private readonly Parser<IEnumerable<Token>> m_Parser;

        // TODO: layout qualifiers
        public List<string> LocalStructNames { get; private set; }
        public List<string> LocalFunctionNames { get; private set; }

        public struct LocalVariable
        {
            public string Name { get; set; }
            public int StackDepth { get; set; }
        }
        public List<LocalVariable> LocalVariables { get; private set; }

        private int m_CurrentStackDepth = 0;
        private int m_CurrentLineNumber = 0;

        // Just for debugging
        private bool m_IsRoot = false;

        private string m_Filepath { get; set; }

        public Tokenizer(bool isRoot = true)
        {
            m_IsRoot = isRoot;

            LocalStructNames = new List<string>();
            LocalFunctionNames = new List<string>();
            LocalVariables = new List<LocalVariable>();
            PreviousToken = null;

            Parser<Token> token = null;
            foreach (TokenType tokenType in Enum.GetValues(typeof(TokenType)))
            {
                Parser<Token> parser = CreateParser(tokenType);
                if (parser == null)
                    continue;
                if (token == null)
                    token = parser;
                token = token.Or(parser);
            }
            m_Parser = token.Positioned().Token().Many();
        }

        public List<IToken> Tokenize(string text, string filepath)
        {
            LocalStructNames.Clear();
            LocalFunctionNames.Clear();
            LocalVariables.Clear();
            PreviousToken = null;
            m_CurrentStackDepth = 0;
            m_CurrentLineNumber = 1; // TODO
            m_Filepath = filepath;

            var tokens = m_Parser.TryParse(text);
            if (tokens.WasSuccessful)
                return tokens.Value.ToList<IToken>();

            return new List<IToken>();
        }     

        private Parser<Token> CreateParser(TokenType type)
        {
            Parser<Token> parser = null;
            switch (type)
            {
                case TokenType.Comment:
                    parser = from value in new CommentParser().AnyComment
                             select new Token(TokenType.Comment, value).Update(this);
                    break;
               
                case TokenType.PreprocessorKeyword:
                    parser = from open in Parse.Char('#').Token()
                             from typeString in Parse.Identifier(Parse.Letter, Parse.LetterOrDigit).Token()
                             select new PreprocessorToken(typeString).Update(this);
                    break;
               
                case TokenType.QuotedString:
                    parser = from open in Parse.Char('"')
                             from value in Parse.CharExcept("\"\r\n").Many().Text()
                             select new Token(TokenType.QuotedString, value).Update(this);
                    break;
                case TokenType.Number:
                    parser = from value in HexNumberParser.Or(DecimalNumberParser)
                             from suffix in Parse.IgnoreCase("f").Or(Parse.IgnoreCase("u")).Optional()
                             select new Token(TokenType.Number, value).Update(this);
                    break;
                case TokenType.Identifier:
                    parser = from value in Parse.Identifier(Parse.Char((char c) => char.IsLetter(c) || '_' == c || '@' == c, "Identifier start"), Parse.Char((char c) => char.IsDigit(c) || (char.IsLetter(c) || '_' == c || '@' == c), "Identifier character"))
                             select new Token(GlslSpecification.KeywordToTokenType(value), value).Update(this);
                    break;

                case TokenType.Operator:
                    parser = from value in Parse.Chars(".,;+-*/?:!&|^{}()[]<>=\\")
                             select new Token(TokenType.Operator, value.ToString()).Update(this);
                    break;

                case TokenType.NewLine:
                    parser = from carriageReturn in Parse.Char('\r').Optional()
                             from newLine in Parse.Char('\n').Optional()
                             select new NewlineToken(carriageReturn.IsDefined, newLine.IsDefined).Update(this);
                    break;
            }
            return parser;
        }

        public T OnParseToken<T>(T token) where T : Token
        {
            token.LineNumber = m_CurrentLineNumber;

            if (token.Type == TokenType.Identifier)
                return OnParseIdentifier(token);

            if (token.Type == TokenType.QuotedString)
                return OnParseQuotedString(token);

            if (token.Type == TokenType.Operator)
                return OnParseOperator(token);

            if (token.Type == TokenType.NewLine)
                return OnParseNewLine(token);

            return token;
        }

        private T OnParseIdentifier<T>(T token) where T : Token
        {
            if (PreviousToken == null)
            {
                token.Type = GlslSpecification.KeywordToTokenType(token.Value);
                return token;
            }

            if (PreviousToken.Type == TokenType.PreprocessorKeyword)
            {
                PreprocessorToken previousToken = (PreprocessorToken)PreviousToken;
                if (previousToken.LineNumber == token.LineNumber)
                {
                    previousToken.Value = token.Value;
                    token.Type = TokenType.Identifier;
                    return token;
                }
            }

            // Example: struct MyStruct { ... };
            // struct is a keyword
            // "MyStruct" is a type name
            if (PreviousToken.Type == TokenType.Keyword)
            {
                if (PreviousToken.Value == "struct")
                {
                    token.Type = TokenType.TypeName;
                    LocalStructNames.Add(token.Value);
                    return token;
                }
            }

            // Example: DirectionalLight dirLight;
            // "DirectionalLight" is a custom struct (type name)
            // dirLight is a normal identifier
            if (PreviousToken.Type == TokenType.Identifier)
            {
                if (LocalStructNames.Contains(PreviousToken.Value))
                {
                    PreviousToken.Type = TokenType.TypeName;

                    // TODO: what about layout qualifiers?

                    LocalVariable variable = new LocalVariable();
                    variable.Name = token.Value;
                    variable.StackDepth = m_CurrentStackDepth;
                    LocalVariables.Add(variable);

                    if (m_IsRoot)
                    {
                        Debug.WriteLine("a");
                    }
                }
            }

            TokenType currentTokenType = GlslSpecification.KeywordToTokenType(token.Value);
            if (currentTokenType != TokenType.Identifier && PreviousToken.Type.IsTypeName())
                currentTokenType = TokenType.Identifier;
            token.Type = currentTokenType;
            return token;
        }

        private T OnParseQuotedString<T>(T token) where T : Token
        {
            if (PreviousToken == null)
                return token;

            if (PreviousToken.Type == TokenType.PreprocessorKeyword)
            {
                PreprocessorToken previousToken = (PreprocessorToken)PreviousToken;
                if (previousToken.DirectiveType == "include")
                {
                    var filepath = token.Value;

                    string includeDir = Path.GetDirectoryName(m_Filepath);
                    string includePath = Path.Combine(includeDir, filepath);

                    if (File.Exists(includePath))
                    {
                        string includeContent = File.ReadAllText(includePath);

                        Tokenizer tokenizer = new Tokenizer(false);
                        tokenizer.Tokenize(includeContent, includePath);

                        LocalStructNames.AddRange(tokenizer.LocalStructNames);
                        LocalFunctionNames.AddRange(tokenizer.LocalFunctionNames);
                    }

                    return token;
                }
            }

            return token;
        }

        private T OnParseOperator<T>(T token) where T : Token
        {
            if (token.Value == "{")
            {
                m_CurrentStackDepth++;
                return token;
            }

            if (token.Value == "}")
            {
                m_CurrentStackDepth--;
                return token;
            }

            if (PreviousToken == null)
                return token;

            if (token.Value == "(")
            {
                if (PreviousToken.PreviousToken != null && PreviousToken.PreviousToken.Type == TokenType.PreprocessorKeyword)
                    return token;

                if (PreviousToken.Type == TokenType.Qualifier || PreviousToken.Type == TokenType.Statement || PreviousToken.Type.IsTypeName())
                    return token;

                if (PreviousToken.Type == TokenType.Identifier)
                {
                    if (PreviousToken.PreviousToken != null)
                    {
                        // Function declaration
                        if (PreviousToken.PreviousToken.Type.IsTypeName())
                        {
                            PreviousToken.Type = TokenType.FunctionName;
                            LocalFunctionNames.Add(PreviousToken.Value);
                            return token;
                        }

                        // Function call
                        if (PreviousToken.PreviousToken.Type == TokenType.Operator)
                        {
                            if (LocalFunctionNames.Contains(PreviousToken.Value))
                            {
                                PreviousToken.Type = TokenType.FunctionName;
                                return token;
                            }

                            PreviousToken.Type = TokenType.MissingFunctionName;
                            return token;
                        }
                    }
                }
            }

            return token;
        }
    
        private T OnParseNewLine<T>(T token) where T : Token
        {
            m_CurrentLineNumber++;
            return token;
        }
    }
}
