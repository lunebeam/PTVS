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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides classification based upon the DLR TokenCategory enum.
    /// </summary>
    internal sealed class PythonClassifier : IClassifier, IPythonTextBufferInfoEventSink {
        private readonly PythonClassifierProvider _provider;

        internal PythonClassifier(PythonClassifierProvider provider) {
            _provider = provider;
        }

        private Task OnNewAnalysisEntryAsync(PythonTextBufferInfo sender, AnalysisEntry entry) {
            var analyzer = entry?.Analyzer;
            if (analyzer == null) {
                Debug.Assert(entry == null, "Should not have new analysis entry without an analyzer");
                return Task.CompletedTask;
            }

            var snapshot = sender.CurrentSnapshot;
            ClassificationChanged?.Invoke(
                this,
                new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length))
            );

            return Task.CompletedTask;
        }

        #region IDlrClassifier

        // This event gets raised if the classification of existing test changes.
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        /// <summary>
        /// This method classifies the given snapshot span.
        /// </summary>
        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var snapshot = span.Snapshot;
            var bi = _provider.Services.GetBufferInfo(snapshot.TextBuffer);

            if (span.Length <= 0 || bi == null || snapshot.IsReplBufferWithCommand()) {
                return Array.Empty<ClassificationSpan>();
            }

            return bi.GetTrackingTokens(span).Select(kv => ClassifyToken(span, kv)).Where(c => c != null).ToList();
        }

        public PythonClassifierProvider Provider {
            get {
                return _provider;
            }
        }

        #endregion

        #region Private Members

        private Task OnTextContentChangedAsync(PythonTextBufferInfo sender, TextContentChangedEventArgs e) {
            // NOTE: Runs on background thread
            if (e == null) {
                Debug.Fail("Invalid type passed to event");
            }

            var snapshot = e.After;

            if (snapshot.IsReplBufferWithCommand()) {
                return Task.CompletedTask;
            }

            Span changedSpan = new Span(0, 0);
            foreach (var change in e.Changes) {
                if (changedSpan.Length == 0) {
                    changedSpan = change.NewSpan;
                } else {
                    changedSpan = Span.FromBounds(
                        Math.Min(change.NewSpan.Start, changedSpan.Start),
                        Math.Max(change.NewSpan.End, changedSpan.End)
                    );
                }
            }
            if (changedSpan.Length > 0) {
                ClassificationChanged?.Invoke(
                    this,
                    new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, changedSpan))
                );
            }

            return Task.CompletedTask;
        }

        private ClassificationSpan ClassifyToken(SnapshotSpan span, TrackingTokenInfo token) {
            IClassificationType classification = null;

            if (token.Category == TokenCategory.Operator) {
                if (token.Trigger == TokenTriggers.MemberSelect) {
                    classification = _provider.DotClassification;
                }
            } else if (token.Category == TokenCategory.Grouping) {
                if ((token.Trigger & TokenTriggers.MatchBraces) != 0) {
                    classification = _provider.GroupingClassification;
                }
            } else if (token.Category == TokenCategory.Delimiter) {
                if (token.Trigger == TokenTriggers.ParameterNext) {
                    classification = _provider.CommaClassification;
                }
            }

            if (classification == null) {
                _provider.CategoryMap.TryGetValue(token.Category, out classification);
            }

            if (classification != null) {
                var tokenSpan = token.ToSnapshotSpan(span.Snapshot);
                var intersection = span.Intersection(tokenSpan);

                if (intersection != null && intersection.Value.Length > 0 ||
                    (span.Length == 0 && tokenSpan.Contains(span.Start))) { // handle zero-length spans which Intersect and Overlap won't return true on ever.
                    return new ClassificationSpan(new SnapshotSpan(span.Snapshot, tokenSpan), classification);
                }
            }

            return null;
        }

        Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
            if (e.Event == PythonTextBufferInfoEvents.NewAnalysisEntry) {
                return OnNewAnalysisEntryAsync(sender, e.AnalysisEntry);
            } else if (e.Event == PythonTextBufferInfoEvents.TextContentChangedOnBackgroundThread) {
                return OnTextContentChangedAsync(sender, (e as PythonTextBufferInfoNestedEventArgs)?.NestedEventArgs as TextContentChangedEventArgs);
            }
            return Task.CompletedTask;
        }

        #endregion
    }

    internal static partial class ClassifierExtensions {
        public static PythonClassifier GetPythonClassifier(this ITextBuffer buffer) {
            var bi = PythonTextBufferInfo.TryGetForBuffer(buffer);
            if (bi == null) {
                return null;
            }

            var provider = bi.Services.ComponentModel.GetService<PythonClassifierProvider>();
            return provider.GetClassifier(buffer) as PythonClassifier;
        }
    }
}
