using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace HazelShaders
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(GlslContentTypes.GlslContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class GlslClassifierProvider : IClassifierProvider
    {
        public delegate void ChangedEventHandler(object sender);
        public event ChangedEventHandler Changed;

        private readonly IClassificationTypeRegistryService m_ClassificationRegistry = null;
        private readonly Dictionary<TokenType, IClassificationType> m_ClassificationTypes = new Dictionary<TokenType, IClassificationType>();

        private readonly Tokenizer m_Tokenizer = new Tokenizer();

        [ImportingConstructor]
        public GlslClassifierProvider([Import] IClassificationTypeRegistryService registry)
        {
            m_ClassificationRegistry = registry;

            m_ClassificationTypes[TokenType.Comment] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            m_ClassificationTypes[TokenType.Identifier] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
            m_ClassificationTypes[TokenType.Operator] = registry.GetClassificationType(PredefinedClassificationTypeNames.Operator);
            m_ClassificationTypes[TokenType.QuotedString] = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
            m_ClassificationTypes[TokenType.Number] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
            m_ClassificationTypes[TokenType.PreprocessorKeyword] = registry.GetClassificationType(PredefinedClassificationTypeNames.PreprocessorKeyword);

            m_ClassificationTypes[TokenType.Keyword] = registry.GetClassificationType(GlslClassificationTypes.Keyword);
            m_ClassificationTypes[TokenType.Variable] = registry.GetClassificationType(GlslClassificationTypes.Variable);
            m_ClassificationTypes[TokenType.Statement] = registry.GetClassificationType(GlslClassificationTypes.Statement);

            m_ClassificationTypes[TokenType.FunctionName] = registry.GetClassificationType(GlslClassificationTypes.Function);
            m_ClassificationTypes[TokenType.MissingFunctionName] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);

            // TODO: WIP 
            // m_ClassificationTypes[TokenType.StructName] = registry.GetClassificationType(GlslClassificationTypes.TypeName);
            m_ClassificationTypes[TokenType.TypeName] = registry.GetClassificationType(GlslClassificationTypes.TypeName);
            m_ClassificationTypes[TokenType.BaseTypeName] = registry.GetClassificationType(GlslClassificationTypes.Keyword);

            // TODO: WIP WIP WIP
            m_ClassificationTypes[TokenType.Identifier] = registry.GetClassificationType(GlslClassificationTypes.Variable);
            m_ClassificationTypes[TokenType.MissingIdentifier] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);

            // Idk about this
            m_ClassificationTypes[TokenType.Qualifier] = registry.GetClassificationType(GlslClassificationTypes.Keyword);
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new GlslClassifier(this, textBuffer));
        }

        public IList<ClassificationSpan> CalculateSpans(SnapshotSpan span)
        {
            string filepath = null;
            if (span.Snapshot.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                filepath = document.FilePath;

            IList<ClassificationSpan> classificationSpans = new List<ClassificationSpan>();
            string source = span.GetText();
            var tokens = m_Tokenizer.Tokenize(source, filepath);
            foreach (var token in tokens)
            {
                var tokenSpan = new SnapshotSpan(span.Snapshot, token.StartPos.Pos, token.Length);
                var classificationType = m_ClassificationTypes[token.Type];
                classificationSpans.Add(new ClassificationSpan(tokenSpan, classificationType));
            }
            return classificationSpans;
        }
    }
}
