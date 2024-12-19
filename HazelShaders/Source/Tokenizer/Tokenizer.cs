using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HazelShaders
{
    class Parsers
    {
        private static readonly Parser<Token> Comment = from value in new CommentParser().AnyComment
                                                        select new Token(TokenType.Comment, value);

        private static readonly Parser<Token> Preprocessor = from open in Parse.Char('#').Token()
                                                             from identifier in Parse.Letter.Once().Text().Then(first => Parse.LetterOrDigit.Many().Text().Select(rest => first + rest))
                                                             from replacement in Parse.CharExcept("\"\r\n").Many().Text().Token()
                                                             select new PreprocessorToken(identifier, replacement);

        private static readonly Parser<Token> QuotedString = from open in Parse.Char('"')
                                                             from value in Parse.CharExcept("\"\r\n").Many().Text()
                                                             from close in Parse.Char('"')
                                                             select new Token(TokenType.QuotedString, open + value + close);

        private static readonly Parser<Token> Number = from value in Parse.DecimalInvariant
                                                       select new Token(TokenType.Number, value);

        private static readonly Parser<Token> Identifier = from value in Parse.Identifier(Parse.Char((char c) => char.IsLetter(c) || '_' == c || '@' == c, "Identifier start"), Parse.Char((char c) => char.IsDigit(c) || (char.IsLetter(c) || '_' == c || '@' == c), "Identifier character"))
                                                           select new Token(GlslSpecification.KeywordToTokenType(value), value);

        private static readonly Parser<Token> Operator = from value in Parse.Chars(".,;+-*/?:!&|^{}()[]<>=\\")
                                                         select new Token(TokenType.Operator, value.ToString());

        public static Parser<Token> CreateParser(TokenType type)
        {
            switch (type)
            {
                case TokenType.Comment: return Comment;
                case TokenType.PreprocessorKeyword: return Preprocessor;
                case TokenType.QuotedString: return QuotedString;
                case TokenType.Number: return Number;
                case TokenType.Identifier: return Identifier;
                case TokenType.Operator: return Operator;
            }
            return null;
        }
    }

    public class Tokenizer
    {
        private readonly Parser<IEnumerable<Token>> m_Parser;

        public Tokenizer()
        {
            Parser<Token> token = null;
            foreach (TokenType tokenType in Enum.GetValues(typeof(TokenType)))
            {
                Parser<Token> parser = Parsers.CreateParser(tokenType);
                if (parser == null)
                    continue;
                if (token == null)
                    token = parser;
                token = token.Or(parser);
            }
            m_Parser = token.Positioned().Token().Many(); // .XMany();
        }

        public IEnumerable<IToken> Tokenize(string text)
        {
            if (text.Trim().Length == 0)
                yield break;

            var tokens = m_Parser.TryParse(text);
            if (tokens.WasSuccessful)
            {
                foreach (var token in tokens.Value)
                    yield return token;
            }
        }
    }
}
