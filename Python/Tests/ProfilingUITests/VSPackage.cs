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
using Microsoft.VisualStudio.Shell;
using TestRunnerInterop;
using TestUtilities.UI;

namespace ProfilingUITests {
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutomationObject(AutomationObject)]
    public sealed class VSPackage : Package {
        public const string AutomationObject = "Microsoft.PythonTools.Tests.ProfilingUITests";

        private readonly Lazy<IVsHostedPythonToolsTest> _testRunner = new Lazy<IVsHostedPythonToolsTest>(() =>
            new HostedPythonToolsTestRunner(
                typeof(VSPackage).Assembly,
                new Guid("F4A63B2A-49AB-4b2d-AA59-A10F01026C89") // Profiling
            )
        );

        protected override object GetAutomationObject(string name) {
            if (name == AutomationObject) {
                return _testRunner.Value;
            }
            return base.GetAutomationObject(name);
        }
    }
}
