using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;

namespace HazelShaders
{
#pragma warning disable CS0618

    [Export(typeof(IQuickInfoSourceProvider))]
    [Name("GlslQuickInfo")]
    [Order(Before = "Default Quick Info Presenter")]
    [ContentType(GlslContentTypes.GlslContentType)]
    internal class GlslQuickInfoSourceProvider : IQuickInfoSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        private readonly IClassifierAggregatorService m_ClassifierAggregatorService = null;

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            IClassifier classifier = m_ClassifierAggregatorService.GetClassifier(textBuffer);
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new GlslQuickInfoSource(this, classifier, textBuffer));
        }
    }

    internal class GlslQuickInfoSource : IQuickInfoSource
    {
        private readonly GlslQuickInfoSourceProvider m_Provider;
        private readonly IClassifier m_Classifier;
        private readonly ITextBuffer m_TextBuffer;
        private readonly Dictionary<string, GlslFunctionInfo> m_Dictionary;
        private bool m_IsDisposed;

        public GlslQuickInfoSource(GlslQuickInfoSourceProvider provider, IClassifier classifier, ITextBuffer textBuffer)
        {
            m_Provider = provider;
            m_Classifier = classifier;
            m_TextBuffer = textBuffer;

            m_Dictionary = new Dictionary<string, GlslFunctionInfo>();

            foreach (var kvp in GlslSpecification.KeywordMap)
            {
                if (kvp.Value == TokenType.Function)
                {
                    var functionInfo = GlslFunctions.GetFunctionInfo(kvp.Key);
                    if (functionInfo != null)
                        m_Dictionary[kvp.Key] = functionInfo;
                }
            }
        }

        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                GC.SuppressFinalize(this);
                m_IsDisposed = true;
            }
        }

        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            // Map the trigger point down to our buffer.
            var triggerPoint = session.GetTriggerPoint(m_TextBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                applicableToSpan = null;
                return;
            }

            // TODO: m_Classifier.GetClassificationSpans()

            var currentSnapshot = triggerPoint.Value.Snapshot;
            var querySpan = new SnapshotSpan(triggerPoint.Value, 0);

            var navigator = m_Provider.NavigatorService.GetTextStructureNavigator(m_TextBuffer);
            var extent = navigator.GetExtentOfWord(triggerPoint.Value);
            var searchText = extent.Span.GetText();

            foreach (string key in m_Dictionary.Keys)
            {
                var foundIndex = searchText.IndexOf(key, StringComparison.CurrentCultureIgnoreCase);
                if (foundIndex > -1)
                {
                    applicableToSpan = currentSnapshot.CreateTrackingSpan(extent.Span.Start + foundIndex, key.Length, SpanTrackingMode.EdgeInclusive);

                    GlslFunctionInfo functionInfo;
                    m_Dictionary.TryGetValue(key, out functionInfo);

                    if (functionInfo != null)
                    {
                        var documentationLink = new Hyperlink(new Run("GLSL Documentation"))
                        {
                            NavigateUri = new Uri(functionInfo.DocumentationLink)
                        };

                        documentationLink.RequestNavigate += (sender, args) =>
                        {
                            System.Diagnostics.Process.Start(args.Uri.ToString());
                        };

                        TextBlock textBlock = new TextBlock();
                        textBlock.TextWrapping = System.Windows.TextWrapping.Wrap;
                        // textBlock.Text = value;
                        textBlock.Inlines.AddRange(new Inline[] {
                            new Italic(new Run("(Function) ")),
                            new Bold(new Run(key)),
                            new LineBreak(),
                            new Run(functionInfo.Description),
                            new LineBreak(),
                            documentationLink
                        });

                        textBlock.SetResourceReference(TextBlock.BackgroundProperty, EnvironmentColors.ToolTipBrushKey);
                        textBlock.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);

                        quickInfoContent.Add(textBlock);
                    }
                    else
                    {
                        quickInfoContent.Add("");
                    }

                    return;
                }
            }

            applicableToSpan = null;
        }
    }

    [Export(typeof(IIntellisenseControllerProvider))]
    [Name("ToolTip QuickInfo Controller")]
    [ContentType(GlslContentTypes.GlslContentType)]
    internal class GlslIntellisenseControllerProvider : IIntellisenseControllerProvider
    {
        [Import]
        internal IQuickInfoBroker QuickInfoBroker { get; set; }

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers)
        {
            return new GlslIntellisenseController(textView, subjectBuffers, this);
        }
    }

    internal class GlslIntellisenseController : IIntellisenseController
    {
        private ITextView m_TextView;
        private IList<ITextBuffer> m_SubjectBuffers;
        private GlslIntellisenseControllerProvider m_Provider;
        private IQuickInfoSession m_Session;

        internal GlslIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers, GlslIntellisenseControllerProvider provider)
        {
            m_TextView = textView;
            m_SubjectBuffers = subjectBuffers;
            m_Provider = provider;

            m_TextView.MouseHover += OnTextViewMouseHover;
        }

        public void Detach(ITextView textView)
        {
            if (m_TextView == textView)
            {
                m_TextView.MouseHover -= OnTextViewMouseHover;
                m_TextView = null;
            }
        }

        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            var point = m_TextView.BufferGraph.MapDownToFirstMatch
                 (new SnapshotPoint(m_TextView.TextSnapshot, e.Position),
                PointTrackingMode.Positive,
                snapshot => m_SubjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor);

            if (point == null)
                return;
            
            if (!m_Provider.QuickInfoBroker.IsQuickInfoActive(m_TextView))
            {
                var triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position, PointTrackingMode.Positive);
                m_Session = m_Provider.QuickInfoBroker.TriggerQuickInfo(m_TextView, triggerPoint, true);
            }
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete
}