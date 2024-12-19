using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace HazelShaders
{
    internal class GlslCompletionSource : ICompletionSource
    {
        private ITextBuffer m_TextBuffer;

        public GlslCompletionSource(ITextBuffer textBuffer)
        {
            m_TextBuffer = textBuffer;
        }

        public void Dispose()
        {

        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
        }
    }
}
