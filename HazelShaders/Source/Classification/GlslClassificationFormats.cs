using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace HazelShaders
{
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = GlslClassificationTypes.Keyword)]
    [Name(nameof(GlslKeywordClassifierFormat))]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class GlslKeywordClassifierFormat : ClassificationFormatDefinition
    {
        public GlslKeywordClassifierFormat()
        {
            DisplayName = "GLSL Keyword";
            ForegroundColor = ColorConverter.ConvertFromString("#569CD6") as Color?;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = GlslClassificationTypes.Function)]
    [Name(nameof(GlslFunctionClassifierFormat))]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class GlslFunctionClassifierFormat : ClassificationFormatDefinition
    {
        public GlslFunctionClassifierFormat()
        {
            DisplayName = "GLSL Function";
            ForegroundColor = ColorConverter.ConvertFromString("#FF8000") as Color?;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = GlslClassificationTypes.Variable)]
    [Name(nameof(GlslVariableClassifierFormat))]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class GlslVariableClassifierFormat : ClassificationFormatDefinition
    {
        public GlslVariableClassifierFormat()
        {
            DisplayName = "GLSL Variable";
            ForegroundColor = ColorConverter.ConvertFromString("#BDB76B") as Color?;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = GlslClassificationTypes.TypeName)]
    [Name(nameof(GlslTypeNameClassifierFormat))]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class GlslTypeNameClassifierFormat : ClassificationFormatDefinition
    {
        public GlslTypeNameClassifierFormat()
        {
            DisplayName = "GLSL TypeName";
            ForegroundColor = ColorConverter.ConvertFromString("#FFD700") as Color?;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = GlslClassificationTypes.Statement)]
    [Name(nameof(GlslStatementClassifierFormat))]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class GlslStatementClassifierFormat : ClassificationFormatDefinition
    {
        public GlslStatementClassifierFormat()
        {
            DisplayName = "GLSL Statement";
            ForegroundColor = ColorConverter.ConvertFromString("#D1A0D4") as Color?;
        }
    }

}
