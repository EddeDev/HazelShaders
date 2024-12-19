using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace HazelShaders
{
    internal class TodoTag : IGlyphTag {}

    internal class TodoTagger : ITagger<TodoTag>
    {
        private readonly IClassifier m_Classifier;

        private const string m_SearchText = "todo";

        internal TodoTagger(IClassifier classifier)
        {
            m_Classifier = classifier;
        }

        IEnumerable<ITagSpan<TodoTag>> ITagger<TodoTag>.GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan span in spans)
            {
                foreach (ClassificationSpan classification in m_Classifier.GetClassificationSpans(span))
                {
                    if (classification.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Comment))
                    {
                        int index = span.GetText().ToLower().IndexOf(m_SearchText);
                        if (index > -1)
                        {
                            SnapshotSpan todoSpan = new SnapshotSpan(span.Snapshot, new Span(span.Start + index, m_SearchText.Length));
                            yield return new TagSpan<TodoTag>(todoSpan, new TodoTag());
                        }
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }
    }
}