using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace HazelShaders
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType(GlslContentTypes.GlslContentType)]
    [Name("GLSLCompletion")]
    internal class GlslCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        private readonly IClassifierAggregatorService m_ClassifierAggregatorService = null;

        [Import]
        private readonly IGlyphService m_GlyphService = null;

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            var classifier = m_ClassifierAggregatorService.GetClassifier(textBuffer);
            return new GlslCompletionSource(textBuffer);
        }
    }
}
