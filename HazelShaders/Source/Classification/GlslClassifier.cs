using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace HazelShaders
{
    internal class GlslClassifier : IClassifier
    {
        private IList<ClassificationSpan> m_Spans = new List<ClassificationSpan>();
        private readonly object m_SpansLock = new object();

        private GlslParser m_Parser;

        internal GlslClassifier(ITextBuffer buffer, GlslParser parser)
        {
            m_Parser = parser;

            var observableSnapshot = Observable.Return(buffer.CurrentSnapshot).Concat(
                Observable.FromEventPattern<TextContentChangedEventArgs>(eventHandler => buffer.Changed += eventHandler, eventHandler => buffer.Changed -= eventHandler)
                .Select(eventPattern => eventPattern.EventArgs.After));

            parser.Changed += eventHandler => UpdateSpans(buffer);

            observableSnapshot.Throttle(TimeSpan.FromSeconds(0.3f));
            observableSnapshot.Subscribe(snapshot => UpdateSpans(buffer));
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        private void UpdateSpans(ITextBuffer buffer)
        {
            var snapshotSpan = new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length);
            lock (m_SpansLock)
            {
                m_Spans = m_Parser.CalculateSpans(snapshotSpan);
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
