using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HazelShaders
{
    internal class GlslCompletionSource : ICompletionSource
    {
        private readonly GlslCompletionSourceProvider m_Provider;
        private readonly IClassifier m_Classifier;
        private readonly ITextBuffer m_TextBuffer;
        private bool m_IsDisposed;

        public GlslCompletionSource(GlslCompletionSourceProvider provider, IClassifier classifier, ITextBuffer textBuffer)
        {
            m_Provider = provider;
            m_Classifier = classifier;
            m_TextBuffer = textBuffer;
        }

        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                GC.SuppressFinalize(this);
                m_IsDisposed = true;
            }
        }

        // TODO: Move to specification
        private static bool IsIdentifierChar(char c) => char.IsDigit(c) || (char.IsLetter(c) || '_' == c || '@' == c);

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(GlslCompletionSource));

            var triggerPoint = session.GetTriggerPoint(m_TextBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
                return;

            var completions = new List<Completion>();

            var span = new SnapshotSpan(m_TextBuffer.CurrentSnapshot, new Span(0, triggerPoint.Value.Position));

            // From: https://github.com/danielscherzer/GLSL/blob/9f7d8a99b76e5a5932a2edc4990021271fb57449/GLSL_Shared/CodeCompletion/GlslCompletionSource.cs#L91
            var tokens = m_Classifier.GetClassificationSpans(span);
            var identifiers = from token in tokens
                              where token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier) && !token.Span.Contains(span.End - 1)
                              let text = token.Span.GetText()
                              orderby text
                              select text;
            identifiers = identifiers.Distinct();

            foreach (var identifier in identifiers)
            {
                Completion completion = new Completion(identifier, identifier, null, null, null);
                completions.Add(completion);
            }

            // Global keywords
            completions.AddRange(m_Provider.GlobalKeywordCompletions);

            var line = triggerPoint.Value.GetContainingLine();
            var start = triggerPoint.Value;
            while (start > line.Start && IsIdentifierChar((start - 1).GetChar()))
                start -= 1;
            var applicableTo = m_TextBuffer.CurrentSnapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint.Value), SpanTrackingMode.EdgeInclusive);
            completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
        }
    }
}
