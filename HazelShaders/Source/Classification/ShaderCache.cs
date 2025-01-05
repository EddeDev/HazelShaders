using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HazelShaders
{
    internal class ShaderCache
    {
        private static Dictionary<int, string> ShaderSourceMap = new Dictionary<int, string>();

        public static string GetCachedPreprocessedSourceCode(SnapshotSpan span)
        {
            var hashCode = span.Snapshot.GetText().GetHashCode();
            if (ShaderSourceMap.TryGetValue(hashCode, out var cachedSource))
                return cachedSource;

            var glslangValidatorPath = GlslangValidator.GetGlslangValidatorPath();
            if (glslangValidatorPath.Length == 0)
                return "";

            var sources = ShaderPreprocessor.RemoveCommentsAndSplitSourceCode(span.GetText(), out var stageTokenPositions);
            var outputSources = new Dictionary<ShaderStage, string>();
            foreach (var entry in sources)
            {
                var stage = entry.Key;
                var stageSource = entry.Value;

                try
                {
                    using (Process process = new Process())
                    {
                        string filepath = null;
                        if (span.Snapshot.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                            filepath = document.FilePath;
                        var includeDir = Path.GetDirectoryName(filepath);

                        StringBuilder argsBuilder = new StringBuilder();
                        argsBuilder.Append("--stdin ");
                        argsBuilder.Append($"-S {stage.ToString().Substring(0, 4).ToLower()} ");
                        argsBuilder.Append($"-I{includeDir} ");
                        argsBuilder.Append("-E ");
                        argsBuilder.Append("-P\"#extension GL_GOOGLE_include_directive : enable\n\"");

                        process.StartInfo.FileName = glslangValidatorPath;
                        process.StartInfo.Arguments = argsBuilder.ToString();
                        process.StartInfo.RedirectStandardInput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;

                        StringBuilder errorData = new StringBuilder();
                        StringBuilder outputData = new StringBuilder();
                        process.ErrorDataReceived += (sender, args) => errorData.AppendLine(args.Data);
                        process.OutputDataReceived += (sender, args) => outputData.AppendLine(args.Data);

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.StandardInput.Write(stageSource);
                        process.StandardInput.Close();

                        // Wait for exit
                        process.WaitForExit();

                        GlslValidatorFailCode failCode = (GlslValidatorFailCode)process.ExitCode;

                        if (outputData.Length > 0)
                            outputSources[stage] = outputData.ToString();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error: {e}");
                }
            }

            StringBuilder sb = new StringBuilder();
            foreach (var entry in outputSources)
                sb.Append(entry.Value);
            string result = sb.ToString();
            ShaderSourceMap.Add(hashCode, result);
            return result;
        }
    }
}
