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
using System.ComponentModel.Composition;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    [Export(typeof(IPackageManagerProvider))]
    sealed class CPythonPipPackageManagerProvider : IPackageManagerProvider {
        class PipCommandsV2 : PipPackageManagerCommands {
            public override IEnumerable<string> Base() => new[] { "-c", ProcessOutput.QuoteSingleArgument("import pip; pip.main()") };
            public override IEnumerable<string> Prepare() => new[] { PythonToolsInstallPath.GetFile("pip_downloader.py", typeof(PipPackageManager).Assembly) };
        }

        class PipCommandsV3 : PipPackageManagerCommands {
            public override IEnumerable<string> Prepare() => new[] { PythonToolsInstallPath.GetFile("pip_downloader.py", typeof(PipPackageManager).Assembly) };
        }

        private static readonly PipPackageManagerCommands CommandsV2 = new PipCommandsV2();
        private static readonly PipPackageManagerCommands CommandsV3 = new PipCommandsV3();

        public IEnumerable<IPackageManager> GetPackageManagers(IPythonInterpreterFactory factory) {
            IPackageManager pm = null;
            try {
                pm = new PipPackageManager(factory, factory.Configuration.Version.Major >= 3 ? CommandsV3 : CommandsV2, 1000);
            } catch (NotSupportedException) {
            }
            
            if (pm != null) {
                yield return pm;
            }
        }
    }
}
