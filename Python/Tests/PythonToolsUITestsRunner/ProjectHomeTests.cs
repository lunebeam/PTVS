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
    public class ProjectHomeTests {
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
        public void LoadRelativeProjects() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.LoadRelativeProjects));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AddDeleteItem() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.AddDeleteItem));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void AddDeleteItem2() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.AddDeleteItem2));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void AddDeleteFolder() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.AddDeleteFolder));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AddDeleteSubfolder() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.AddDeleteSubfolder));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void SaveProjectAndCheckProjectHome() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.SaveProjectAndCheckProjectHome));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void DragDropRelocatedTest() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.DragDropRelocatedTest));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void CutPasteRelocatedTest() {
            _vs.RunTest(nameof(PythonToolsUITests.ProjectHomeTests.CutPasteRelocatedTest));
        }
    }
}
