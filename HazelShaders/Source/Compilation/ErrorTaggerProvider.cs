using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Classification;
using System.Diagnostics;

namespace HazelShaders
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(GlslContentTypes.GlslContentType)]
    [TagType(typeof(ErrorTag))]
    internal class ErrorTaggerProvider : IViewTaggerProvider
    {
        [Import]
        private readonly IClassifierAggregatorService m_ClassifierAggregatorService = null;
        
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (!ReferenceEquals(buffer, textView.TextBuffer))
                return null;

            return buffer.Properties.GetOrCreateSingletonProperty(() => new ErrorTagger(m_ClassifierAggregatorService.GetClassifier(buffer), buffer)) as ITagger<T>;
        }
    }
}
