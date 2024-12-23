using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

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

            const float compileDelay = 200.0f;
            observableSnapshot.Throttle(TimeSpan.FromMilliseconds(compileDelay)).Subscribe(snapshot => OnSourceCodeChanged(snapshot));

            Debug.WriteLine($"Creating ErrorTagger for: {m_FilePath}");
        }

        private static int WhiteSpaceAtBegin(string str)
        {
            int count = 0;
            int ix = 0;
            while (Char.IsWhiteSpace(str[ix++]) && ix < str.Length)
                count++;
            return count;
        }

        private static int WhiteSpaceAtEnd(string str)
        {
            int count = 0;
            int ix = str.Length - 1;
            while (Char.IsWhiteSpace(str[ix--]) && ix >= 0)
                count++;
            return count;
        }

        private void OnSourceCodeChanged(ITextSnapshot snapshot)
        {
            // Clear tags
            m_Tags.Clear();

            var shaderSource = snapshot.GetText();

            Debug.WriteLine($"[Hazel Shaders] Preprocessing shader '{m_FilePath}'...");
            var sources = ShaderPreprocessor.Preprocess(snapshot, m_Classifier, out var stageTokenPositions);

            Debug.WriteLine($"[Hazel Shaders] Compiling shader '{m_FilePath}'...");

            string vulkanSdkPathStr = Environment.GetEnvironmentVariable("VULKAN_SDK");
            string glslangValidatorPath = Path.Combine(vulkanSdkPathStr, "Bin/glslangValidator.exe");
            if (!File.Exists(glslangValidatorPath))
            {
                // TODO
                return;
            }

            foreach (var entry in sources)
            {
                ShaderStage stage = entry.Key;
                string stageSource = entry.Value;

                string stageString = stage.ToString().Substring(0, 4).ToLower();

                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = glslangValidatorPath;
                        process.StartInfo.Arguments = $"--stdin -S {stageString} --client vulkan100 -Od --keep-uncalled";
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

                        if (failCode == GlslValidatorFailCode.Success)
                        {
                            Debug.WriteLine($"[Hazel Shaders] Successfully compiled {stage.ToString().ToLower()} shader");
                            continue;
                        }

                        if (failCode == GlslValidatorFailCode.FailCompile)
                            Debug.WriteLine($"[Hazel Shaders] Failed to compile {stage.ToString().ToLower()} shader.\nFail Code: {failCode.ToString()}\nOutput Data: {outputData}");
                        else
                            Debug.Assert(false);

                        var lines = outputData.ToString().Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line == "stdin")
                                continue;

                            var match = Regex.Match(line, @"(WARNING|ERROR):\s+(\d|.*):(\d+):\s+(.*)");

                            if (match.Groups.Count == 5)
                            {
                                var errorLine = Convert.ToInt32(match.Groups[3].Value);
                                var message = match.Groups[4].Value;
                                Debug.WriteLine($"{errorLine}: {message}");

                                stageTokenPositions.TryGetValue(stage, out var stageTokenSpan);
                                var stageStartLine = stageTokenSpan.Snapshot.GetLineNumberFromPosition(stageTokenSpan.Start.Position);

                                var snapshotLine = snapshot.GetLineFromLineNumber(errorLine - 1 + stageStartLine);
                                string lineString = snapshotLine.GetText();

                                int begin = WhiteSpaceAtBegin(lineString);
                                int end = WhiteSpaceAtEnd(lineString);
                                SnapshotSpan tagSpan = new SnapshotSpan(snapshot, snapshotLine.Start + begin, snapshotLine.Length - end);

                                ThreadHelper.JoinableTaskFactory.Run(async delegate
                                {
                                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                                    TextBlock toolTipContent = new TextBlock();
                                    toolTipContent.Inlines.Add(new Run("Test"));

                                    m_Tags.Add(new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError)));
                                });
                            }
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
