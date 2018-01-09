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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Detects interpreters in user-created conda environments.
    /// </summary>
    /// <remarks>
    /// Uses %HOMEPATH%/.conda/environments.txt and `conda info --envs`.
    /// </remarks>
    [InterpreterFactoryId(FactoryProviderName)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [Export(typeof(CondaEnvironmentFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CondaEnvironmentFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private readonly IServiceProvider _site;
        private readonly Dictionary<string, PythonInterpreterInformation> _factories = new Dictionary<string, PythonInterpreterInformation>();
        internal const string FactoryProviderName = "CondaEnv";
        internal const string EnvironmentCompanyName = "CondaEnv";

        private bool _isDisposed;
        private int _ignoreNotifications;
        private bool _initialized;
        private readonly CPythonInterpreterFactoryProvider _globalProvider;
        private readonly bool _watchFileSystem;
        private FileSystemWatcher _envsTxtWatcher;
        private Timer _envsTxtWatcherTimer;
        private string _environmentsTxtPath;

        [ImportingConstructor]
        public CondaEnvironmentFactoryProvider(
            [Import] CPythonInterpreterFactoryProvider globalProvider,
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null,
            [Import("Microsoft.VisualStudioTools.MockVsTests.IsMockVs", AllowDefault = true)] object isMockVs = null
        ) {
            _site = site;
            _watchFileSystem = isMockVs == null;
            _globalProvider = globalProvider;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CondaEnvironmentFactoryProvider() {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                _isDisposed = true;
                lock (_factories) {
                    if (_envsTxtWatcher != null) {
                        _envsTxtWatcher.Dispose();
                    }
                    if (_envsTxtWatcherTimer != null) {
                        _envsTxtWatcherTimer.Dispose();
                    }
                }
            }
        }

        private void EnsureInitialized() {
            if (_initialized) {
                return;
            }

            bool doDiscover = false;
            lock (_factories) {
                if (!_initialized) {
                    _initialized = true;
                    doDiscover = true;
                    try {
                        _environmentsTxtPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".conda",
                            "environments.txt"
                        );
                    } catch (ArgumentException) {
                    }

                    if (_watchFileSystem && !string.IsNullOrEmpty(_environmentsTxtPath)) {
                        // Watch the file %HOMEPATH%/.conda/Environments.txt which
                        // is updated by conda after a new environment is created.
                        var watchedPath = Path.GetDirectoryName(_environmentsTxtPath);
                        if (Directory.Exists(watchedPath)) {
                            try {
                                _envsTxtWatcher = new FileSystemWatcher(watchedPath, "*.txt");
                                _envsTxtWatcher.Changed += _envsTxtWatcher_Changed;
                                _envsTxtWatcher.Created += _envsTxtWatcher_Changed;
                                _envsTxtWatcher.EnableRaisingEvents = true;
                                _envsTxtWatcherTimer = new Timer(_envsTxtWatcherTimer_Elapsed);
                            } catch (ArgumentException) {
                            } catch (IOException) {
                            }
                        }
                    }
                }
            }

            if (doDiscover) {
                DiscoverInterpreterFactories();
            }
        }

        private void _envsTxtWatcherTimer_Elapsed(object state) {
            try {
                lock (_factories) {
                    _envsTxtWatcherTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }

                DiscoverInterpreterFactories();
            } catch (ObjectDisposedException) {
            }
        }

        private void _envsTxtWatcher_Changed(object sender, FileSystemEventArgs e) {
            if (PathUtils.IsSamePath(e.FullPath, _environmentsTxtPath)) {
                lock (_factories) {
                    try {
                        _envsTxtWatcherTimer.Change(1000, Timeout.Infinite);
                    } catch (ObjectDisposedException) {
                    }
                }
            }
        }

        private void DiscoverInterpreterFactories() {
            if (Volatile.Read(ref _ignoreNotifications) > 0) {
                return;
            }

            // Discover the available interpreters...
            bool anyChanged = false;

            List<PythonInterpreterInformation> found = null;

            try {
                // Try to find an existing root conda installation
                // If the future we may decide to install a private installation of conda/miniconda
                var globalFactories = _globalProvider.GetInterpreterFactories().ToList();
                var mainCondaExePath = CondaUtils.GetLatestCondaExecutablePath(globalFactories);
                if (mainCondaExePath != null) {
                    found = FindCondaEnvironments(mainCondaExePath).ToList();
                }
            } catch (ObjectDisposedException) {
                // We are aborting, so silently return with no results.
                return;
            }

            var uniqueIds = new HashSet<string>(found.Select(i => i.Configuration.Id));

            // Then update our cached state with the lock held.
            lock (_factories) {
                foreach (var info in found.MaybeEnumerate()) {
                    PythonInterpreterInformation existingInfo;
                    if (!_factories.TryGetValue(info.Configuration.Id, out existingInfo) ||
                        info.Configuration != existingInfo.Configuration) {
                        _factories[info.Configuration.Id] = info;
                        anyChanged = true;
                    }
                }

                // Remove any factories we had before and no longer see...
                foreach (var unregistered in _factories.Keys.Except(uniqueIds).ToArray()) {
                    _factories.Remove(unregistered);
                    anyChanged = true;
                }
            }

            if (anyChanged) {
                OnInterpreterFactoriesChanged();
            }
        }

        private static CondaInfoResult ExecuteCondaInfo(string condaPath) {
            using (var output = ProcessOutput.RunHiddenAndCapture(condaPath, "info", "--json")) {
                output.Wait();
                if (output.ExitCode == 0) {
                    var json = string.Join(Environment.NewLine, output.StandardOutputLines);
                    try {
                        return JsonConvert.DeserializeObject<CondaInfoResult>(json);
                    } catch (JsonException ex) {
                        Debug.WriteLine("Failed to parse: {0}".FormatInvariant(ex.Message));
                        Debug.WriteLine(json);
                        return null;
                    }
                }
                return null;
            }
        }

        class CondaInfoResult {
            [JsonProperty("envs")]
            public string[] EnvironmentFolders = null;

            [JsonProperty("envs_dirs")]
            public string[] EnvironmentRootFolders = null;
        }

        private static readonly bool FindUsingCondaInfo = true;
        private static readonly bool FindUsingEnvironmentsTxt = false; // Not necessary for conda 4.4+

        private List<PythonInterpreterInformation> FindCondaEnvironments(string condaPath) {
            var found = new List<PythonInterpreterInformation>();
            var watchFolders = new HashSet<string>();

            if (FindUsingCondaInfo) {
                // Find environments that were created with "conda create -n <name>"
                var condaInfoResult = ExecuteCondaInfo(condaPath);
                if (condaInfoResult != null) {
                    foreach (var folder in condaInfoResult.EnvironmentFolders) {
                        if (!Directory.Exists(folder)) {
                            continue;
                        }

                        PythonInterpreterInformation env = CreateEnvironmentInfo(folder);
                        if (env != null) {
                            found.Add(env);
                        }
                    }
                }
            }

            if (FindUsingEnvironmentsTxt) {
                // Find environments that were created with "conda create -p <folder>"
                // Note that this may have a bunch of entries that no longer exist
                // as well as duplicates that were returned by conda info.
                if (File.Exists(_environmentsTxtPath)) {
                    try {
                        var folders = File.ReadAllLines(_environmentsTxtPath);
                        foreach (var folder in folders) {
                            if (!Directory.Exists(folder)) {
                                continue;
                            }

                            if (found.FirstOrDefault(pii => PathUtils.IsSameDirectory(pii.Configuration.PrefixPath, folder)) != null) {
                                continue;
                            }

                            PythonInterpreterInformation env = CreateEnvironmentInfo(folder);
                            if (env != null) {
                                found.Add(env);
                            }
                        }
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    }
                }
            }

            return found;
        }

        private static PythonInterpreterInformation CreateEnvironmentInfo(string prefixPath) {
            var name = Path.GetFileName(prefixPath);
            var description = name;
            var vendor = Strings.CondaEnvironmentDescription;
            var vendorUrl = string.Empty;
            var supportUrl = string.Empty;
            var interpreterPath = Path.Combine(prefixPath, CondaEnvironmentFactoryConstants.ConsoleExecutable);
            var windowsInterpreterPath = Path.Combine(prefixPath, CondaEnvironmentFactoryConstants.WindowsExecutable);

            InterpreterArchitecture arch = InterpreterArchitecture.Unknown;
            Version version = null;

            if (File.Exists(interpreterPath)) {
                using (var output = ProcessOutput.RunHiddenAndCapture(
                    interpreterPath, "-c", "import sys; print('%s.%s' % (sys.version_info[0], sys.version_info[1]))"
                )) {
                    output.Wait();
                    if (output.ExitCode == 0) {
                        var versionName = output.StandardOutputLines.FirstOrDefault() ?? "";
                        if (!Version.TryParse(versionName, out version)) {
                            version = null;
                        }
                    }
                }

                arch = CPythonInterpreterFactoryProvider.ArchitectureFromExe(interpreterPath);
            } else {
                return null;
            }

            var config = new InterpreterConfiguration(
                CondaEnvironmentFactoryConstants.GetInterpreterId(CondaEnvironmentFactoryProvider.EnvironmentCompanyName, name),
                description,
                prefixPath,
                interpreterPath,
                windowsInterpreterPath,
                CondaEnvironmentFactoryConstants.PathEnvironmentVariableName,
                arch,
                version
            );

            var unique = new PythonInterpreterInformation(
                config,
                vendor,
                vendorUrl,
                supportUrl
            );
            return unique;
        }

        #region IPythonInterpreterProvider Members

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            if (!ExperimentalOptions.AutoDetectCondaEnvironments) {
                return Enumerable.Empty<InterpreterConfiguration>();
            }

            EnsureInitialized();

            lock (_factories) {
                return _factories.Values.Select(x => x.Configuration).ToArray();
            }
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            if (!ExperimentalOptions.AutoDetectCondaEnvironments) {
                return null;
            }

            EnsureInitialized();

            PythonInterpreterInformation info;
            lock (_factories) {
                _factories.TryGetValue(id, out info);
            }

            return info?.GetOrCreateFactory(CreateFactory);
        }

        private IPythonInterpreterFactory CreateFactory(PythonInterpreterInformation info) {
            if (!ExperimentalOptions.NoDatabaseFactory) {
                return new LegacyDB.CPythonInterpreterFactory(
                    info.Configuration,
                    new InterpreterFactoryCreationOptions {
                        WatchFileSystem = true,
                        DatabasePath = DatabasePathSelector.CalculateGlobalDatabasePath(info.Configuration, LegacyDB.PythonTypeDatabase.FormatVersion)
                    }
                );
            }

            return new Ast.AstPythonInterpreterFactory(
                info.Configuration,
                new InterpreterFactoryCreationOptions {
                    WatchFileSystem = true,
                    DatabasePath = DatabasePathSelector.CalculateVSLocalDatabasePath(_site, info.Configuration, 1)
                }
            );
        }

        private EventHandler _interpFactoriesChanged;
        public event EventHandler InterpreterFactoriesChanged {
            add {
                if (ExperimentalOptions.AutoDetectCondaEnvironments) {
                    EnsureInitialized();
                }
                _interpFactoriesChanged += value;
            }
            remove {
                _interpFactoriesChanged -= value;
            }
        }

        private void OnInterpreterFactoriesChanged() {
            _interpFactoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public object GetProperty(string id, string propName) {
            if (!ExperimentalOptions.AutoDetectCondaEnvironments) {
                return null;
            }

            PythonInterpreterInformation info;

            switch (propName) {
                case PythonRegistrySearch.CompanyPropertyKey:
                    lock (_factories) {
                        if (_factories.TryGetValue(id, out info)) {
                            return info.Vendor;
                        }
                    }
                    break;
                case PythonRegistrySearch.SupportUrlPropertyKey:
                    lock (_factories) {
                        if (_factories.TryGetValue(id, out info)) {
                            return info.SupportUrl;
                        }
                    }
                    break;
                case "PersistInteractive":
                    return true;
            }

            return null;
        }

#endregion
    }
}
