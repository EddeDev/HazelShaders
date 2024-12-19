using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Documents;

#pragma warning disable CS0618 // Type or member is obsolete

namespace HazelShaders
{
    [Export(typeof(IQuickInfoSourceProvider))]
    [Name("ToolTip QuickInfo Source")]
    [Order(Before = "Default Quick Info Presenter")]
    [ContentType(GlslContentTypes.GlslContentType)]
    // TODO: Use IAsyncQuickInfoSourceProvider instead
    internal class GlslQuickInfoSourceProvider : IQuickInfoSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal ITextBufferFactoryService TextBufferFactoryService { get; set; }

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return new GlslQuickInfoSource(this, textBuffer);
        }
    }

    internal class GlslQuickInfoSource : IQuickInfoSource
    {
        private GlslQuickInfoSourceProvider m_Provider;
        private ITextBuffer m_SubjectBuffer;
        private Dictionary<string, GlslFunctionInfo> m_Dictionary;
        private bool m_IsDisposed;

        public GlslQuickInfoSource(GlslQuickInfoSourceProvider provider, ITextBuffer subjectBuffer)
        {
            m_Provider = provider;
            m_SubjectBuffer = subjectBuffer;

            m_Dictionary = new Dictionary<string, GlslFunctionInfo>();

            foreach (var kvp in GlslSpecification.KeywordToTokenTypeMap)
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
            SnapshotPoint? subjectTriggerPoint = session.GetTriggerPoint(m_SubjectBuffer.CurrentSnapshot);
            if (!subjectTriggerPoint.HasValue)
            {
                applicableToSpan = null;
                return;
            }

            ITextSnapshot currentSnapshot = subjectTriggerPoint.Value.Snapshot;
            SnapshotSpan querySpan = new SnapshotSpan(subjectTriggerPoint.Value, 0);

            //look for occurrences of our QuickInfo words in the span
            ITextStructureNavigator navigator = m_Provider.NavigatorService.GetTextStructureNavigator(m_SubjectBuffer);
            TextExtent extent = navigator.GetExtentOfWord(subjectTriggerPoint.Value);
            string searchText = extent.Span.GetText();

            foreach (string key in m_Dictionary.Keys)
            {
                int foundIndex = searchText.IndexOf(key, StringComparison.CurrentCultureIgnoreCase);
                if (foundIndex > -1)
                {
                    applicableToSpan = currentSnapshot.CreateTrackingSpan
                    (
                        //querySpan.Start.Add(foundIndex).Position, 9, SpanTrackingMode.EdgeInclusive
                        extent.Span.Start + foundIndex, key.Length, SpanTrackingMode.EdgeInclusive
                    );

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
    internal class GlslQuickInfoControllerProvider : IIntellisenseControllerProvider
    {
        [Import]
        internal IQuickInfoBroker QuickInfoBroker { get; set; }

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers)
        {
            return new GlslQuickInfoController(textView, subjectBuffers, this);
        }
    }

    internal class GlslQuickInfoController : IIntellisenseController
    {
        private ITextView m_textView;
        private IList<ITextBuffer> m_subjectBuffers;
        private GlslQuickInfoControllerProvider m_provider;
        private IQuickInfoSession m_session;

        internal GlslQuickInfoController(ITextView textView, IList<ITextBuffer> subjectBuffers, GlslQuickInfoControllerProvider provider)
        {
            m_textView = textView;
            m_subjectBuffers = subjectBuffers;
            m_provider = provider;

            m_textView.MouseHover += this.OnTextViewMouseHover;
        }

        public void Detach(ITextView textView)
        {
            if (m_textView == textView)
            {
                m_textView.MouseHover -= this.OnTextViewMouseHover;
                m_textView = null;
            }
        }

        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            //find the mouse position by mapping down to the subject buffer
            SnapshotPoint? point = m_textView.BufferGraph.MapDownToFirstMatch
                 (new SnapshotPoint(m_textView.TextSnapshot, e.Position),
                PointTrackingMode.Positive,
                snapshot => m_subjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor);

            if (point != null)
            {
                ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position,
                PointTrackingMode.Positive);

                if (!m_provider.QuickInfoBroker.IsQuickInfoActive(m_textView))
                {
                    m_session = m_provider.QuickInfoBroker.TriggerQuickInfo(m_textView, triggerPoint, true);
                }
            }
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }
    }
}

#pragma warning restore CS0618 // Type or member is obsolete