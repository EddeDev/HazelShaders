using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace HazelShaders
{
    internal static class GlslClassificationTypes
    {
        public const string Keyword = nameof(GLSL_Keyword);
        public const string Function = nameof(GLSL_Function);
        public const string Variable = nameof(GLSL_Variable);
        public const string Statement = nameof(GLSL_Statement);

        [Export]
        [Name(Keyword)]
        private static ClassificationTypeDefinition GLSL_Keyword;

        [Export]
        [Name(Function)]
        private static ClassificationTypeDefinition GLSL_Function;

        [Export]
        [Name(Variable)]
        private static ClassificationTypeDefinition GLSL_Variable;

        [Export]
        [Name(Statement)]
        private static ClassificationTypeDefinition GLSL_Statement;
    }
}
