using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace HazelShaders
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(GlslContentTypes.GlslContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class GlslClassifierProvider : IClassifierProvider
    {
        [Import]
        private IClassificationTypeRegistryService m_ClassificationRegistry;
        
        private GlslParser m_Parser;

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            if (m_Parser == null)
            {
                m_Parser = new GlslParser(m_ClassificationRegistry);
            }

            return buffer.Properties.GetOrCreateSingletonProperty(() => new GlslClassifier(buffer, m_Parser));
        }
    }
}
