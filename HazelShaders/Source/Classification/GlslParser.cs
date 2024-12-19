using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HazelShaders
{
    enum ShaderStage
    {
        None,
        Vertex,
        Fragment,
        Compute
    }

    internal class GlslParser
    {
        public delegate void ChangedEventHandler(object sender);
        public event ChangedEventHandler Changed;

        private readonly Dictionary<TokenType, IClassificationType> m_ClassificationTypes = new Dictionary<TokenType, IClassificationType>();
        private readonly Tokenizer m_Tokenizer = new Tokenizer();

        public GlslParser(IClassificationTypeRegistryService registry)
        {
            m_ClassificationTypes[TokenType.Comment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            m_ClassificationTypes[TokenType.Identifier] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
            m_ClassificationTypes[TokenType.Operator] = registry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
            m_ClassificationTypes[TokenType.QuotedString] = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
            m_ClassificationTypes[TokenType.Number] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
            m_ClassificationTypes[TokenType.PreprocessorKeyword] = registry.GetClassificationType(PredefinedClassificationTypeNames.PreprocessorKeyword);

            m_ClassificationTypes[TokenType.Keyword] = registry.GetClassificationType(GlslClassifierTypes.Keyword);
            m_ClassificationTypes[TokenType.Function] = registry.GetClassificationType(GlslClassifierTypes.Function);
            m_ClassificationTypes[TokenType.Variable] = registry.GetClassificationType(GlslClassifierTypes.Variable);
            m_ClassificationTypes[TokenType.Statement] = registry.GetClassificationType(GlslClassifierTypes.Statement);

            // TODO
            // m_ClassificationTypes[TokenType.Identifier] = registry.GetClassificationType(GlslClassifierTypes.Variable);
        }

        public IList<ClassificationSpan> CalculateSpans(SnapshotSpan span)
        {
            IList<ClassificationSpan> classificationSpans = new List<ClassificationSpan>();
            string source = span.GetText();
            var tokens = m_Tokenizer.Tokenize(source);
            foreach (var token in tokens)
            {
                var lineSpan = new SnapshotSpan(span.Snapshot, token.Start, token.Length);
                var classificationType = m_ClassificationTypes[token.Type];
                classificationSpans.Add(new ClassificationSpan(lineSpan, classificationType));
            }

            List<KeyValuePair<ShaderStage, Token>> stageTokens = new List<KeyValuePair<ShaderStage, Token>>();
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.PreprocessorKeyword)
                {
                    var preprocessorToken = token as PreprocessorToken;
                    if (preprocessorToken.Identifier == "stage")
                    {
                        var stageString = preprocessorToken.Replacement;
                        if (stageString.Length == 0)
                            continue;

                        stageString = stageString.ToLower();
                        char firstChar = Char.ToUpper(stageString[0]);
                        stageString = stageString.Remove(0, 1).Insert(0, firstChar.ToString());

                        if (!Enum.TryParse<ShaderStage>(stageString, out var stage))
                            continue;

                        stageTokens.Add(new KeyValuePair<ShaderStage, Token>(stage, preprocessorToken));
                    }
                }
            }

            stageTokens = stageTokens.OrderBy(x => x.Value.Start).ToList();

            Dictionary<ShaderStage, string> sources = new Dictionary<ShaderStage, string>();
            for (int i = 0; i < stageTokens.Count; i++)
            {
                ShaderStage stage = stageTokens[i].Key;
                
                Token token = stageTokens[i].Value;
                int begin = token.Start + token.Length;

                Token nextToken = i < stageTokens.Count - 1 ? stageTokens[i + 1].Value : null;
                int end = nextToken != null ? nextToken.Start : source.Length;

                string stageSource = source.Substring(begin, end - begin);

                // stageSource = stageSource.Trim();

                // Replace with UNIX line endings
                stageSource = stageSource.Replace("\r\n", "\n");

                // Remove all multiline comments and replace them with empty lines
                while (true)
                {
                    // Regex regex = new Regex(@"/\\*(.|[\\r\\n])*?\\*/");
                    Regex regex = new Regex(@"/\*(.|[\r\n])*?\*/");
                    Match match = regex.Match(stageSource);
                    if (!match.Success)
                        break;
                    
                    int numLineEndings = match.Value.Count(c => c == '\n');
                    stageSource = stageSource.Replace(match.Value, new string('\n', numLineEndings));
                }

                // Remove all line comments and replace them with empty lines
                stageSource = Regex.Replace(stageSource, "//.*", string.Empty);
                
                sources.Add(stage, stageSource);
            }

            return classificationSpans;
        }
    }
}