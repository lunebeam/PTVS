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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace PythonToolsUITestsRunner {
    [TestClass]
    public class BasicProjectTests {
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

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void TemplateDirectories() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.TemplateDirectories));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void UserProjectFile() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.UserProjectFile));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void SetDefaultInterpreter() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.SetDefaultInterpreter));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void LoadPythonProject() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.LoadPythonProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void LoadPythonProjectWithNoConfigurations() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.LoadPythonProjectWithNoConfigurations));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void SaveProjectAs() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.SaveProjectAs));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void RenameProjectTest() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.RenameProjectTest));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectAddItem() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectAddItem));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectAddFolder() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectAddFolder));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectAddFolderThroughUI() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectAddFolderThroughUI));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void AddExistingFolder() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddExistingFolder));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void AddExistingFolderWhileDebugging() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddExistingFolderWhileDebugging));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectBuild() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectBuild));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectRenameAndDeleteItem() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectRenameAndDeleteItem));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ChangeDefaultInterpreterProjectClosed() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ChangeDefaultInterpreterProjectClosed));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AddTemplateItem() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddTemplateItem));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AutomationProperties() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AutomationProperties));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectItemAutomation() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectItemAutomation));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void RelativePaths() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.RelativePaths));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectConfiguration() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectConfiguration));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DependentNodes() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DependentNodes));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void PythonSearchPaths() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.PythonSearchPaths));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DotNetReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DotNetReferences));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DotNetSearchPathReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DotNetSearchPathReferences));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void DotNetProjectReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DotNetProjectReferences));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void DotNetAssemblyReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DotNetAssemblyReferences));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void MultipleDotNetAssemblyReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.MultipleDotNetAssemblyReferences));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void MultiProjectAnalysis() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.MultiProjectAnalysis));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CProjectReference() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.CProjectReference));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DeprecatedPydReferenceNode() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DeprecatedPydReferenceNode));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DeprecatedWebPIReferenceNode() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DeprecatedWebPIReferenceNode));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void AddFolderExists() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddFolderExists));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void AddFolderCopyAndPasteFile() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddFolderCopyAndPasteFile));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CopyAndPasteFolder() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.CopyAndPasteFolder));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AddFromFileInSubDirectory() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddFromFileInSubDirectory));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AddFromFileOutsideOfProject() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddFromFileOutsideOfProject));
        }

        [TestMethod, Priority(2)]
        [TestCategory("Installed")]
        public void CopyFolderWithMultipleItems() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.CopyFolderWithMultipleItems));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void OpenInteractiveFromSolutionExplorer() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.OpenInteractiveFromSolutionExplorer));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void LoadProjectWithDuplicateItems() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.LoadProjectWithDuplicateItems));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void PreviewFile() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.PreviewFile));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void PreviewMissingFile() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.PreviewMissingFile));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SaveWithDataLoss() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.SaveWithDataLoss));
        }
    }
}
