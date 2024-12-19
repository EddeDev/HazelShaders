using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace HazelShaders
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(GlslContentTypes.GlslContentType)]
    [TagType(typeof(TodoTag))]
    class ToDoTaggerProvider : ITaggerProvider
    {
        [Import]
        private readonly IClassifierAggregatorService m_ClassifierAggregatorService = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            return new TodoTagger(m_ClassifierAggregatorService.GetClassifier(buffer)) as ITagger<T>;
        }
    }
}