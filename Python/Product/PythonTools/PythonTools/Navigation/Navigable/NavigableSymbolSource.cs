// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace Microsoft.PythonTools.Navigation.Navigable {
    class NavigableSymbolSource : INavigableSymbolSource {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITextBuffer _buffer;
        private readonly IClassifier _classifier;
        private readonly ITextStructureNavigator _textNavigator;
        private readonly AnalysisEntryService _entryService;

        private static readonly string[] _classifications = new string[] {
            PredefinedClassificationTypeNames.Identifier,
            //PredefinedClassificationTypeNames.String,
            PythonPredefinedClassificationTypeNames.Class,
            PythonPredefinedClassificationTypeNames.Function,
            PythonPredefinedClassificationTypeNames.Module,
            PythonPredefinedClassificationTypeNames.Parameter,
        };

        public NavigableSymbolSource(IServiceProvider serviceProvider, ITextBuffer buffer, IClassifier classifier, ITextStructureNavigator textNavigator) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _textNavigator = textNavigator ?? throw new ArgumentNullException(nameof(textNavigator));
            _entryService = serviceProvider.GetEntryService();
        }

        public void Dispose() {
        }

        public async Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken cancellationToken) {
            Debug.Assert(triggerSpan.Length == 1);

            cancellationToken.ThrowIfCancellationRequested();

            var extent = _textNavigator.GetExtentOfWord(triggerSpan.Start);
            if (!extent.IsSignificant) {
                return null;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            AnalysisEntry entry = null;
            _entryService?.TryGetAnalysisEntry(_buffer, out entry);
            if (entry == null) {
                return null;
            }

            foreach (var token in _classifier.GetClassificationSpans(extent.Span)) {
                cancellationToken.ThrowIfCancellationRequested();

                // Quickly eliminate anything that isn't the right classification.
                if (!_classifications.Any(c => token.ClassificationType.IsOfType(c))) {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                //if (token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.String)) {
                //    var r = GetNavigableFile(token.Span);
                //    if (r != null) {
                //        return r;
                //    }
                //    continue;
                //}

                // Check with the analyzer, which will give us a precise
                // result, including the source location.
                var result = await GetDefinitionLocationsAsync(entry, token.Span.Start).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (result.Any()) {
                    return new NavigableSymbol(_serviceProvider, result.First(), token.Span);
                }
            }

            return null;
        }

        private string GetCurrentWorkingDir() {
            string cwd;
            var replEval = _buffer.GetInteractiveWindow()?.GetPythonEvaluator();
            if (replEval != null) {
                cwd = replEval.CurrentWorkingDirectory;
            } else if (!string.IsNullOrEmpty(cwd = _buffer.GetFilePath())) {
                cwd = PathUtils.GetParent(cwd);
            } else {
                cwd = Environment.CurrentDirectory;
            }
            return cwd;
        }

        internal static readonly char[] QuoteChars = new[] { '"', '\'' };

        private NavigableSymbol GetNavigableFile(SnapshotSpan span) {
            var filePath = span.GetText();
            bool quoteBackslash = !span.GetText().TakeWhile(c => !QuoteChars.Contains(c)).Any(c => c == 'r' || c == 'R');

            if (quoteBackslash) {
                filePath = filePath.Replace("\\\\", "\\");
            }

            if (Path.GetInvalidPathChars().Any(c => filePath.Contains(c))) {
                return null;
            }

            if (Path.IsPathRooted(filePath)) {
                if (File.Exists(filePath)) {
                    return new NavigableSymbol(_serviceProvider, new AnalysisLocation(filePath, 1, 1), span);
                }
            } else {
                filePath = Path.Combine(GetCurrentWorkingDir(), filePath);
                if (File.Exists(filePath)) {
                    return new NavigableSymbol(_serviceProvider, new AnalysisLocation(filePath, 1, 1), span);
                }
            }

            return null;
        }

        internal static async Task<AnalysisLocation[]> GetDefinitionLocationsAsync(AnalysisEntry entry, SnapshotPoint pt) {
            var list = new List<AnalysisLocation>();

            var result = await entry.Analyzer.AnalyzeExpressionAsync(entry, pt).ConfigureAwait(false);
            foreach (var variable in (result?.Variables).MaybeEnumerate()) {
                if (variable.Type == Analysis.VariableType.Definition &&
                    !string.IsNullOrEmpty(variable.Location?.FilePath)) {
                    list.Add(variable.Location);
                }
            }

            return list.ToArray();
        }
    }
}
