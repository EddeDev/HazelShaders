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

namespace HazelShaders
{
    using ShaderSourceMap = Dictionary<ShaderStage, string>;

    internal class ShaderPreprocessor
    {
        private const string StageToken = "#stage";

        public static ShaderSourceMap Preprocess(ITextSnapshot snapshot, IClassifier classifier, out Dictionary<ShaderStage, SnapshotSpan> outStageTokenPositions)
        {
            var shaderSource = snapshot.GetText();

            var snapshotSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            var classificationSpans = classifier.GetClassificationSpans(snapshotSpan);

            var stageTokenPositions = new List<KeyValuePair<ShaderStage, SnapshotSpan>>();
            foreach (var classificationSpan in classificationSpans)
            {
                if (classificationSpan.ClassificationType.IsOfType(PredefinedClassificationTypeNames.PreprocessorKeyword))
                {
                    var tokenSpan = new SnapshotSpan(snapshot, classificationSpan.Span);
                    var tokenText = tokenSpan.GetText();

                    // TODO: this substring should already be tokenized/trimmed
                    tokenText = tokenText.Trim();

                    if (tokenText == StageToken)
                    {
                        int start = classificationSpan.Span.Start + StageToken.Length;
                        int nextNewLine = shaderSource.IndexOf(Environment.NewLine, start);
                        if (nextNewLine == -1)
                            continue;

                        int length = nextNewLine - start;
                        if (length == 0)
                            continue;

                        string stageString = shaderSource.Substring(start, length).Trim();
                        if (stageString.Length == 0)
                            continue;

                        stageString = stageString.ToLower();
                        char firstChar = Char.ToUpper(stageString[0]);
                        stageString = stageString.Remove(0, 1).Insert(0, firstChar.ToString());

                        if (!Enum.TryParse<ShaderStage>(stageString, out var stage))
                            continue;

                        SnapshotSpan span = new SnapshotSpan(classificationSpan.Span.Start, nextNewLine - classificationSpan.Span.Start);
                        stageTokenPositions.Add(new KeyValuePair<ShaderStage, SnapshotSpan>(stage, span));
                    }
                }
            }

            stageTokenPositions = stageTokenPositions.OrderBy(x => x.Value.Start).ToList();

            ShaderSourceMap sources = new ShaderSourceMap();
            for (int i = 0; i < stageTokenPositions.Count; i++)
            {
                ShaderStage stage = stageTokenPositions[i].Key;

                int begin = stageTokenPositions[i].Value.End;
                int end = i < stageTokenPositions.Count - 1 ? stageTokenPositions[i + 1].Value.Start : shaderSource.Length;

                string stageSource = shaderSource.Substring(begin, end - begin);

                // Replace with UNIX line endings
                stageSource = stageSource.Replace("\r\n", "\n");

                // Remove all multiline comments and replace them with empty lines
                while (true)
                {
                    // Regex regex = new Regex(@"/\\*(.|[\\r\\n])*?\\*/");
                    Regex regex = new Regex(@"/\*(.|[\r\n])*?\*/");
                    Match match = regex.Match(stageSource);
                    if (!match.Success)
                        break;

                    int numLineEndings = match.Value.Count(c => c == '\n');
                    stageSource = stageSource.Replace(match.Value, new string('\n', numLineEndings));
                }

                // Remove all line comments and replace them with empty lines
                stageSource = Regex.Replace(stageSource, "//.*", string.Empty);

                sources.Add(stage, stageSource);
            }

            outStageTokenPositions = stageTokenPositions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            return sources;
        }
    }
}
