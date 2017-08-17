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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    sealed class BufferParser : IDisposable {
        private readonly Timer _timer;
        internal readonly PythonEditorServices _services;
        internal readonly AnalysisEntry AnalysisEntry;

        private IList<PythonTextBufferInfo> _buffers;
        private bool _parsing, _requeue, _textChange, _parseImmediately;
        private ITextDocument _document;

        /// <summary>
        /// Maps between buffer ID and buffer info.
        /// </summary>
        private Dictionary<int, PythonTextBufferInfo> _bufferIdMapping = new Dictionary<int, PythonTextBufferInfo>();

        private const int ReparseDelay = 1000;      // delay in MS before we re-parse a buffer w/ non-line changes.

        public static readonly object DoNotParse = new object();
        public static readonly object ParseImmediately = new object();

        public BufferParser(AnalysisEntry entry) {
            Debug.Assert(entry != null);

            _services = entry.Analyzer._services;
            _timer = new Timer(ReparseTimer, null, Timeout.Infinite, Timeout.Infinite);
            _buffers = Array.Empty<PythonTextBufferInfo>();
            AnalysisEntry = entry;
        }

        public PythonTextBufferInfo GetBuffer(ITextBuffer buffer) {
            return buffer == null ? null : _services.GetBufferInfo(buffer);
        }

        public PythonTextBufferInfo GetBuffer(int bufferId) {
            lock (this) {
                PythonTextBufferInfo res;
                _bufferIdMapping.TryGetValue(bufferId, out res);
                return res;
            }
        }

        /// <summary>
        /// Indicates that the specified buffer ID has been analyzed with this version.
        /// </summary>
        /// <returns>
        /// True if the specified version is newer than the last one we had received.
        /// </returns>
        public bool Analyzed(int bufferId, int version) {
            return GetBuffer(bufferId)?.UpdateLastReceivedAnalysis(version) ?? false;
        }

        /// <summary>
        /// Indicates that the specified buffer ID has been parsed with this version.
        /// </summary>
        /// <returns>
        /// True if the specified version is newer than the last one we had received.
        /// </returns>
        public bool Parsed(int bufferId, int version) {
            return GetBuffer(bufferId)?.UpdateLastReceivedParse(version) ?? false;
        }

        internal ITextSnapshot GetLastSentSnapshot(ITextBuffer buffer) {
            return GetBuffer(buffer)?.LastSentSnapshot;
        }

        internal void SetLastSentSnapshot(ITextSnapshot snapshot) {
            if (snapshot == null) {
                Debug.Fail("null snapshot");
                return;
            }

            GetBuffer(snapshot.TextBuffer).LastSentSnapshot = snapshot;
        }

        public ITextBuffer[] Buffers {
            get {
                return _buffers.Where(x => !x.DoNotParse).Select(x => x.Buffer).ToArray();
            }
        }

        internal void AddBuffer(ITextBuffer textBuffer) {
            int newId;
            lock (this) {
                var bi = _services.GetBufferInfo(textBuffer);
                if (_buffers.Contains(bi)) {
                    return;
                }

                EnsureMutableBuffers();
                _buffers.Add(bi);
                newId = _buffers.Count - 1;
                if (bi.ParseImmediately) {
                    _parseImmediately = true;
                }
            }

            InitBuffer(textBuffer, newId);
        }

        internal int RemoveBuffer(ITextBuffer subjectBuffer) {
            int result;
            var bi = PythonTextBufferInfo.TryGetForBuffer(subjectBuffer);

            lock (this) {
                if (bi != null) {
                    EnsureMutableBuffers();
                    _buffers.Remove(bi);
                }
                result = _buffers.Count;
            }

            if (bi != null) {
                UninitBuffer(bi);
            }

            return result;
        }

        private void UninitBuffer(PythonTextBufferInfo subjectBuffer) {
            if (subjectBuffer == null) {
                throw new ArgumentNullException(nameof(subjectBuffer));
            }
            subjectBuffer.OnChangedLowPriority -= BufferChangedLowPriority;
            VsProjectAnalyzer.DisconnectErrorList(subjectBuffer);
            lock (this) {
                _bufferIdMapping.Remove(subjectBuffer.AnalysisEntryId);
                subjectBuffer.SetAnalysisEntryId(-1);
            }


            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
        }

        private void InitBuffer(ITextBuffer buffer, int id = 0) {
            var bi = _services.GetBufferInfo(buffer);
            if (!bi.SetAnalysisEntryId(id)) {
                Debug.Fail("Buffer is already initialized");
                return;
            }

            bi.OnChangedLowPriority += BufferChangedLowPriority;
            VsProjectAnalyzer.ConnectErrorList(bi);

            lock (this) {
                _bufferIdMapping[id] = bi;
            }

            ITextDocument doc;
            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out doc) && doc != _document) {
                if (_document != null) {
                    _document.EncodingChanged -= EncodingChanged;
                }
                _document = doc;
                if (_document != null) {
                    _document.EncodingChanged += EncodingChanged;
                }
            }
        }

        private void EnsureMutableBuffers() {
            if (_buffers.IsReadOnly) {
                _buffers = new List<PythonTextBufferInfo>(_buffers);
            }
        }

        internal void ReparseTimer(object unused) {
            RequeueWorker();
        }

        internal void ReparseWorker(object unused) {
            ITextSnapshot[] snapshots;
            lock (this) {
                if (_parsing) {
                    return;
                }

                _parsing = true;
                snapshots = _buffers.Where(b => !b.DoNotParse).Select(b => b.CurrentSnapshot).ToArray();
            }

            ParseBuffers(snapshots).WaitAndHandleAllExceptions(_services.Site);

            lock (this) {
                _parsing = false;
                if (_requeue) {
                    RequeueWorker();
                }
                _requeue = false;
            }
        }

        public async Task EnsureCodeSyncedAsync(ITextBuffer buffer) {
            var lastSent = GetLastSentSnapshot(buffer);
            var snapshot = buffer.CurrentSnapshot;
            if (lastSent != buffer.CurrentSnapshot) {
                await ParseBuffers(Enumerable.Repeat(snapshot, 1));
            }
        }

        private Task ParseBuffers(IEnumerable<ITextSnapshot> snapshots) {
            return ParseBuffersAsync(_services, AnalysisEntry, snapshots);
        }

        private static IEnumerable<ITextVersion> GetVersions(ITextVersion from, ITextVersion to) {
            for (var v = from; v != null && v != to; v = v.Next) {
                yield return v;
            }
        }

        private static AP.FileUpdate GetUpdateForSnapshot(PythonEditorServices services, ITextSnapshot snapshot) {
            var buffer = services.GetBufferInfo(snapshot.TextBuffer);
            if (buffer.DoNotParse || snapshot.IsReplBufferWithCommand() || buffer.AnalysisEntryId < 0) {
                return null;
            }

            var lastSent = buffer.LastSentSnapshot;

            if (lastSent?.Version == snapshot.Version) {
                // this snapshot is up to date...
                return null;
            }

            // Update last sent snapshot and the analysis cookie to our
            // current snapshot.
            buffer.LastSentSnapshot = snapshot;
            var entry = buffer.AnalysisEntry;
            if (entry != null) {
                entry.AnalysisCookie = new SnapshotCookie(snapshot);
            }

            if (lastSent == null || lastSent.TextBuffer != buffer.Buffer) {
                // First time parsing from a live buffer, send the entire
                // file and set our initial snapshot.  We'll roll forward
                // to new snapshots when we receive the errors event.  This
                // just makes sure that the content is in sync.
                return new AP.FileUpdate {
                    content = snapshot.GetText(),
                    version = snapshot.Version.VersionNumber,
                    bufferId = buffer.AnalysisEntryId,
                    kind = AP.FileUpdateKind.reset
                };
            }

            var versions = GetVersions(lastSent.Version, snapshot.Version).Select(v => new AP.VersionChanges{
                changes = GetChanges(v)
            }).ToArray();

            return new AP.FileUpdate() {
                versions = versions,
                version = snapshot.Version.VersionNumber,
                bufferId = buffer.AnalysisEntryId,
                kind = AP.FileUpdateKind.changes
            };
        }

        [Conditional("DEBUG")]
        private static void ValidateBufferContents(IEnumerable<ITextSnapshot> snapshots, Dictionary<int, string> code) {
            foreach (var snapshot in snapshots) {
                var bi = PythonTextBufferInfo.TryGetForBuffer(snapshot.TextBuffer);
                if (bi == null) {
                    continue;
                }

                string newCode;
                if (!code.TryGetValue(bi.AnalysisEntryId, out newCode)) {
                    continue;
                }

                Debug.Assert(newCode.TrimEnd() == snapshot.GetText().TrimEnd(), "Buffer content mismatch");
            }
        }

        internal static async Task ParseBuffersAsync(
            PythonEditorServices services,
            AnalysisEntry entry,
            IEnumerable<ITextSnapshot> snapshots
        ) {
            var updates = snapshots.Select(s => GetUpdateForSnapshot(services, s)).Where(u => u != null).ToList();

            if (!updates.Any()) {
                return;
            }

            entry.Analyzer._analysisComplete = false;
            Interlocked.Increment(ref entry.Analyzer._parsePending);

            var res = await entry.Analyzer.SendRequestAsync(
                new AP.FileUpdateRequest() {
                    fileId = entry.FileId,
                    updates = updates.ToArray()
                }
            );

            if (res != null) {
                Debug.Assert(res.failed != true);
                entry.Analyzer.OnAnalysisStarted();
                ValidateBufferContents(snapshots, res.newCode);
            } else {
                Interlocked.Decrement(ref entry.Analyzer._parsePending);
            }
        }

        private static AP.ChangeInfo[] GetChanges(ITextVersion curVersion) {
            Debug.WriteLine("Changes for version {0}", curVersion.VersionNumber);
            var changes = new List<AP.ChangeInfo>();
            if (curVersion.Changes != null) {
                foreach (var change in curVersion.Changes) {
                    Debug.WriteLine("Changes for version {0} {1} {2}", change.OldPosition, change.OldLength, change.NewText);
                    
                    changes.Add(
                        new AP.ChangeInfo() {
                            start = change.OldPosition,
                            length = change.OldLength,
                            newText = change.NewText
                        }
                    );
                }
            }
            return changes.ToArray();
        }

        internal void EncodingChanged(object sender, EncodingChangedEventArgs e) {
            lock (this) {
                if (_parsing) {
                    // we are currently parsing, just reque when we complete
                    _requeue = true;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                } else {
                    Requeue();
                }
            }
        }

        internal void BufferChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            lock (this) {
                // only immediately re-parse on line changes after we've seen a text change.

                if (_parsing) {
                    // we are currently parsing, just reque when we complete
                    _requeue = true;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                } else if (_parseImmediately) {
                    // we are a test buffer, we should requeue immediately
                    Requeue();
                } else if (LineAndTextChanges(e)) {
                    // user pressed enter, we should requeue immediately
                    Requeue();
                } else {
                    // parse if the user doesn't do anything for a while.
                    _textChange = IncludesTextChanges(e);
                    _timer.Change(ReparseDelay, Timeout.Infinite);
                }
            }
        }

        internal void Requeue() {
            RequeueWorker();
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void RequeueWorker() {
            ThreadPool.QueueUserWorkItem(ReparseWorker);
        }

        /// <summary>
        /// Used to track if we have line + text changes, just text changes, or just line changes.
        /// 
        /// If we have text changes followed by a line change we want to immediately reparse.
        /// If we have just text changes we want to reparse in ReparseDelay ms from the last change.
        /// If we have just repeated line changes (e.g. someone's holding down enter) we don't want to
        ///     repeatedly reparse, instead we want to wait ReparseDelay ms.
        /// </summary>
        private bool LineAndTextChanges(TextContentChangedEventArgs e) {
            if (_textChange) {
                _textChange = false;
                return e.Changes.IncludesLineChanges;
            }

            bool mixedChanges = false;
            if (e.Changes.IncludesLineChanges) {
                mixedChanges = IncludesTextChanges(e);
            }

            return mixedChanges;
        }

        /// <summary>
        /// Returns true if the change incldues text changes (not just line changes).
        /// </summary>
        private static bool IncludesTextChanges(TextContentChangedEventArgs e) {
            bool mixedChanges = false;
            foreach (var change in e.Changes) {
                if (!string.IsNullOrEmpty(change.OldText) || change.NewText != Environment.NewLine) {
                    mixedChanges = true;
                    break;
                }
            }
            return mixedChanges;
        }

        public void Dispose() {
            foreach (var buffer in _buffers) {
                UninitBuffer(buffer);
            }
            _timer.Dispose();
            AnalysisEntry.ClearBufferParser(this);
        }

        internal ITextDocument Document {
            get {
                return _document;
            }
        }
    }
}
