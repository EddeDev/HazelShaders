using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Sprache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Documents;

namespace HazelShaders
{
    internal class GlslClassifier : IClassifier
    {
        private readonly GlslClassifierProvider m_Provider;
        
        private IList<ClassificationSpan> m_Spans = new List<ClassificationSpan>();
        private readonly object m_SpansLock = new object();

        internal GlslClassifier(GlslClassifierProvider provider, ITextBuffer textBuffer)
        {
            m_Provider = provider;

            var observableSnapshot = Observable.Return(textBuffer.CurrentSnapshot).Concat(
                Observable.FromEventPattern<TextContentChangedEventArgs>(eventHandler => textBuffer.Changed += eventHandler, eventHandler => textBuffer.Changed -= eventHandler)
                .Select(eventPattern => eventPattern.EventArgs.After));

            provider.Changed += eventHandler => UpdateSpans(textBuffer);

            observableSnapshot.Throttle(TimeSpan.FromMilliseconds(300.0f));
            observableSnapshot.Subscribe(snapshot => UpdateSpans(textBuffer));
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        private void UpdateSpans(ITextBuffer textBuffer)
        {
            var snapshotSpan = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);
            lock (m_SpansLock)
            {
                m_Spans = m_Provider.CalculateSpans(snapshotSpan);
            }

            ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(snapshotSpan));
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan inputSpan)
        {
            IList<ClassificationSpan> result = new List<ClassificationSpan>();

            IList<ClassificationSpan> currentSpans;
            lock (m_SpansLock)
            {
                currentSpans = m_Spans;
            }

            if (currentSpans.Count == 0)
                return result;

            var translatedInput = inputSpan.TranslateTo(m_Spans[0].Span.Snapshot, SpanTrackingMode.EdgeInclusive);

            foreach (var span in currentSpans)
            {
                if (translatedInput.OverlapsWith(span.Span))
                    result.Add(span);
            }

            return result;
        }
    }
}
