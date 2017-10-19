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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Keeps track of logged events and makes them available for display in the diagnostics window.
    /// </summary>
    [Export(typeof(IPythonToolsLogger))]
    [Export(typeof(InMemoryLogger))]
    class InMemoryLogger : IPythonToolsLogger {
        private int _installedInterpreters, _installedV2, _installedV3;
        private int _debugLaunchCount, _normalLaunchCount;
        private List<PackageInfo> _seenPackages = new List<PackageInfo>();
        private List<AnalysisInfo> _analysisInfo = new List<AnalysisInfo>();
        private List<string> _analysisAbnormalities = new List<string>();
        private Dictionary<string, Tuple<int, int, long, int>> _analysisTiming = new Dictionary<string, Tuple<int, int, long, int>>();
        private Dictionary<string, long> _analysisCount = new Dictionary<string, long>();

        #region IPythonToolsLogger Members

        public void LogEvent(PythonLogEvent logEvent, object argument) {
            var dictArgument = argument as IDictionary<string, object>;

            switch (logEvent) {
                case PythonLogEvent.Launch:
                    if (((LaunchInfo)argument).IsDebug) {
                        _debugLaunchCount++;
                    } else {
                        _normalLaunchCount++;
                    }
                    break;
                case PythonLogEvent.InstalledInterpreters:
                    _installedInterpreters = (int)dictArgument["Total"];
                    _installedV2 = (int)dictArgument["2x"];
                    _installedV3 = (int)dictArgument["3x"];
                    break;
                case PythonLogEvent.PythonPackage:
                    lock (_seenPackages) {
                        _seenPackages.Add(argument as PackageInfo);
                    }
                    break;
                case PythonLogEvent.AnalysisCompleted:
                    lock (_analysisInfo) {
                        _analysisInfo.Add(argument as AnalysisInfo);
                    }
                    break;
                case PythonLogEvent.AnalysisExitedAbnormally:
                case PythonLogEvent.AnalysisOperationCancelled:
                case PythonLogEvent.AnalysisOperationFailed:
                case PythonLogEvent.AnalysisWarning:
                    lock (_analysisAbnormalities) {
                        _analysisAbnormalities.Add("[{0}] {1}: {2}".FormatInvariant(DateTime.Now, logEvent, argument as string ?? ""));
                    }
                    break;
                case PythonLogEvent.AnalysisRequestTiming:
                    lock (_analysisTiming) {
                        var a = (AnalysisTimingInfo)argument;
                        if (_analysisTiming.ContainsKey(a.RequestName)) {
                            var t = _analysisTiming[a.RequestName];
                            _analysisTiming[a.RequestName] = Tuple.Create(t.Item1 + 1, Math.Max(t.Item2, a.Milliseconds), t.Item3 + a.Milliseconds, t.Item4 + (a.Timeout ? 1 : 0));
                        } else {
                            _analysisTiming[a.RequestName] = Tuple.Create(1, a.Milliseconds, (long)a.Milliseconds, a.Timeout ? 1 : 0);
                        }
                    }
                    break;
                case PythonLogEvent.AnalysisRequestSummary:
                    lock (_analysisCount) {
                        var a = (Dictionary<string, object>)argument;
                        foreach (var kv in a) {
                            if (kv.Value is long l) {
                                long existing;
                                _analysisCount.TryGetValue(kv.Key, out existing);
                                _analysisCount[kv.Key] = existing + l;
                            }
                        }
                    }
                    break;
                case PythonLogEvent.GetExpressionAtPoint:
                    lock (_analysisTiming) {
                        var a = (GetExpressionAtPointInfo)argument;
                        if (_analysisTiming.ContainsKey("GetExpressionAtPoint")) {
                            var t = _analysisTiming["GetExpressionAtPoint"];
                            _analysisTiming["GetExpressionAtPoint"] = Tuple.Create(t.Item1 + 1, Math.Max(t.Item2, a.Milliseconds), t.Item3 + a.Milliseconds, t.Item4 + (a.Success ? 0 : 1));
                        } else {
                            _analysisTiming["GetExpressionAtPoint"] = Tuple.Create(1, a.Milliseconds, (long)a.Milliseconds, a.Success ? 0 : 1);
                        }
                    }
                    break;
            }
        }

        #endregion

        public override string ToString() {
            StringBuilder res = new StringBuilder();
            res.AppendLine("Installed Interpreters: " + _installedInterpreters);
            res.AppendLine("    v2.x: " + _installedV2);
            res.AppendLine("    v3.x: " + _installedV3);
            res.AppendLine("Debug Launches: " + _debugLaunchCount);
            res.AppendLine("Normal Launches: " + _normalLaunchCount);
            res.AppendLine();

            lock (_seenPackages) {
                if (_seenPackages.Any(p => p != null)) {
                    res.AppendLine("Seen Packages:");
                    foreach (var package in _seenPackages) {
                        if (package != null) {
                            res.AppendLine("    " + package.Name);
                        }
                    }
                    res.AppendLine();
                }
            }

            lock (_analysisInfo) {
                if (_analysisInfo.Any(a => a != null)) {
                    res.AppendLine("Completion DB analyses:");
                    foreach (var analysis in _analysisInfo) {
                        if (analysis != null) {
                            res.AppendLine("    {0} - {1}s".FormatInvariant(analysis.InterpreterId, analysis.AnalysisSeconds));
                        }
                    }
                }
            }

            lock (_analysisAbnormalities) {
                if (_analysisAbnormalities.Any()) {
                    res.AppendFormat("Analysis abnormalities ({0}):", _analysisAbnormalities.Count);
                    res.AppendLine();
                    foreach (var abnormalExit in _analysisAbnormalities) {
                        res.AppendLine(abnormalExit);
                    }
                    res.AppendLine();
                }
            }

            lock (_analysisTiming) {
                lock (_analysisCount) {
                    if (_analysisTiming.Any()) {
                        res.AppendLine("Analysis timing:");
                        foreach (var kv in _analysisTiming.OrderBy(kv => kv.Key)) {
                            long count;
                            if (!_analysisCount.TryGetValue(kv.Key, out count)) {
                                count = kv.Value.Item1;
                            }

                            res.AppendFormat("    {0} (count {5}, slow count {1}, {2} timeouts, max {3:N0}ms, mean {4:N2}ms)",
                                kv.Key, kv.Value.Item1, kv.Value.Item4, kv.Value.Item2, (double)kv.Value.Item3 / kv.Value.Item1, count);
                            res.AppendLine();
                        }
                        res.AppendLine();
                    }
                }
            }

            return res.ToString();
        }
    }
}
