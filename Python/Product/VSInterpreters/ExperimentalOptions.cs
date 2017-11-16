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
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    public static class ExperimentalOptions {
        private const string ExperimentSubkey = @"Software\Microsoft\PythonTools\Experimental";
        internal const string NoDatabaseFactoryKey = "NoDatabaseFactory";
        internal const string AutoDetectCondaEnvironmentsKey = "AutoDetectCondaEnvironments";
        internal const string UseCondaPackageManagerKey = "UseCondaPackageManager";
        internal static readonly Lazy<bool> _noDatabaseFactory = new Lazy<bool>(GetNoDatabaseFactory);
        internal static readonly Lazy<bool> _autoDetectCondaEnvironments = new Lazy<bool>(GetAutoDetectCondaEnvironments);
        internal static readonly Lazy<bool> _useCondaPackageManager = new Lazy<bool>(GetUseCondaPackageManager);

        public static bool GetNoDatabaseFactory() => GetBooleanFlag(NoDatabaseFactoryKey, defaultVal: true);
        public static bool GetAutoDetectCondaEnvironments() => GetBooleanFlag(AutoDetectCondaEnvironmentsKey, defaultVal: false);
        public static bool GetUseCondaPackageManager() => GetBooleanFlag(UseCondaPackageManagerKey, defaultVal: false);

        private static bool GetBooleanFlag(string keyName, bool defaultVal) {
            using (var root = Registry.CurrentUser.OpenSubKey(ExperimentSubkey, false)) {
                var value = root?.GetValue(keyName);
                if (value == null) {
                    return defaultVal;
                }
                int? asInt = value as int?;
                if (asInt.HasValue) {
                    if (asInt.GetValueOrDefault() == 0) {
                        // REG_DWORD but 0 means no experiment
                        return false;
                    }
                } else if (string.IsNullOrEmpty(value as string)) {
                    // Empty string or no value means no experiment
                    return false;
                }
            }
            return true;
        }

        private static void SetBooleanFlag(string keyName, bool value) {
            using (var root = Registry.CurrentUser.CreateSubKey(ExperimentSubkey, true)) {
                if (root == null) {
                    throw new UnauthorizedAccessException();
                }
                if (value) {
                    root.SetValue(keyName, 1);
                } else {
                    root.SetValue(keyName, 0);
                }
            }
        }

        /// <summary>
        /// Returns the setting for the NoDatabaseFactory experiment.
        /// </summary>
        /// <remarks>
        /// The value returned is determined at the start of the session and
        /// cannot be modified while running.
        /// </remarks>
        public static bool NoDatabaseFactory {
            get {
                return _noDatabaseFactory.Value;
            }
            set {
                SetBooleanFlag(NoDatabaseFactoryKey, value);
            }
        }

        public static bool AutoDetectCondaEnvironments {
            get {
                return _autoDetectCondaEnvironments.Value;
            }
            set {
                SetBooleanFlag(AutoDetectCondaEnvironmentsKey, value);
            }
        }

        public static bool UseCondaPackageManager {
            get {
                return _useCondaPackageManager.Value;
            }
            set {
                SetBooleanFlag(UseCondaPackageManagerKey, value);
            }
        }
    }
}
