﻿// Visual Studio Shared Project
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

namespace Microsoft.VisualStudioTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideDebugAdapterAttribute : RegistrationAttribute {
        private readonly string _debugAdapterHostCLSID = "{DAB324E9-7B35-454C-ACA8-F6BB0D5C8673}";
        private readonly string _name;
        private readonly string _engineId;
        private readonly string _adapterLauncherCLSID;
        private readonly string _languageName;
        private readonly string _languageId;
        private readonly Type _adapterLauncherType;

        public ProvideDebugAdapterAttribute(string name, string engineId, string adapterLauncherCLSID, string languageName, string languageId, Type adapterLauncherType) {
            _name = name;
            _engineId = engineId;
            _adapterLauncherCLSID = adapterLauncherCLSID;
            _languageName = languageName;
            _languageId = languageId;
            _adapterLauncherType = adapterLauncherType;
        }

        public override void Register(RegistrationContext context) {
            var engineKey = context.CreateKey("AD7Metrics\\Engine\\" + _engineId);


            // The following this line are boiler-plate settings required by all debug adapters.
            // Indicates that the "Debug Adapter Host" engine should be used
            engineKey.SetValue("CLSID", _debugAdapterHostCLSID);

            // Indicates that the engine should be loaded directly by VS
            engineKey.SetValue("AlwaysLoadLocal", 1);

            // Address and callstack breakpoints are not currently supported by the Debug Adapter Host
            engineKey.SetValue("AddressBP", 0);
            engineKey.SetValue("CallStackBP", 0);

            /*
             * "Attach to Process" support
             * To support attaching via the VS "Attach to Process" dialog:
             *     - Set the "Attach" property to "1" below
             *     - Provide a port supplier GUID.  To attach to processes on the local machine by PID, the default
             *         port supplier is suffient, and can be used by uncommenting the "PortSupplier" property below.
             *     - Provide a custom IAdapterLauncher implementation to generate launch configuration JSON
             *         for the adapter based on the selection in the "Attach to Process" dialog, and specify
             *         its CLSID in the "AdapterLauncher" property below.
             */
            engineKey.SetValue("Attach", 1);
            // engineKey.SetValue("PortSupplier", "{708C1ECA-FF48-11D2-904F-00C04FA302A1}");
            engineKey.SetValue("AdapterLauncher", _adapterLauncherCLSID);

            /*
             * Set to "1" if the debug adapter will use the VS "Exception Setting" tool window.  The debug adapter's must
             * support one of the following:
             *     -Exception Breakpoints
             *         The debug adapter's response to the "initialize" request must contain a set of ExceptionBreakpointFilters,
             *         and the "ExceptionBreakpointCategory" property must be defined below.An optional set of
             *         "ExceptionBreakpointMappings" may also be provided if the VS exception names do not correspond to the
             *         "Label" properties of the ExceptionBreakpointFilters.
             *     -Exception Options
             *         The debug adapter's response to the "initialize" request must contain the "SupportsExceptionOptions"
             *         and "SupportsExceptionDetailsRequest" flags, and ExceptionCategoryMapping information must be supplied.
             */
            engineKey.SetValue("Exceptions", 1);

            /*
             * Set to "1" if the debug adapter supports the VS exception conditions experience(For skipping exceptions in specific modules).
             * The debug adapter's response to the "initialize" request must contain the "SupportsExceptionConditions"
             * and "SupportsExceptionDetailsRequest" flags.
             */
            engineKey.SetValue("ExceptionConditions", 0);

            /*
             * Debug Adapter Host settings
             * These settings control the behavior of the Debug Adapter Host
             *
             * Name of the debug adapter
             * This appears in VS in several places.  For example:
             *     -The "Select Code Type" dialog for choosing which debugger to attach to a process(if Attach is supported)
             *     -The "Debugging" column of the "Processes" tool window
             */
            engineKey.SetValue("Name", _name);

            /*
             * Path to the debug adapter executable
             */
            // engineKey.SetValue("Adapter", @"$PackageFolder$\DebugAdapter.exe");

            /*
             * Arguments for the debug adapter executable (optional)
             */
            // engineKey.SetValue("AdapterArgs", "");

            /*
             * Language name
             * This appears in (e.g.) the "Language" column of the Stack Trace tool window.
             */
            engineKey.SetValue("Language", _languageName);
            engineKey.SetValue("LanguageId", _languageId);

            /* 
             * Adapter launcher registration 
             */
            var adapterKey = context.CreateKey($"CLSID\\{_adapterLauncherCLSID}");
            var assembly = _adapterLauncherType.Assembly.GetName().Name;
            var className = _adapterLauncherType.FullName;
            adapterKey.SetValue("Assembly", assembly);
            adapterKey.SetValue("Class", className);
            adapterKey.SetValue("CodeBase", $@"$PackageFolder$\{assembly}.dll");
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
