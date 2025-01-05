using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HazelShaders
{
    // From: https://github.com/KhronosGroup/glslang/blob/3a2834e7702651043ca9f35d022739e740563516/StandAlone/StandAlone.cpp#L128
    enum GlslValidatorFailCode
    {
        Success = 0,
        FailUsage,
        FailCompile,
        FailLink,
        FailCompilerCreate,
        FailThreadCreate,
        FailLinkerCreate
    }

    internal class ErrorTagger : ITagger<IErrorTag>
    {
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private readonly List<ITagSpan<IErrorTag>> m_Tags = new List<ITagSpan<IErrorTag>>();
        private readonly IClassifier m_Classifier;
        private readonly ITextBuffer m_Buffer;
        private readonly string m_FilePath;

        internal ErrorTagger(IClassifier classifier, ITextBuffer buffer)
        {
            m_Classifier = classifier;
            m_Buffer = buffer;

            if (m_Buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                m_FilePath = document.FilePath;

            var observableSnapshot = Observable.Return(buffer.CurrentSnapshot).Concat(
                Observable.FromEventPattern<TextContentChangedEventArgs>(eventHandler => buffer.Changed += eventHandler, eventHandler => buffer.Changed -= eventHandler)
                .Select(eventPattern => eventPattern.EventArgs.After));

            const float compileDelay = 300.0f;
            observableSnapshot.Throttle(TimeSpan.FromMilliseconds(compileDelay)).Subscribe(snapshot => OnSourceCodeChanged(snapshot));

            // Debug.WriteLine($"Creating ErrorTagger for: {m_FilePath}");
        }

        private void OnSourceCodeChanged(ITextSnapshot snapshot)
        {
            // Clear tags
            m_Tags.Clear();

            var shaderSource = snapshot.GetText();
            var sources = ShaderPreprocessor.RemoveCommentsAndSplitSourceCode(shaderSource, out var stageTokens);

            var glslangValidatorPath = GlslangValidator.GetGlslangValidatorPath();
            if (glslangValidatorPath.Length == 0)
                return;

            foreach (var entry in sources)
            {
                ShaderStage stage = entry.Key;
                string stageSource = entry.Value;

                string stageString = stage.ToString().Substring(0, 4).ToLower();

                try
                {
                    using (Process process = new Process())
                    {
                        string filepath = null;
                        if (snapshot.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                            filepath = document.FilePath;
                        var includeDir = Path.GetDirectoryName(filepath);

                        StringBuilder argsBuilder = new StringBuilder();
                        argsBuilder.Append("--stdin ");
                        argsBuilder.Append($"-S {stageString} ");
                        argsBuilder.Append($"-I{includeDir} ");
                        argsBuilder.Append("--client vulkan100 ");
                        argsBuilder.Append("-Od ");
                        argsBuilder.Append(" --keep-uncalled ");

                        // Enable include directives
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
                        if (failCode == GlslValidatorFailCode.FailUsage)
                            Debug.WriteLine($"FailUsage: ({errorData.ToString()}) ({outputData.ToString()})");
                        if (failCode == GlslValidatorFailCode.Success)
                            continue;

                        Dictionary<SnapshotSpan, string> errorMessages = new Dictionary<SnapshotSpan, string>();
                        void AddError(int start, int length, string message)
                        {
                            SnapshotSpan tagSpan = new SnapshotSpan(snapshot, start, length);
                            if (errorMessages.ContainsKey(tagSpan))
                            {
                                errorMessages[tagSpan] += Environment.NewLine;
                                errorMessages[tagSpan] += message;
                            }
                            else
                            {
                                errorMessages.Add(tagSpan, message);
                            }
                        }
                        /*
                        void ClearErrors()
                        {
                            errorMessages.Clear();
                        }
                        */

                        var lines = outputData.ToString().Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var line in lines)
                        {
                            if (line == "stdin")
                                continue;

                            if (line.EndsWith(": compilation terminated "))
                                continue;

                            var match = Regex.Match(line, @"(WARNING|ERROR):\s+(\d|.*):(\d+):\s+(.*)");
                            if (match.Groups.Count != 5)
                                continue;

                            var errorLine = Convert.ToInt32(match.Groups[3].Value);
                            var message = match.Groups[4].Value.Trim();

                            stageTokens.TryGetValue(stage, out var stageToken);
                            var stageStartLine = snapshot.GetLineNumberFromPosition(stageToken.Start);

                            var snapshotLine = snapshot.GetLineFromLineNumber((stageStartLine + errorLine) - 1);
                            string lineString = snapshotLine.GetText();

                            var undeclaredIdentifierMatch = Regex.Match(message, @"'(?<identifier>.*?)'\s:\s*(?<error>undeclared identifier)");
                            if (undeclaredIdentifierMatch.Success)
                            {
                                string identifier = undeclaredIdentifierMatch.Groups["identifier"].Value;
                                string pattern = @"\b" + identifier + @"\b";
                                var identifierMatches = Regex.Matches(lineString, pattern);
                                // ClearErrors();
                                foreach (Match identifierMatch in identifierMatches)
                                    AddError(snapshotLine.Start + identifierMatch.Index, identifierMatch.Length, message);
                                // break;
                                continue;
                            }

                            // 'textur' : no matching overloaded function found
                            var noMatchingOverloadedFunctionFound = Regex.Match(message, @"'(?<function>.*?)'\s:\s*(?<error>no matching overloaded function found)");
                            if (noMatchingOverloadedFunctionFound.Success)
                            {
                                string function = noMatchingOverloadedFunctionFound.Groups["function"].Value;
                                string pattern = @"\b" + function + @"\b";
                                var functionMatches = Regex.Matches(lineString, pattern);
                                // ClearErrors();
                                foreach (Match functionMatch in functionMatches)
                                    AddError(snapshotLine.Start + functionMatch.Index, functionMatch.Length, message);
                                // break;
                                continue;
                            }

                            AddError(snapshotLine.Start + lineString.WhiteSpaceAtStart(), snapshotLine.Length - lineString.WhiteSpaceAtEnd(), message);
                        }

                        foreach (var kvp in errorMessages)
                        {
                            var tagSpan = kvp.Key;
                            var message = kvp.Value;

                            // TODO: add support for warnings
                            m_Tags.Add(new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message)));
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error: {e}");
                }
            }

            var span = new SnapshotSpan(m_Buffer.CurrentSnapshot, 0, m_Buffer.CurrentSnapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) => m_Tags;
    }
}
