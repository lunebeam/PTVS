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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Interpreter {
    class CondaPackageManager : IPackageManager, IDisposable {
        private IPythonInterpreterFactory _factory;
        private string _condaPath;
        private FileSystemWatcher _historyWatcher;
        private Timer _historyWatcherTimer;
        private string _historyPath;
        private readonly List<PackageSpec> _installedPackages;
        private readonly List<PackageSpec> _availablePackages;
        private CancellationTokenSource _currentRefresh;
        private bool _isReady, _everCached, _everCachedInstallable;

        internal readonly SemaphoreSlim _working = new SemaphoreSlim(1);

        private int _suppressCount;
        private bool _isDisposed;

        // Prevent too many concurrent executions to avoid exhausting disk IO
        private static readonly SemaphoreSlim _concurrencyLock = new SemaphoreSlim(4);

        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] {
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        public CondaPackageManager(
            bool allowFileSystemWatchers = true
        ) {
            _installedPackages = new List<PackageSpec>();
            _availablePackages = new List<PackageSpec>();

            if (allowFileSystemWatchers) {
                _historyWatcher = new FileSystemWatcher();
                _historyWatcher.Changed += _historyWatcher_Changed;
                _historyWatcher.Created += _historyWatcher_Changed;
                _historyWatcherTimer = new Timer(_historyWatcherTimer_Elapsed);
            }
        }

        private async void _historyWatcherTimer_Elapsed(object state) {
            try {
                _historyWatcherTimer.Change(Timeout.Infinite, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }

            OnHistoryFileChanged(this, EventArgs.Empty);
        }

        private void _historyWatcher_Changed(object sender, FileSystemEventArgs e) {
            if (PathUtils.IsSamePath(e.FullPath, _historyPath)) {
                try {
                    _historyWatcherTimer.Change(1000, Timeout.Infinite);
                } catch (ObjectDisposedException) {
                }
            }
        }

        public void SetInterpreterFactory(IPythonInterpreterFactory factory) {
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }
            if (!File.Exists(factory.Configuration?.InterpreterPath)) {
                throw new NotSupportedException();
            }

            _factory = factory;
            _condaPath = CondaUtils.GetCondaExecutablePath(factory.Configuration.PrefixPath);
            _historyPath = Path.Combine(_factory.Configuration.PrefixPath, "conda-meta", "history");

            if (_historyWatcher != null) {
                // Watch the conda-meta/history file, which is updated after 
                // a package is installed or uninstalled successfully.
                // Note: conda packages don't all install under lib/site-packages.
                try {
                    _historyWatcher.Path = Path.GetDirectoryName(_historyPath);
                    _historyWatcher.EnableRaisingEvents = true;
                } catch (ArgumentException) {
                } catch (IOException) {
                }
            }

            Task.Delay(100).ContinueWith(async t => {
                try {
                    await UpdateIsReadyAsync(false, CancellationToken.None);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }
            }).DoNotWait();
        }

        public IPythonInterpreterFactory Factory => _factory;

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CondaPackageManager() {
            Dispose(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_refreshIsCurrentTrigger")]
        protected void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;

            if (disposing) {
                if (_historyWatcher != null) {
                    _historyWatcher.Changed -= _historyWatcher_Changed;
                    _historyWatcher.Created -= _historyWatcher_Changed;
                    _historyWatcher.Dispose();
                }

                if (_historyWatcherTimer != null) {
                    _historyWatcherTimer.Dispose();
                }

                _working.Dispose();
            }
        }

        private void AbortOnInvalidConfiguration() {
            if (_factory == null || _factory.Configuration == null ||
                string.IsNullOrEmpty(_factory.Configuration.InterpreterPath)) {
                throw new InvalidOperationException(Strings.MisconfiguredEnvironment);
            }
        }

        private async Task AbortIfNotReady(CancellationToken cancellationToken) {
            if (!IsReady) {
                await UpdateIsReadyAsync(false, cancellationToken);
                if (!IsReady) {
                    throw new InvalidOperationException(Strings.MisconfiguredEnvironment);
                }
            }
        }

        private Task<bool> ShouldElevate(IPackageManagerUI ui, string operation) {
            // Global installation in C:\ProgramData\<prefix>
            // requires elevation to modify existing files, but our
            // elevation detection logic in package manager UI only
            // tries to create a file (which succeeds) so it thinks we
            // do not need to elevate.
            //
            // Conda itself checks for permission against the first file that matches:
            // - ./conda-meta/history
            // - ./conda-meta/conda*.json
            // - ./conda-meta/*.json
            // - ./python.exe
            // It tries to open the file with append mode, if that fails => CondaIOError

            // TODO: we need to apply the same logic so the user gets prompted for elevation
            return ui == null ? Task.FromResult(false) : ui.ShouldElevateAsync(this, operation);
        }

        public bool IsReady {
            get {
                return _isReady;
            }
            private set {
                if (_isReady != value) {
                    _isReady = value;
                    IsReadyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public event EventHandler IsReadyChanged;

        private async Task UpdateIsReadyAsync(bool alreadyHasLock, CancellationToken cancellationToken) {
            var args = new [] { "-h" };
            var workingLock = alreadyHasLock ? null : await _working.LockAsync(cancellationToken);
            try {
                using (var proc = ProcessOutput.Run(
                    _condaPath,
                    args,
                    _factory.Configuration.PrefixPath,
                    UnbufferedEnv,
                    false,
                    null
                )) {
                    try {
                        IsReady = (await proc == 0);
                    } catch (OperationCanceledException) {
                        IsReady = false;
                        return;
                    }
                }
            } finally {
                workingLock?.Dispose();
            }
        }

        public async Task PrepareAsync(IPackageManagerUI ui, CancellationToken cancellationToken) {
            if (IsReady) {
                return;
            }

            AbortOnInvalidConfiguration();

            await UpdateIsReadyAsync(false, cancellationToken);
            if (IsReady) {
                return;
            }
        }

        public async Task<bool> ExecuteAsync(string arguments, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);
            return false;
        }

        public async Task<bool> InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            bool success = false;
            var args = new List<string>();
            args.Add("install");
            args.Add("-p");
            args.Add(ProcessOutput.QuoteSingleArgument(_factory.Configuration.PrefixPath));
            args.Add("-y");

            args.Add(package.FullSpec);
            var name = string.IsNullOrEmpty(package.Name) ? package.FullSpec : package.Name;
            var operation = string.Join(" ", args);

            using (await _working.LockAsync(cancellationToken)) {
                ui?.OnOperationStarted(this, operation);
                ui?.OnOutputTextReceived(this, Strings.InstallingPackageStarted.FormatUI(name));

                try {
                    using (var output = ProcessOutput.Run(
                        _condaPath,
                        args,
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
                        false,
                        PackageManagerUIRedirector.Get(this, ui),
                        quoteArgs: false,
                        elevate: await ShouldElevate(ui, operation)
                    )) {
                        if (!output.IsStarted) {
                            return false;
                        }
                        var exitCode = await output;
                        success = exitCode == 0;
                    }
                    return success;
                } catch (IOException) {
                    return false;
                } finally {
                    if (!success) {
                        // Check whether we failed because conda is missing
                        UpdateIsReadyAsync(true, CancellationToken.None).DoNotWait();
                    }

                    var msg = success ? Strings.InstallingPackageSuccess : Strings.InstallingPackageFailed;
                    ui?.OnOutputTextReceived(this, msg.FormatUI(name));
                    ui?.OnOperationFinished(this, operation, success);
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
            }
        }

        public async Task<bool> UninstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            AbortOnInvalidConfiguration();
            await AbortIfNotReady(cancellationToken);

            bool success = false;
            var args = new List<string>();
            args.Add("uninstall");
            args.Add("-p");
            args.Add(ProcessOutput.QuoteSingleArgument(_factory.Configuration.PrefixPath));
            args.Add("-y");

            args.Add(package.Name);
            var name = string.IsNullOrEmpty(package.Name) ? package.FullSpec : package.Name;
            var operation = string.Join(" ", args);

            try {
                using (await _working.LockAsync(cancellationToken)) {
                    ui?.OnOperationStarted(this, operation);
                    ui?.OnOutputTextReceived(this, Strings.UninstallingPackageStarted.FormatUI(name));

                    using (var output = ProcessOutput.Run(
                        _condaPath,
                        args,
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
                        false,
                        PackageManagerUIRedirector.Get(this, ui),
                        elevate: await ShouldElevate(ui, operation)
                    )) {
                        if (!output.IsStarted) {
                            // The finally block handles output
                            return false;
                        }
                        var exitCode = await output;
                        success = exitCode == 0;
                    }
                    return success;
                }
            } catch (IOException) {
                return false;
            } finally {
                if (!success) {
                    // Check whether we failed because conda is missing
                    UpdateIsReadyAsync(false, CancellationToken.None).DoNotWait();
                }

                if (IsReady) {
                    await CacheInstalledPackagesAsync(false, false, cancellationToken);
                    if (!success) {
                        // Double check whether the package has actually
                        // been uninstalled, to avoid reporting errors 
                        // where, for all practical purposes, there is no
                        // error.
                        if (!(await GetInstalledPackageAsync(package, cancellationToken)).IsValid) {
                            success = true;
                        }
                    }
                }

                var msg = success ? Strings.UninstallingPackageSuccess : Strings.UninstallingPackageFailed;
                ui?.OnOutputTextReceived(this, msg.FormatUI(name));
                ui?.OnOperationFinished(this, operation, success);
            }
        }

        public event EventHandler InstalledPackagesChanged;
        public event EventHandler InstalledFilesChanged;

        private string EnvironmentName => Path.GetFileName(_factory.Configuration.PrefixPath);

        public string ExtensionDisplayName => Strings.CondaExtensionDisplayName;

        public string IndexDisplayName => Strings.CondaDefaultIndexName;

        public string SearchHelpText => Strings.CondaExtensionSearchCondaLabel;

        public string GetInstallCommandDisplayName(string searchQuery) {
            if (string.IsNullOrEmpty(searchQuery)) {
                return string.Empty;
            }

            return Strings.CondaExtensionCondaInstallFrom.FormatUI(searchQuery);
        }

        public bool CanUninstall(PackageSpec package) {
            // Don't make it easy for the users to get themselves in trouble.
            // If they really need to uninstall these packages, they can fall
            // back to command line.
            return package.Name != "python";
        }

        private async Task CacheInstalledPackagesAsync(
            bool alreadyHasLock,
            bool alreadyHasConcurrencyLock,
            CancellationToken cancellationToken
        ) {
            if (!IsReady) {
                await UpdateIsReadyAsync(alreadyHasLock, cancellationToken);
                if (!IsReady) {
                    return;
                }
            }

            List<PackageSpec> packages = null;

            var workingLock = alreadyHasLock ? null : await _working.LockAsync(cancellationToken);
            try {
                var args = new List<string>();
                args.Add("list");
                args.Add("-p");
                args.Add(ProcessOutput.QuoteSingleArgument(_factory.Configuration.PrefixPath));
                args.Add("--json");

                var concurrencyLock = alreadyHasConcurrencyLock ? null : await _concurrencyLock.LockAsync(cancellationToken);
                try {
                    using (var proc = ProcessOutput.Run(
                        _condaPath,
                        args,
                        _factory.Configuration.PrefixPath,
                        UnbufferedEnv,
                        false,
                        null
                    )) {
                        try {
                            if ((await proc) == 0) {
                                var json = string.Join(Environment.NewLine, proc.StandardOutputLines);
                                try {
                                    var data = JArray.Parse(json);
                                    packages = data
                                        .Select(j => new PackageSpec(j.Value<string>("name"), j.Value<string>("version")))
                                        .Where(p => p.IsValid)
                                        .OrderBy(p => p.Name)
                                        .ToList();
                                } catch (Newtonsoft.Json.JsonException ex) {
                                    Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                                    Debug.WriteLine(json);
                                }
                            }
                        } catch (OperationCanceledException) {
                            // Process failed to run
                            Debug.WriteLine("Failed to run conda to collect installed packages");
                            foreach (var line in proc.StandardOutputLines) {
                                Debug.WriteLine(line);
                            }
                        }
                    }
                } finally {
                    concurrencyLock?.Dispose();
                }

                // Outside of concurrency lock, still in working lock

                _installedPackages.Clear();
                if (packages != null) {
                    _installedPackages.AddRange(packages);
                }
                _everCached = true;
            } finally {
                workingLock?.Dispose();
            }

            InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task CacheInstallablePackagesAsync(
            bool alreadyHasLock,
            bool alreadyHasConcurrencyLock,
            CancellationToken cancellationToken
        ) {
            if (!IsReady) {
                await UpdateIsReadyAsync(alreadyHasLock, cancellationToken);
                if (!IsReady) {
                    return;
                }
            }

            List<PackageSpec> packages = null;

            var workingLock = alreadyHasLock ? null : await _working.LockAsync(cancellationToken);
            try {
                var concurrencyLock = alreadyHasConcurrencyLock ? null : await _concurrencyLock.LockAsync(cancellationToken);
                try {
                    packages = await ExecuteCondaSearch();
                } finally {
                    concurrencyLock?.Dispose();
                }

                // Outside of concurrency lock, still in working lock

                _availablePackages.Clear();
                _availablePackages.AddRange(packages);
                _everCachedInstallable = true;
            } finally {
                workingLock?.Dispose();
            }
        }

        private async Task<List<PackageSpec>> ExecuteCondaSearch() {
            var packages = new List<PackageSpec>();

            // TODO: Find a way to obtain package descriptions
            //       When a package is downloaded, often this file exists:
            //       %LOCALAPPDATA%/conda/conda/pkgs/requests-2.14.2-py36_0/info/about.json
            //       There are "summary" and "description" fields
            //       Latest news:
            //       Conda developer says they are working on a better API for this
            //       (which will get us descriptions), they will let us know when ready.

            // TODO: Use a global cache that can be reused by multiple conda envs (may need one per platform)
            // TODO: need to check if conda uses the platform of specified environment
            // or the platform of the conda.exe (does it always match?).
            // If the latter, we may need to pass in:
            // --platform win-32
            // --platform win-64
            var args = new List<string>();
            args.Add("search");
            args.Add("-p");
            args.Add(ProcessOutput.QuoteSingleArgument(_factory.Configuration.PrefixPath));
            args.Add("--json");

            using (var proc = ProcessOutput.Run(
                _condaPath,
                args,
                _factory.Configuration.PrefixPath,
                UnbufferedEnv,
                false,
                null
            )) {
                try {
                    if ((await proc) == 0) {
                        var json = string.Join(Environment.NewLine, proc.StandardOutputLines);
                        try {
                            // This json can be large, and has a lot of fields
                            // that we don't care about. For this json, DeserializeObject
                            // is much faster than JObject.Parse by about 4x-5x.

                            // TODO: find how to do this with a reader
                            var data = JsonConvert.DeserializeObject<Dictionary<string, CondaPackage[]>>(json);
                            packages = data.Values.Select(LastPackageSpec)
                                .Where(p => p.IsValid)
                                .OrderBy(p => p.Name)
                                .ToList();
                        } catch (JsonException ex) {
                            Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                            Debug.WriteLine(json);
                        }
                    }
                } catch (OperationCanceledException) {
                    // Process failed to run
                    Debug.WriteLine("Failed to run conda to collect installable packages");
                    foreach (var line in proc.StandardOutputLines) {
                        Debug.WriteLine(line);
                    }
                }
            }

            return packages;
        }

        class CondaPackage {
            [JsonProperty("name")]
            public string Name = null;

            [JsonProperty("version")]
            public string VersionText = null;
        }

        private PackageSpec LastPackageSpec(CondaPackage[] pkgs) {
            // TODO: the last package may not be compatible?
            var last = pkgs.LastOrDefault();
            return last != null ? new PackageSpec(last.Name, last.VersionText) : new PackageSpec();
         }

        public async Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCached) {
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
                return _installedPackages.ToArray();
            }
        }

        public async Task<PackageSpec> GetInstalledPackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            if (!package.IsValid) {
                return package;
            }
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCached) {
                    await CacheInstalledPackagesAsync(true, false, cancellationToken);
                }
                return _installedPackages.FirstOrDefault(p => p.Name == package.Name) ?? new PackageSpec(null);
            }
        }

        public async Task<IList<PackageSpec>> GetInstallablePackagesAsync(CancellationToken cancellationToken) {
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCachedInstallable) {
                    await CacheInstallablePackagesAsync(true, false, cancellationToken);
                }
                return _availablePackages.ToArray();
            }
        }

        public async Task<PackageSpec> GetInstallablePackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            if (!package.IsValid) {
                return package;
            }
            using (await _working.LockAsync(cancellationToken)) {
                if (!_everCachedInstallable) {
                    await CacheInstallablePackagesAsync(true, false, cancellationToken);
                }
                return _availablePackages.FirstOrDefault(p => p.Name == package.Name) ?? new PackageSpec(null);
            }
        }

        private bool WatchingLibrary {
            get {
                if (_historyWatcher == null) {
                    return false;
                }

                return _historyWatcher.EnableRaisingEvents;
            }
            set {
                if (_historyWatcher == null) {
                    return;
                }

                if (_historyWatcher.EnableRaisingEvents != value) {
                    try {
                        _historyWatcher.EnableRaisingEvents = value;
                    } catch (ArgumentException) {
                    } catch (IOException) {
                    } catch (ObjectDisposedException) {
                    }
                }
            }
        }

        private sealed class Suppressed : IDisposable {
            private readonly CondaPackageManager _manager;

            public Suppressed(CondaPackageManager manager) {
                _manager = manager;
            }

            public void Dispose() {
                if (Interlocked.Decrement(ref _manager._suppressCount) == 0) {
                    _manager.WatchingLibrary = true;
                }
            }
        }

        public IDisposable SuppressNotifications() {
            WatchingLibrary = false;
            Interlocked.Increment(ref _suppressCount);
            return new Suppressed(this);
        }

        public void NotifyPackagesChanged() {
            OnHistoryFileChanged(this, EventArgs.Empty);
        }

        private async void OnHistoryFileChanged(object sender, EventArgs e) {
            if (_isDisposed) {
                return;
            }

            InstalledFilesChanged?.Invoke(this, EventArgs.Empty);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var oldCts = Interlocked.Exchange(ref _currentRefresh, cts);
            try {
                oldCts?.Cancel();
                oldCts?.Dispose();
            } catch (ObjectDisposedException) {
            }

            try {
                await CacheInstalledPackagesAsync(false, false, cancellationToken);
            } catch (OperationCanceledException) {
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }
        }
    }
}
