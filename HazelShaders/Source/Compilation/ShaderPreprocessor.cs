using Sprache;
using Microsoft.VisualStudio.Package;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text;
using System.Diagnostics;
using System.IO;
using Microsoft.ServiceHub.Resources;
using System.Windows.Media;

namespace HazelShaders
{
    using ShaderSourceMap = Dictionary<ShaderStage, string>;

    struct ShaderStageToken
    {
        public ShaderStage Stage;
        public int Start;
        public int Length;

        public ShaderStageToken(ShaderStage stage, int start, int length)
        {
            Stage = stage;
            Start = start;
            Length = length;
        }
    }

    internal class ShaderPreprocessor
    {
        // TODO: there might whitespace between # and stage
        private const string StageToken = "#stage";


        /*
        // Preprocess shader source using glslangValidator
        public static string GlslangPreprocess(ITextSnapshot snapshot, IList<ClassificationSpan> classificationSpans)
        {
            var sources = new ShaderSourceMap(); // Preprocess(snapshot, classificationSpans, out var stageTokenPositions);




            var outputSources = new ShaderSourceMap();

            foreach (var entry in sources)
            {
                var stage = entry.Key;
                var stageSource = entry.Value;

                string stageSourceTempFile = Path.GetTempFileName();
                File.WriteAllText(stageSourceTempFile, stageSource);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "glslangValidator",
                        Arguments = "",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                File.Delete(stageSourceTempFile);

                outputSources.Add(stage, output);
            }

            StringBuilder sb = new StringBuilder();
            foreach (var entry in outputSources)
            {
                sb.Append(entry.Value);
            }
            return sb.ToString();
        }
        */



        /*
        private static string ProcessIncludes(string input, string includeDir)
        {
            var lines = input.Split(new char[] { '\n' }, StringSplitOptions.None);
            string output = input;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Regex includeRegex = new Regex(@"#include\s*[""<](.*?)["">]");
                var match = includeRegex.Match(line);
                if (match.Success)
                {
                    var filepath = match.Groups[1].Value;
                    string includePath = Path.Combine(includeDir, filepath);
                    string directoryName = Path.GetDirectoryName(includePath);
                    string includeContent = ProcessIncludes(File.ReadAllText(includePath), directoryName);
                    output = output.Replace(match.Value, includeContent + "\n" + $"#line {i + 1}");
                }
            }
            return output;
        }
        */

        public static ShaderSourceMap RemoveCommentsAndSplitSourceCode(string shaderSource, out Dictionary<ShaderStage, ShaderStageToken> outStageTokens)
        {
            // Replace with UNIX line endings
            shaderSource = shaderSource.Replace("\r\n", " \n");

            // Remove all multiline comments and replace them with empty lines
            while (true)
            {
                // Regex regex = new Regex(@"/\\*(.|[\\r\\n])*?\\*/");
                Regex regex = new Regex(@"/\*(.|[\r\n])*?\*/");
                Match match = regex.Match(shaderSource);
                if (!match.Success)
                    break;

                string comment = match.Value;
                int numLineEndings = comment.Count(c => c == '\n');
                string replacement = string.Concat(comment.Select(c => c == '\n' ? '\n' : ' '));
                shaderSource = shaderSource.Replace(comment, replacement);
            }

            // Remove all line comments with whitespace
            shaderSource = Regex.Replace(shaderSource, "//.*", match => new string(' ', match.Length));

            outStageTokens = new Dictionary<ShaderStage, ShaderStageToken>();

            var sources = new ShaderSourceMap();

            int stageTokenPos = shaderSource.IndexOf(StageToken);
            while (stageTokenPos != -1)
            {
                int eol = shaderSource.FindFirstOf("\n", stageTokenPos);
                if (eol == -1)
                    break;

                int begin = stageTokenPos + StageToken.Length;
                string stageString = shaderSource.Substring(begin, eol - begin);
                stageString = stageString.Trim();
                if (stageString.Length == 0)
                    break;

                stageString = stageString.ToLower();
                char firstChar = Char.ToUpper(stageString[0]);
                stageString = stageString.Remove(0, 1).Insert(0, firstChar.ToString());
                if (!Enum.TryParse<ShaderStage>(stageString, out var stage))
                    continue;

                int nextLinePos = shaderSource.FindFirstNotOf("\n", eol);

                // Add token to dictionary
                outStageTokens.Add(stage, new ShaderStageToken(stage, stageTokenPos, eol - 1 - stageTokenPos));

                if (nextLinePos == -1)
                    break;

                stageTokenPos = shaderSource.IndexOf(StageToken, nextLinePos);

                int end = stageTokenPos != -1 ? stageTokenPos : shaderSource.Length;

                string stageSource = shaderSource.Substring(eol, end - eol);
                sources.Add(stage, stageSource);
            }

            return sources;
        }
    }
}
