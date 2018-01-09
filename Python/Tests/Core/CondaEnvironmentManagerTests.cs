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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsUITests {
    [TestClass]
    public class CondaEnvironmentManagerTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private static readonly List<string> DeleteFolder = new List<string>();

        private static readonly PackageSpec[] NonExistingPackages = new[] {
            PackageSpec.FromArguments("python=0.1"),
        };

        private static readonly PackageSpec[] Python27Packages = new[] {
            PackageSpec.FromArguments("python=2.7"),
        };

        private static readonly PackageSpec[] Python27AndFlask012Packages = new[] {
            PackageSpec.FromArguments("python=2.7"),
            PackageSpec.FromArguments("flask=0.12"),
        };

        [ClassCleanup]
        public static void DoCleanup() {
            foreach (var folder in DeleteFolder) {
                FileUtils.DeleteDirectory(folder);
            }
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByPath() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envPath = Path.Combine(TestData.GetTempPath(), "newenv");
            bool result = await mgr.CreateAsync(envPath, Python27Packages, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            Assert.IsTrue(Directory.Exists(envPath), "Environment folder not found.");
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Successfully created '{envPath}'")));
            AssertCondaMetaFiles(envPath, "python-2.7.*.json");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByPathNonExistingPackage() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envPath = Path.Combine(TestData.GetTempPath(), "newenvunk");
            bool result = await mgr.CreateAsync(envPath, NonExistingPackages, ui, CancellationToken.None);

            Assert.IsFalse(result, "Create did not fail.");
            Assert.IsTrue(ui.ErrorText.Any(line => line.Contains("The following packages are not available from current channels")));
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Failed to create '{envPath}'")));
            Assert.IsFalse(Directory.Exists(envPath), "Environment folder was found.");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByName() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            GetAvailableEnvironmentName("testenv", out string envName, out string envPath);
            DeleteFolder.Add(envPath);

            bool result = await mgr.CreateAsync(envName, Python27Packages, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            Assert.IsTrue(Directory.Exists(envPath), "Environment folder not found.");
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Successfully created '{envName}'")));
            AssertCondaMetaFiles(envPath, "python-2.7.*.json");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByNameAlreadyExists() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            GetAvailableEnvironmentName("testenv", out string envName, out string envPath);
            DeleteFolder.Add(envPath);

            bool result = await mgr.CreateAsync(envName, Python27Packages, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            Assert.IsTrue(Directory.Exists(envPath), "Environment folder not found.");

            ui.Clear();

            result = await mgr.CreateAsync(envName, Python27Packages, ui, CancellationToken.None);

            Assert.IsFalse(result, "Create did not fail.");
            Assert.IsTrue(ui.ErrorText.Any(line => line.Contains("prefix already exists")));
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Failed to create '{envName}'")));
            Assert.IsTrue(Directory.Exists(envPath), "Environment folder should not have been deleted.");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByNameRelativePath() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            // Relative path is relative to user profile envs folder,
            // passed to conda.exe using -n (by name) argument.
            GetAvailableEnvironmentName(@"tests\\testenvrelative", out string envName, out string envPath);
            DeleteFolder.Add(envPath);

            bool result = await mgr.CreateAsync(envName, Python27Packages, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            Assert.IsTrue(Directory.Exists(envPath), "Environment folder not found.");
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Successfully created '{envName}'")));
            AssertCondaMetaFiles(envPath, "python-2.7.*.json");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByNameInvalidChars() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envName = "<invalid*name>";
            bool result = await mgr.CreateAsync(envName, Python27Packages, ui, CancellationToken.None);

            Assert.IsFalse(result, "Create did not fail.");
            Assert.IsTrue(ui.ErrorText.Any(line => line.Contains($"Invalid name or path '{envName}'")));
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Failed to create '{envName}'")));
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByPathFromEnvironmentFileCondaOnly() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            // cookies conda package
            var envPath = Path.Combine(TestData.GetTempPath(), "conda-only");
            var envFilePath = TestData.GetPath("TestData", "CondaEnvironments", "conda-only.yml");
            bool result = await mgr.CreateFromEnvironmentFileAsync(envPath, envFilePath, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            AssertSitePackagesFile(envPath, "cookies.py");
            AssertCondaMetaFiles(envPath, "cookies-2.2.1*.json", "python-*.json");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByPathFromEnvironmentFileCondaAndPip() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            // python 3.4 conda package, flask conda package and flask_testing pip package
            var envPath = Path.Combine(TestData.GetTempPath(), "conda-and-pip");
            var envFilePath = TestData.GetPath("TestData", "CondaEnvironments", "conda-and-pip.yml");
            var result = await mgr.CreateFromEnvironmentFileAsync(envPath, envFilePath, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            AssertSitePackagesFile(envPath, @"flask\__init__.py");
            AssertSitePackagesFile(envPath, @"flask_testing\__init__.py");
            AssertCondaMetaFiles(envPath, "flask-*.json", "python-3.4.*.json");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByPathFromEnvironmentFileNonExisting() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envPath = Path.Combine(TestData.GetTempPath(), "testenv");
            var envFilePath = TestData.GetPath("TestData", "CondaEnvironments", "non-existing.yml");
            var result = await mgr.CreateFromEnvironmentFileAsync(envPath, envFilePath, ui, CancellationToken.None);

            Assert.IsFalse(result, "Create did not fail.");
            Assert.IsTrue(ui.ErrorText.Any(line => line.Contains($"File not found '{envFilePath}'")));
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Failed to create '{envPath}'")));
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task CreateEnvironmentByPathFromExistingEnvironment() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var sourceEnvPath = await CreatePython27AndFlask012EnvAsync(mgr, ui, "envsrc");
            var envPath = Path.Combine(TestData.GetTempPath(), "envdst");
            var result = await mgr.CreateFromExistingEnvironmentAsync(envPath, sourceEnvPath, ui, CancellationToken.None);

            Assert.IsTrue(result, "Clone failed.");
            AssertCondaMetaFiles(envPath, "flask-*.json", "python-2.7.*.json");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task ExportEnvironmentFile() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envPath = await CreatePython27AndFlask012EnvAsync(mgr, ui, "envtoexport");
            var destinationPath = Path.Combine(TestData.GetTempPath(), "exported.yml");
            var result = await mgr.ExportEnvironmentFileAsync(envPath, destinationPath, ui, CancellationToken.None);

            Assert.IsTrue(result, "Export failed.");

            var definition = File.ReadAllText(destinationPath, Encoding.UTF8);
            Debug.WriteLine($"Contents of '{destinationPath}':");
            Debug.WriteLine(definition);

            AssertUtil.Contains(definition, "name:", "dependencies:", "prefix:");
            AssertUtil.Contains(definition, "python=2.7.", "flask=0.12.");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task ExportExplicitSpecificationFile() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envPath = await CreatePython27AndFlask012EnvAsync(mgr, ui, "envtoexport");
            var destinationPath = Path.Combine(TestData.GetTempPath(), "exported.txt");
            var result = await mgr.ExportExplicitSpecificationFileAsync(envPath, destinationPath, ui, CancellationToken.None);

            Assert.IsTrue(result, "Export failed.");

            var definition = File.ReadAllText(destinationPath, Encoding.UTF8);
            Debug.WriteLine($"Contents of '{destinationPath}':");
            Debug.WriteLine(definition);

            AssertUtil.Contains(definition, "# platform:", "@EXPLICIT");
            AssertUtil.Contains(definition, "python-2.7.", "flask-0.12.");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task DeleteEnvironment() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            var envPath = await CreatePython27AndFlask012EnvAsync(mgr, ui, "envtodelete");
            var result = await mgr.DeleteAsync(envPath, ui, CancellationToken.None);

            Assert.IsTrue(result, "Delete failed.");
            Assert.IsFalse(Directory.Exists(envPath), "Environment folder was not deleted.");
        }

        [TestMethod, Priority(0)]
        [TestCategory("10s")]
        public async Task DeleteEnvironmentNonExisting() {
            var mgr = CreateEnvironmentManager();
            var ui = new MockCondaEnvironmentManagerUI();

            // Try a path that exists but is empty
            var envPath = TestData.GetTempPath();
            var result = await mgr.DeleteAsync(envPath, ui, CancellationToken.None);

            Assert.IsFalse(result, "Delete did not fail.");
            Assert.IsTrue(ui.ErrorText.Any(line => line.Contains("Not a conda environment")));
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Failed to delete '{envPath}'")));

            ui.Clear();

            // Try a path that doesn't exist
            envPath = Path.Combine(TestData.GetTempPath(), "test");
            result = await mgr.DeleteAsync(envPath, ui, CancellationToken.None);

            Assert.IsFalse(result, "Delete did not fail.");
            Assert.IsTrue(ui.ErrorText.Any(line => line.Contains($"Folder not found '{envPath}'")));
            Assert.IsTrue(ui.OutputText.Any(line => line.Contains($"Failed to delete '{envPath}'")));
        }

        private static async Task<string> CreatePython27AndFlask012EnvAsync(CondaEnvironmentManager mgr, MockCondaEnvironmentManagerUI ui, string name) {
            var envPath = Path.Combine(TestData.GetTempPath(), name);
            var result = await mgr.CreateAsync(envPath, Python27AndFlask012Packages, ui, CancellationToken.None);

            Assert.IsTrue(result, "Create failed.");
            AssertCondaMetaFiles(envPath, "flask-*.json", "python-2.7.*.json");

            ui.Clear();

            return envPath;
        }

        private static void AssertCondaMetaFiles(string expectedEnvPath, params string[] fileFilters) {
            foreach (var fileFilter in fileFilters) {
                Assert.IsTrue(Directory.EnumerateFiles(Path.Combine(expectedEnvPath, "conda-meta"), fileFilter).Any(), $"{fileFilter} not found.");
            }
        }

        private static void AssertSitePackagesFile(string envPath, string fileRelativePath) {
            Assert.IsTrue(File.Exists(Path.Combine(envPath, "Lib", "site-packages", fileRelativePath)), $"{fileRelativePath} not found.");
        }

        private static void GetAvailableEnvironmentName(string prefix, out string envName, out string expectedEnvPath) {
            int index = 0;
            do {
                index++;
                envName = $"{prefix}{index}";
                expectedEnvPath = GetExpectedEnvironmentPath(envName);
            } while (Directory.Exists(expectedEnvPath));
        }

        private static string GetExpectedEnvironmentPath(string envName) {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "conda",
                "conda",
                "envs",
                envName
            );
        }

        private static CondaEnvironmentManager CreateEnvironmentManager() {
            if (!PythonPaths.AnacondaVersions.Any()) {
                Assert.Inconclusive("Anaconda is required.");
            }

            var container = CreateCompositionContainer();
            var interpreters = container.GetExportedValue<IInterpreterRegistryService>();
            var mgr = CondaEnvironmentManager.Create(interpreters);
            Assert.IsNotNull(mgr, "Could not create conda environment manager.");
            return mgr;
        }

        private static CompositionContainer CreateCompositionContainer(bool defaultProviders = true) {
            var sp = new MockServiceProvider();
            sp.Services[typeof(SVsActivityLog).GUID] = new MockActivityLog();
            var settings = new MockSettingsManager();
            sp.Services[typeof(SVsSettingsManager).GUID] = settings;

            return InterpreterCatalog.CreateContainer(typeof(IInterpreterRegistryService), typeof(IInterpreterOptionsService));
        }

        class MockCondaEnvironmentManagerUI : ICondaEnvironmentManagerUI {
            public readonly List<string> ErrorText = new List<string>();
            public readonly List<string> OperationFinished = new List<string>();
            public readonly List<string> OperationStarted = new List<string>();
            public readonly List<string> OutputText = new List<string>();

            public void Clear() {
                ErrorText.Clear();
                OperationFinished.Clear();
                OperationStarted.Clear();
                OutputText.Clear();
            }

            public void OnErrorTextReceived(ICondaEnvironmentManager sender, string text) {
                ErrorText.Add(text);
                Debug.WriteLine(text);
            }

            public void OnOperationFinished(ICondaEnvironmentManager sender, string operation, bool success) {
                OperationFinished.Add(operation);
                Debug.WriteLine(operation + $"; success={success}");
            }

            public void OnOperationStarted(ICondaEnvironmentManager sender, string operation) {
                OperationStarted.Add(operation);
                Debug.WriteLine(operation);
            }

            public void OnOutputTextReceived(ICondaEnvironmentManager sender, string text) {
                OutputText.Add(text);
                Debug.WriteLine(text);
            }
        }
    }
}
