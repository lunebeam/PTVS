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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace PythonToolsUITestsRunner {
    [TestClass]
    public class SnippetsTests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.PythonToolsUITests",
            // Remote class name
            $"PythonToolsUITests.{GetType().Name}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion


        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestBasicSnippetsTab() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestBasicSnippetsTab));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestPassSelected() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestPassSelected));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestPassSelectedIndented() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestPassSelectedIndented));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestSurroundWith() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestSurroundWith));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestSurroundWithMultiline() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestSurroundWithMultiline));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestInsertSnippet() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestInsertSnippet));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestInsertSnippetEmptySelectionNonEmptyLine() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestInsertSnippetEmptySelectionNonEmptyLine));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTestClassSnippet() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestTestClassSnippet));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTestClassSnippetBadImport() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestTestClassSnippetBadImport));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTestClassSnippetImportAs() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestTestClassSnippetImportAs));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTestClassSnippetUnitTestImported() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestTestClassSnippetUnitTestImported));
        }

        /// <summary>
        /// Starting a nested session should dismiss the initial session
        /// </summary>
        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestNestedSession() {
            _vs.RunTest(nameof(PythonToolsUITests.SnippetsTests.TestNestedSession));
        }
    }
}
