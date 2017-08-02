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
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    class PythonSuggestedConvertNumericLiteralAction : ISuggestedAction {
        private readonly PythonSuggestedActionsSource _source;
        private readonly ITextBuffer _buffer;
        private readonly AP.NumericConversion _conversion;

        private static readonly Guid _telemetryId = new Guid("{AA1F684A-BED7-42C1-ADE5-12B9629DCE9A}");
        public PythonSuggestedConvertNumericLiteralAction(PythonSuggestedActionsSource source, ITextBuffer buffer, AP.NumericConversion conversion) {
            _source = source;
            _buffer = buffer;
            _conversion = conversion;
        }

        public IEnumerable<SuggestedActionSet> ActionSets {
            get {
                return Enumerable.Empty<SuggestedActionSet>();
            }
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
            return Task.FromResult(ActionSets);
        }

        public bool HasActionSets {
            get { return false; }
        }

        public string DisplayText {
            get {
                return _conversion.changes.First().newText;
            }
        }

        public string IconAutomationText {
            get {
                return null;
            }
        }

        public ImageMoniker IconMoniker {
            get {
                return default(ImageMoniker);
            }
        }

        public ImageSource IconSource {
            get {
                // TODO: Convert from IconMoniker
                return null;
            }
        }

        public string InputGestureText {
            get {
                return null;
            }
        }

        public void Dispose() { }

        public object GetPreview(CancellationToken cancellationToken) {
            return null;
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
            return Task.FromResult<object>(null);
        }

        public bool HasPreview {
            get { return false; }
        }

        public void Invoke(CancellationToken cancellationToken) {
            var entryService = _source._provider.GetEntryService();
            AnalysisEntry entry;
            if (entryService == null || !entryService.TryGetAnalysisEntry(_source._view, _buffer, out entry)) {
                return;
            }

            var lastVersion = entry.GetAnalysisVersion(_buffer);

            VsProjectAnalyzer.ApplyChanges(
                _conversion.changes,
                lastVersion,
                _buffer,
                _conversion.version
            );
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            telemetryId = _telemetryId;
            return false;
        }
    }
}
