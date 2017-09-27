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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TestRunnerInterop {
    public sealed class VsInstance : IDisposable {
        private readonly object _lock = new object();
        private Process _vs;
        private VisualStudioApp _app;
        private EnvDTE.DTE _dte;

        private bool _isDisposed = false;

        private string _currentSettings;

        public void StartOrRestart(
            string devenvExe,
            string devenvArguments,
            string testDataRoot,
            string tempRoot
        ) {
            lock (_lock) {
                var settings = $"{devenvExe};{devenvArguments};{testDataRoot};{tempRoot}";
                if (_vs != null && _app != null && _dte == null) {
                    if (_currentSettings == settings) {
                        return;
                    }
                    Console.WriteLine("Restarting VS because settings have changed");
                }
                _currentSettings = settings;
                CloseCurrentInstance();

                var psi = new ProcessStartInfo {
                    FileName = devenvExe,
                    Arguments = devenvArguments,
                    ErrorDialog = false,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.Environment["_PTVS_UI_TEST"] = "1";
                if (!string.IsNullOrEmpty(testDataRoot)) {
                    psi.Environment["_TESTDATA_ROOT_PATH"] = testDataRoot;
                }
                if (!string.IsNullOrEmpty(tempRoot)) {
                    psi.Environment["_TESTDATA_TEMP_PATH"] = tempRoot;
                }
                _vs = Process.Start(psi);

                // Forward console output to our own output, which will
                // be captured by the test runner.
                _vs.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                _vs.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
                _vs.BeginOutputReadLine();
                _vs.BeginErrorReadLine();

                // Always allow at least five seconds to start
                Thread.Sleep(5000);
                if (_vs.HasExited) {
                    throw new InvalidOperationException("Failed to start VS");
                }
                _app = VisualStudioApp.FromProcessId(_vs.Id);

                var stopAt = DateTime.Now.AddSeconds(60);
                while (DateTime.Now < stopAt && _dte == null) {
                    try {
                        _dte = _app.GetDTE();
                    } catch (InvalidOperationException) {
                        Thread.Sleep(1000);
                    }
                }
                if (_dte == null) {
                    throw new InvalidOperationException("Failed to start VS");
                }

                AttachIfDebugging(_vs);
            }
        }

        private void CloseCurrentInstance(bool hard = false) {
            lock (_lock) {
                if (_vs != null) {
                    if (hard) {
                        _vs.Kill();
                    } else {
                        if (!_vs.CloseMainWindow()) {
                            _vs.Kill();
                        }
                        if (!_vs.WaitForExit(10000)) {
                            _vs.Kill();
                        }
                    }
                    _vs.Dispose();
                    _vs = null;
                }
                _app = null;
                _dte = null;
            }
        }

        public bool IsRunning => !_isDisposed && _dte != null && !_vs.HasExited;

        private static void AttachIfDebugging(Process targetVs) {
            if (!Debugger.IsAttached) {
                return;
            }

            // We are debugging tests, so attach the debugger to VS
            var selfId = Process.GetCurrentProcess().Id;

            foreach (var p in Process.GetProcessesByName("devenv")) {
                if (p.Id == targetVs.Id) {
                    continue;
                }

                using (var vs = VisualStudioApp.FromProcessId(p.Id)) {
                    EnvDTE.DTE dte;
                    try {
                        dte = vs.GetDTE();
                    } catch (InvalidOperationException) {
                        // DTE is not available, which means VS has not been running
                        continue;
                    }

                    if (dte.Debugger.CurrentMode == EnvDTE.dbgDebugMode.dbgDesignMode) {
                        // Not the correct VS
                        continue;
                    }

                    foreach (EnvDTE.Process dp in dte.Debugger.DebuggedProcesses) {
                        if (dp.ProcessID == selfId) {
                            // This is the correct VS, so attach and return.

                            vs.AttachToProcess(targetVs, null);
                            return;
                        }
                    }
                }
            }

        }

        public void RunTest(string container, string name, TimeSpan timeout, object[] arguments) {
            if (_isDisposed) {
                throw new ObjectDisposedException(GetType().Name);
            }
            var dte = _dte;
            if (dte == null) {
                throw new InvalidOperationException("VS has not started");
            }

            bool timedOut = false;
            CancellationTokenSource cts = null;

            if (!Debugger.IsAttached && timeout < TimeSpan.MaxValue) {
                cts = new CancellationTokenSource();
                Task.Delay(timeout, cts.Token).ContinueWith(t => {
                    cts.Dispose();
                    if (!t.IsCanceled) {
                        timedOut = true;
                        Console.WriteLine($"Terminating {container}.{name}() after {timeout}");
                        // Terminate VS to unblock the Execute() call below
                        CloseCurrentInstance(hard: true);
                    }
                });
            }

            try {
                var r = dte.GetObject(container).Execute(name, arguments);
                cts?.Cancel();
                if (!r.IsSuccess) {
                    throw new TestFailedException(
                        r.ExceptionType,
                        r.ExceptionMessage,
                        r.ExceptionTraceback
                    );
                }
                return;
            } catch (COMException ex) {
                if (timedOut) {
                    throw new TimeoutException($"Terminating {container}.{name}() after {timeout}", ex);
                }
                // A COMException probably needs VS to restart, so close it.
                CloseCurrentInstance();
            }
        }

        void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                }

                CloseCurrentInstance();

                _isDisposed = true;
            }
        }

        ~VsInstance() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
