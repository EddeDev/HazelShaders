using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazelShaders
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType(GlslContentTypes.GlslContentType)]
    [Name("GLSLCompletionSourceProvider")]
    internal class GlslCompletionSourceProvider : ICompletionSourceProvider
    {
        public readonly List<Completion> GlobalKeywordCompletions = new List<Completion>();

        public readonly Dictionary<TokenType, ImageSource> GlyphMap = new Dictionary<TokenType, ImageSource>();

        private readonly IClassifierAggregatorService m_ClassifierAggregatorService = null;

        [ImportingConstructor]
        public GlslCompletionSourceProvider([Import] IClassifierAggregatorService classifierAggregatorService, [Import] IGlyphService glyphService)
        {
            m_ClassifierAggregatorService = classifierAggregatorService;

            Debug.Assert(glyphService != null);

            GlyphMap[TokenType.Keyword] = glyphService.GetGlyph(StandardGlyphGroup.GlyphKeyword, StandardGlyphItem.GlyphItemPublic);
            GlyphMap[TokenType.Function] = glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic);
            GlyphMap[TokenType.Variable] = glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupVariable, StandardGlyphItem.GlyphItemPublic);
            GlyphMap[TokenType.Identifier] = glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupVariable, StandardGlyphItem.GlyphItemFriend);

            foreach (var kvp in GlslSpecification.KeywordMap)
            {
                if (!GlyphMap.TryGetValue(kvp.Value, out var imageSource))
                    imageSource = GlyphMap[TokenType.Identifier];
                GlobalKeywordCompletions.Add(new Completion(kvp.Key, kvp.Key, null, imageSource, null));
            }
        }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            var classifier = m_ClassifierAggregatorService.GetClassifier(textBuffer);
            return new GlslCompletionSource(this, classifier, textBuffer);
        }
    }
}
