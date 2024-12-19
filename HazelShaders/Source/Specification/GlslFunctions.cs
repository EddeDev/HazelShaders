using System.Collections.Generic;
using System.Text;

namespace HazelShaders
{
    public class GlslFunctionInfo
    {
        public string Name;
        public string[] Declaration;
        public string[] Parameters;
        public string Description;
        public string DocumentationLink;

        public GlslFunctionInfo(string name, string[] declaration, string[] parameters, string description, string documentationLink)
        {
            Name = name;
            Declaration = declaration;
            Parameters = parameters;
            Description = description;
            DocumentationLink = documentationLink;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Name:");
            sb.AppendLine();
            sb.Append(Name);
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Declaration:");
            sb.AppendLine();
            foreach (var v in Declaration)
            {
                sb.Append(v);
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.Append("Parameters:");
            sb.AppendLine();
            for (int i = 0; i < Parameters.Length; i += 2)
            {
                sb.Append(Parameters[i + 0]);
                sb.Append(": ");
                sb.Append(Parameters[i + 1]);
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.Append("Description:");
            sb.AppendLine();
            sb.Append(Description);
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(DocumentationLink);
            return sb.ToString();
        }
    }

    internal class GlslFunctions
    {
        private static Dictionary<string, GlslFunctionInfo> Functions = new Dictionary<string, GlslFunctionInfo>()
        {
            {
                "min",
                new GlslFunctionInfo(
                    "min - return the lesser of two values",
                    new string[]
                    {
                        "TODO"
                    },
                    new string[]
                    {
                        "x", "Specify the first value to compare",
                        "y", "Specify the second value to compare."
                    },
                    "returns the minimum of the two parameters. It returns y if y is less than x, otherwise it returns x.",
                    "https://registry.khronos.org/OpenGL-Refpages/gl4/html/min.xhtml"
                )
            },
            {
                "max",
                new GlslFunctionInfo(
                    "max - return the greater of two values",
                    new string[]
                    {
                        "TODO"
                    },
                    new string[]
                    {
                        "x", "Specify the first value to compare",
                        "y", "Specify the second value to compare."
                    },
                    "returns the maximum of the two parameters. It returns y if y is greater than x, otherwise it returns x.",
                    "https://registry.khronos.org/OpenGL-Refpages/gl4/html/max.xhtml"
                )
            },
            {
                "normalize", 
                new GlslFunctionInfo(
                    "normalize - calculates the unit vector in the same direction as the original vector",
                    new string[]
                    {
                        "genType normalize(genType v);",
                        "genDType normalize(genDType v);"
                    },
                    new string[]
                    {
                        "v", "Specifies the vector to normalize."
                    },
                    "returns a vector with the same direction as its parameter, v, but with length 1.",
                    "https://registry.khronos.org/OpenGL-Refpages/gl4/html/normalize.xhtml"
                )
            }
        };

        public static GlslFunctionInfo GetFunctionInfo(string name)
        {
            if (Functions.TryGetValue(name, out GlslFunctionInfo functionInfo))
                return functionInfo;
            return null;
        }
    }
}
