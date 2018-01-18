﻿// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Translates spans in a versioned response from the out of proc analysis.
    /// 
    /// Responses which need to operator on buffers (for editing, classification, etc...)
    /// will return a version which is what the results are based upon.  A SpanTranslator
    /// will translate from the version which is potentially old to the current
    /// version of the buffer.
    /// </summary>
    internal class LocationTracker {
        private ITextSnapshot _snapshot;
        private readonly Dictionary<int, NewLineLocation[]> _lineCache;

        /// <summary>
        /// Creates a new location tracker which can track spans and positions through time.
        /// 
        /// The tracker will translate positions from the specified version to the current
        /// snapshot in VS.  Requests can be made to track either forwards or backwards.
        /// </summary>
        public LocationTracker(ITextSnapshot snapshot) {
            // We always hold onto the last version that we've successfully analyzed, as that's
            // the last event the out of proc analyzer will send us.  Once we've received
            // that event all future information should come from at least that version.  This
            // prevents us from holding onto every version in the world.

            _lineCache = new Dictionary<int, NewLineLocation[]>();
            _snapshot = snapshot;
        }

        public ITextBuffer TextBuffer {
            get {
                lock (_lineCache) {
                    return _snapshot.TextBuffer;
                }
            }
        }

        internal bool IsCached(int version) {
            lock (_lineCache) {
                return _lineCache.ContainsKey(version);
            }
        }

        public void UpdateBaseSnapshot(ITextSnapshot snapshot) {
            lock (_lineCache) {
                if (_snapshot.TextBuffer != TextBuffer && TextBuffer != null) {
                    throw new InvalidOperationException("Cannot change buffer");
                }

                _snapshot = snapshot;
                foreach (var key in _lineCache.Keys.Where(k => k < _snapshot.Version.VersionNumber).ToArray()) {
                    _lineCache.Remove(key);
                }
            }
        }

        public bool CanTranslateFrom(int version) {
            var ver = _snapshot?.Version;
            if (ver == null || version < ver.VersionNumber) {
                return false;
            }
            while (ver.Next != null && ver.VersionNumber < version) {
                ver = ver.Next;
            }
            return ver.VersionNumber == version;
        }

        private static IEnumerable<NewLineLocation> LinesToLineEnds(IEnumerable<ITextSnapshotLine> lines) {
            foreach (var l in lines) {
                var nlk = NewLineKind.None;
                if (l.LineBreakLength == 2) {
                    nlk = NewLineKind.CarriageReturnLineFeed;
                } else if (l.LineBreakLength == 1) {
                    if (l.GetLineBreakText() == "\n") {
                        nlk = NewLineKind.LineFeed;
                    } else {
                        nlk = NewLineKind.CarriageReturn;
                    }
                }
                yield return new NewLineLocation(l.EndIncludingLineBreak.Position, nlk);
            }
        }

        private static IEnumerable<NewLineLocation> LineEndsToLineLengths(IEnumerable<NewLineLocation> lineEnds) {
            bool lastWasEmpty = false;
            int lastEnd = 0;
            foreach (var line in lineEnds) {
                int length = line.EndIndex - lastEnd;
                lastEnd = line.EndIndex;
                lastWasEmpty = length == 0;
                // Length is only 0 when Kind == None
                if (length > 0) {
                    yield return new NewLineLocation(length, line.Kind);
                }
            }

            if (lastWasEmpty) {
                // If we finish with an empty line, need to include it
                yield return new NewLineLocation(0, NewLineKind.None);
            }
        }

        private static IEnumerable<NewLineLocation> LineLengthsToLineEnds(IEnumerable<NewLineLocation> lineLengths) {
            bool lastHadNoEnding = false;
            int lastEnd = 0;
            foreach (var line in lineLengths) {
                lastEnd += line.EndIndex;
                if (line.Kind == NewLineKind.None) {
                    // Only yield NewLineKind.None at the end
                    // Otherwise, we want to merge the lines
                    lastHadNoEnding = true;
                } else {
                    lastHadNoEnding = false;
                    yield return new NewLineLocation(lastEnd, line.Kind);
                }
            }

            if (lastHadNoEnding) {
                yield return new NewLineLocation(lastEnd, NewLineKind.None);
            }
        }

        internal NewLineLocation[] GetLineLocations(int version) {
            var ver = _snapshot.Version;
            NewLineLocation[] initial;

            lock (_lineCache) {
                // Precalculated for this version
                if (_lineCache.TryGetValue(version, out initial)) {
                    return initial;
                }

                int fromVersion = version;
                // Get the last available set of newlines
                while (--fromVersion >= ver.VersionNumber) {
                    if (_lineCache.TryGetValue(fromVersion, out initial)) {
                        break;
                    }
                }

                // Create the initial set if it wasn't cached
                if (initial == null) {
                    fromVersion = ver.VersionNumber;
                    _lineCache[fromVersion] = initial = LinesToLineEnds(_snapshot.Lines).ToArray();
                }

                while (ver.Next != null && ver.VersionNumber < fromVersion) {
                    ver = ver.Next;
                }

                List<NewLineLocation> asLengths = null;
                while (ver.Changes != null && ver.VersionNumber < version) {
                    if (asLengths == null) {
                        asLengths = LineEndsToLineLengths(initial).ToList();
                    }

                    // Apply the changes from this version to the line lengths
                    foreach (var c in ver.Changes) {
                        var oldLoc = NewLineLocation.IndexToLocation(initial, c.OldPosition);
                        int lineNo = oldLoc.Line - 1;
                        while (asLengths.Count <= lineNo) {
                            asLengths.Add(new NewLineLocation(0, NewLineKind.None));
                        }
                        var line = asLengths[lineNo];
                        
                        if (c.OldLength > 0) {
                            // Deletion may span lines, so combine them until we can delete
                            int cutAtCol = oldLoc.Column - 1;
                            for (int toRemove = c.OldLength; toRemove > 0 && lineNo < asLengths.Count; lineNo += 1) {
                                line = asLengths[lineNo];
                                int lineLen = line.EndIndex - cutAtCol;
                                cutAtCol = 0;
                                if (line.Kind == NewLineKind.CarriageReturnLineFeed && toRemove == lineLen - 1) {
                                    // Special case of deleting just the '\r' from a '\r\n' ending
                                    asLengths[lineNo] = new NewLineLocation(line.EndIndex - toRemove, NewLineKind.LineFeed);
                                    break;
                                } else if (toRemove < lineLen) {
                                    asLengths[lineNo] = new NewLineLocation(line.EndIndex - toRemove, line.Kind);
                                    break;
                                } else {
                                    asLengths[lineNo] = new NewLineLocation(line.EndIndex - lineLen, NewLineKind.None);
                                    toRemove -= lineLen;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(c.NewText)) {
                            NewLineLocation addedLine = new NewLineLocation(0, NewLineKind.None);
                            int lastLineEnd = 0, cutAtCol = oldLoc.Column - 1;
                            if (cutAtCol > line.EndIndex - line.Kind.GetSize() && lineNo + 1 < asLengths.Count) {
                                cutAtCol = 0;
                                lineNo += 1;
                                line = asLengths[lineNo];
                            }
                            while ((addedLine = NewLineLocation.FindNewLine(c.NewText, lastLineEnd)).Kind != NewLineKind.None) {
                                if (cutAtCol > 0) {
                                    asLengths[lineNo] = new NewLineLocation(line.EndIndex - cutAtCol, line.Kind);
                                    lastLineEnd -= cutAtCol;
                                    cutAtCol = 0;
                                }
                                line = new NewLineLocation(addedLine.EndIndex - lastLineEnd, addedLine.Kind);
                                asLengths.Insert(lineNo++, line);
                                lastLineEnd = addedLine.EndIndex;
                            }
                            if (addedLine.EndIndex > lastLineEnd) {
                                if (lineNo < asLengths.Count) {
                                    line = asLengths[lineNo];
                                    asLengths[lineNo] = line = new NewLineLocation(line.EndIndex + addedLine.EndIndex - lastLineEnd, line.Kind);
                                } else {
                                    asLengths.Add(new NewLineLocation(addedLine.EndIndex - lastLineEnd, NewLineKind.None));
                                }
                            }
                        }
                    }

                    initial = LineLengthsToLineEnds(asLengths).ToArray();
                    _lineCache[ver.VersionNumber + 1] = initial;

                    ver = ver.Next;
                }

                return initial;
            }
        }

        public int GetIndex(SourceLocation loc, int atVersion) {
            var lines = GetLineLocations(atVersion);
            return NewLineLocation.LocationToIndex(lines, loc, -1);
        }

        public SourceLocation GetSourceLocation(int index, int atVersion) {
            var lines = GetLineLocations(atVersion);
            return NewLineLocation.IndexToLocation(lines, index);
        }

        public SourceLocation Translate(SourceLocation loc, int fromVersion, int toVersion) {
            var fromVer = _snapshot.Version;
            while (fromVer.Next != null && fromVer.VersionNumber < fromVersion) {
                fromVer = fromVer.Next;
            }

            var toVer = toVersion > fromVersion ? fromVer : _snapshot.Version;
            while (toVer.Next != null && toVer.VersionNumber < toVersion) {
                toVer = toVer.Next;
            }

            var fromLines = GetLineLocations(fromVer.VersionNumber);
            var index = NewLineLocation.LocationToIndex(fromLines, loc, fromVer.Length);

            if (fromVer.VersionNumber < toVer.VersionNumber) {
                index = Tracking.TrackPositionForwardInTime(PointTrackingMode.Negative, index, fromVer, toVer);
            } else {
                index = Tracking.TrackPositionBackwardInTime(PointTrackingMode.Negative, index, fromVer, toVer);
            }

            var toLines = GetLineLocations(toVer.VersionNumber);
            return NewLineLocation.IndexToLocation(toLines, index);
        }

        public SnapshotPoint Translate(SourceLocation loc, int fromVersion, ITextSnapshot toSnapshot) {
            return Translate(loc, fromVersion, toSnapshot.Version.VersionNumber).ToSnapshotPoint(toSnapshot);
        }

        public SourceSpan Translate(SourceSpan span, int fromVersion, int toVersion) {
            return new SourceSpan(
                Translate(span.Start, fromVersion, toVersion),
                Translate(span.End, fromVersion, toVersion)
            );
        }

        public SnapshotSpan Translate(SourceSpan span, int fromVersion, ITextSnapshot toSnapshot) {
            return Translate(span, fromVersion, toSnapshot.Version.VersionNumber).ToSnapshotSpan(toSnapshot);
        }
    }

}
