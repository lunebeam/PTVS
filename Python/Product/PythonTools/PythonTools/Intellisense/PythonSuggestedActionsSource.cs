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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudio.Language.StandardClassification;

namespace Microsoft.PythonTools.Intellisense {
    class PythonSuggestedActionsSource : ISuggestedActionsSource {
        internal readonly IServiceProvider _provider;
        internal readonly ITextView _view;
        private readonly ITextBuffer _textBuffer;
        private readonly IClassifier _classifier;
        private readonly ITextStructureNavigator _textNavigator;
        private Lazy<PreviewChangesService> _changePreviewFactory;

        private readonly object _currentLock = new object();
        private IEnumerable<SuggestedActionSet> _current;
        private SnapshotSpan _currentSpan;
        private readonly UIThreadBase _uiThread;

        private static readonly Guid _telemetryId = new Guid("{9D2182D9-27BC-4143-9A93-B7D9C015D01B}");

        public PythonSuggestedActionsSource(
            IServiceProvider provider,
            ITextView textView,
            ITextBuffer textBuffer,
            IClassifier classifier,
            ITextStructureNavigator textNavigator,
            Lazy<PreviewChangesService> changePreviewFactory) {
            _provider = provider;
            _view = textView;
            _textBuffer = textBuffer;
            _textBuffer.RegisterForNewAnalysis(OnNewAnalysisEntry);
            _uiThread = provider.GetUIThread();
            _classifier = classifier;
            _textNavigator = textNavigator;
            _changePreviewFactory = changePreviewFactory;
        }

        private void OnNewAnalysisEntry(AnalysisEntry obj) {
            SuggestedActionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose() { }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
            lock (_currentLock) {
                if (_currentSpan == range) {
                    return _current;
                }
            }
            return null;
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
            var suggestions = new List<SuggestedActionSet>();

            var sources = new Func<SnapshotSpan, CancellationToken, Task<SuggestedActionSet>>[] {
                GetMissingImports,
                GetNumericConversions,
            };

            foreach (var source in sources) {
                var imports = await source(range, cancellationToken);
                if (imports != null) {
                    suggestions.Add(imports);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!suggestions.SelectMany(s => s.Actions).Any()) {
                return false;
            }

            lock (_currentLock) {
                cancellationToken.ThrowIfCancellationRequested();
                _current = suggestions;
                _currentSpan = range;
            }

            return true;
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            telemetryId = _telemetryId;
            return false;
        }

        private async Task<SuggestedActionSet> GetNumericConversions(SnapshotSpan range, CancellationToken cancellationToken) {
            var textBuffer = range.Snapshot.TextBuffer;
            var numericSpans = _classifier.GetClassificationSpans(range)
                .Where(token => token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Number));
            if (numericSpans.Any()) {
                var trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                    numericSpans.First().Span,
                    SpanTrackingMode.EdgeInclusive
                );

                var conversions = await _uiThread.InvokeTask(
                    () => VsProjectAnalyzer.GetSuggestedNumericFormatsAsync(
                        _provider,
                        _view,
                        textBuffer.CurrentSnapshot,
                        trackingSpan)
                );
                if (conversions != null) {
                    return new SuggestedActionSet(
                        conversions.Data.conversions.Select(
                            conv => new PythonSuggestedConvertNumericLiteralAction(this, textBuffer, conv, _changePreviewFactory, conversions.GetTracker(conv.version))
                    ));
                }
            }

            return null;
        }

        private async Task<SuggestedActionSet> GetMissingImports(SnapshotSpan range, CancellationToken cancellationToken) {
            var pos = _view.Caret.Position.BufferPosition;
            if (pos.Position < pos.GetContainingLine().End.Position) {
                pos += 1;
            }
            var targetPoint = _view.BufferGraph.MapDownToFirstMatch(pos, PointTrackingMode.Positive, EditorExtensions.IsPythonContent, PositionAffinity.Successor);
            if (targetPoint == null) {
                return null;
            }
            var textBuffer = targetPoint.Value.Snapshot.TextBuffer;
            var lineStart = targetPoint.Value.GetContainingLine().Start;

            var span = targetPoint.Value.Snapshot.CreateTrackingSpan(
                lineStart,
                targetPoint.Value.Position - lineStart.Position,
                SpanTrackingMode.EdgePositive,
                TrackingFidelityMode.Forward
            );

            var suggestions = new List<SuggestedActionSet>();

            var imports = await _uiThread.InvokeTask(() => VsProjectAnalyzer.GetMissingImportsAsync(_provider, _view, textBuffer.CurrentSnapshot, span));
            if (imports == MissingImportAnalysis.Empty) {
                return null;
            }

            var availableImports = await imports.GetAvailableImportsAsync(cancellationToken);
            return new SuggestedActionSet(
                availableImports.Select(s => new PythonSuggestedImportAction(this, textBuffer, s))
                    .OrderBy(k => k)
                    .Distinct()
            );
        }
    }
}
