using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace HazelShaders
{
    internal static class GlslContentTypes
    {
        public const string GlslContentType = "glsl";

        [Export(typeof(ContentTypeDefinition))]
        [Name(GlslContentType)]
        [BaseDefinition("code")]
        private static ContentTypeDefinition ContentTypeDef;

        [Export(typeof(FileExtensionToContentTypeDefinition))]
        [ContentType(GlslContentType)]
        [FileExtension(".glsl")]
        private static FileExtensionToContentTypeDefinition FileExtensionDef;
    }
}
