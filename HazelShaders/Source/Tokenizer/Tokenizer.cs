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
        MissingIdentifier,
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

        public static bool IsIdentifier(this TokenType type)
        {
            return type == TokenType.Identifier || type == TokenType.MissingIdentifier;
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

    static class TokenExtensions
    {
        public static bool HasTypeAndValue(this Token token, TokenType type, string value)
        {
            return token.Type == type && token.Value == value;
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
        public static T Update<T>(this T token, Tokenizer tokenizer, bool updatePreviousToken = true) where T : Token
        {
            token = tokenizer.OnParseToken(token);
            token.PreviousToken = tokenizer.PreviousToken;
            if (updatePreviousToken)
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

        public struct Variable
        {
            public string Name { get; set; }
            public int StackDepth { get; set; }
        }
        public List<Variable> LocalVariables { get; private set; }
        public struct Uniform
        {
            public string Name { get; set; }
            public int StackDepth { get; set; }
        }
        public List<Uniform> LocalUniforms { get; private set; }

        public class Function
        {
            public string Name { get; set; }
            public Token OpenParen { get; set; }
            public Token CloseParen { get; set; }
        }
        public List<Function> LocalFunctions { get; private set; }

        private int m_CurrentScopeDepth = 0;
        private int m_CurrentLineNumber = 0;

        private int m_CurrentFunctionIndex = -1;

        private string m_Filepath { get; set; }

        public Tokenizer()
        {
            LocalStructNames = new List<string>();
            LocalFunctions = new List<Function>();
            LocalVariables = new List<Variable>();
            LocalUniforms = new List<Uniform>();
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
            LocalFunctions.Clear();
            LocalVariables.Clear();
            LocalUniforms.Clear();
            PreviousToken = null;
            m_CurrentScopeDepth = 0;
            m_CurrentFunctionIndex = -1;
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
                             select new Token(TokenType.Comment, value); //.Update(this, false);
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
                             select new NewlineToken(carriageReturn.IsDefined, newLine.IsDefined); //.Update(this, false);
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

        private bool IsKeywordDefinedLocally(string name)
        {
            foreach (var variable in LocalVariables)
            {
                if (variable.Name == name)
                    return true;
            }
            foreach (var structName in LocalStructNames)
            {
                if (structName == name)
                    return true;
            }
            foreach (var function in LocalFunctions)
            {
                if (function.Name == name)
                    return true;
            }
            return false;
        }

        private T OnParseIdentifier<T>(T token) where T : Token
        {
            if (PreviousToken == null)
            {
                token.Type = GlslSpecification.KeywordToTokenType(token.Value);
                return token;
            }

            // Example:
            //
            // #endif
            // 
            //
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

            if (PreviousToken.Type == TokenType.Qualifier)
            {
                if (PreviousToken.Value == "uniform")
                {
                    token.Type = TokenType.TypeName;

                    Uniform uniform = new Uniform();
                    uniform.Name = token.Value;
                    uniform.StackDepth = m_CurrentScopeDepth;
                    LocalUniforms.Add(uniform);
                    return token;
                }
            }

            // Example: DirectionalLight dirLight;
            // "DirectionalLight" is a custom struct (type name)
            // dirLight is a normal identifier
            // TODO: what about layout qualifiers?
            if (PreviousToken.Type == TokenType.Identifier)
            {
                if (LocalStructNames.Contains(PreviousToken.Value))
                {
                    PreviousToken.Type = TokenType.TypeName;
                }
            }
            
            if (PreviousToken.Type == TokenType.TypeName)
            {
                Variable variable = new Variable();
                variable.Name = token.Value;
                variable.StackDepth = m_CurrentScopeDepth;
                LocalVariables.Add(variable);

                token.Type = TokenType.Identifier;
                return token;
            }

            token.Type = GlslSpecification.KeywordToTokenType(token.Value);

            if (token.Type == TokenType.Identifier)
            {
                if (!IsKeywordDefinedLocally(token.Value))
                {
                    token.Type = TokenType.MissingIdentifier;
                }
            }

            // Example: vec2 imageSize;
            // "imageSize" is a built in function but GLSL allows you
            // to write code like this
            if (!token.Type.IsIdentifier() && PreviousToken.Type.IsTypeName())
                token.Type = TokenType.Identifier;
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

                        Tokenizer tokenizer = new Tokenizer();
                        tokenizer.Tokenize(includeContent, includePath);

                        LocalStructNames.AddRange(tokenizer.LocalStructNames);
                        LocalFunctions.AddRange(tokenizer.LocalFunctions);
                        LocalVariables.AddRange(tokenizer.LocalVariables);
                        LocalUniforms.AddRange(tokenizer.LocalUniforms);
                    }

                    return token;
                }
            }

            return token;
        }

        private static bool SkipAllCommentsAndNewlines(ref Token t)
        {
            bool skipped = false;
            while (true)
            {
                if (t == null || (t.Type != TokenType.Comment && t.Type != TokenType.NewLine))
                    break;
                t = t.PreviousToken;
                skipped = true;
            }
            return skipped;
        }

        private static IEnumerable<Token> ExtractFunctionParameters(Token openParenToken, Token closeParenToken)
        {
            List<Token> tokens = new List<Token>();
            if (openParenToken == null || closeParenToken == null)
                return tokens;

            var it = closeParenToken;
            while (it != openParenToken)
            {
                if (it != openParenToken && it != closeParenToken)
                    tokens.Add(it);
                it = it.PreviousToken;
            }

            tokens.Reverse();
            return tokens;
        }

        private Function GetCurrentFunction()
        {
            if (m_CurrentFunctionIndex == -1)
                return null;

            if (m_CurrentFunctionIndex >= LocalFunctions.Count)
                return null;

            return LocalFunctions.ElementAt(m_CurrentFunctionIndex);
        }

        private T OnParseOperator<T>(T token) where T : Token
        {
            if (token.Value == "{")
            {
                m_CurrentScopeDepth++;

                if (m_CurrentScopeDepth == 1 && m_CurrentFunctionIndex == -1)
                {
                    Token openParen = null;
                    Token closeParen = null;

                    var it = PreviousToken;
                    while (it != null)
                    {
                        SkipAllCommentsAndNewlines(ref it);
                        if (it == null)
                            break;

                        if (closeParen == null && it.HasTypeAndValue(TokenType.Operator, ")"))
                        {
                            closeParen = it;
                        }
                        else if (openParen == null && it.HasTypeAndValue(TokenType.Operator, "("))
                        {
                            openParen = it;
                        }
                        else if (openParen != null && closeParen != null)
                        {
                            if (it.Type == TokenType.FunctionName)
                            {
                                Function function = LocalFunctions.Find((f) => { return f.Name == it.Value; });
                                if (function != null)
                                {
                                    var parameters = ExtractFunctionParameters(openParen, closeParen);

                                    function.OpenParen = openParen;
                                    function.CloseParen = closeParen;

                                    m_CurrentFunctionIndex = LocalFunctions.IndexOf(function);
                                }

                                return token;
                            }
                        }

                        it = it.PreviousToken;
                    }

                    it = PreviousToken;
                    while (it != null)
                    {
                        SkipAllCommentsAndNewlines(ref it);
                        if (it == null)
                            break;

                        Debug.WriteLine("a");

                        it = it.PreviousToken;
                    }
                }

                return token;
            }

            if (token.Value == "}")
            {
                if (m_CurrentScopeDepth == 1)
                {
                    if (m_CurrentFunctionIndex != -1)
                    {
                        // End of function
                        m_CurrentFunctionIndex = -1;
                    }
                }

                m_CurrentScopeDepth--;
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


                            Function function = new Function();
                            function.Name = PreviousToken.Value;
                            LocalFunctions.Add(function);

                            return token;
                        }

                        // Function call
                        if (PreviousToken.PreviousToken.Type == TokenType.Operator)
                        {
                            Function function = LocalFunctions.Find((f) => { return f.Name == PreviousToken.Value; });
                            if (function != null)
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
