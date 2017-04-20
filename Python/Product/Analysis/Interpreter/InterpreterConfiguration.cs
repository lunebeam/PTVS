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
using System.Linq;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class InterpreterConfiguration : IEquatable<InterpreterConfiguration> {
        private readonly string _description;
        private string _fullDescription;

        /// <summary>
        /// <para>Constructs a new interpreter configuration based on the
        /// provided values.</para>
        /// <para>No validation is performed on the parameters.</para>
        /// <para>If winPath is null or empty,
        /// <see cref="WindowsInterpreterPath"/> will be set to path.</para>
        /// </summary>
        public InterpreterConfiguration(
            string id,
            string description,
            string prefixPath = null,
            string path = null,
            string winPath = "",
            string pathVar = "",
            InterpreterArchitecture arch = default(InterpreterArchitecture),
            Version version = null,
            InterpreterUIMode uiMode = InterpreterUIMode.Normal
        ) {
            Id = id;
            _description = description ?? "";
            PrefixPath = prefixPath;
            InterpreterPath = path;
            WindowsInterpreterPath = string.IsNullOrEmpty(winPath) ? path : winPath;
            PathEnvironmentVariable = pathVar;
            Architecture = arch ?? InterpreterArchitecture.Unknown;
            Version = version ?? new Version();
            UIMode = uiMode;
        }

        /// <summary>
        /// Gets a unique and stable identifier for this interpreter.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets a friendly description of the interpreter
        /// </summary>
        public string Description => _fullDescription ?? _description;

        /// <summary>
        /// Changes the description to be less likely to be
        /// ambiguous with other interpreters.
        /// </summary>
        private void SwitchToFullDescription() {
            bool hasVersion = _description.Contains(Version.ToString());
            bool hasArch = _description.IndexOf(Architecture.ToString(), StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                _description.IndexOf(Architecture.ToString("x"), StringComparison.CurrentCultureIgnoreCase) >= 0;

            if (hasVersion && hasArch) {
                // Regular description is sufficient
                _fullDescription = null;
            } else if (hasVersion) {
                _fullDescription = "{0} ({1})".FormatUI(_description, Architecture);
            } else if (hasArch) {
                _fullDescription = "{0} ({1})".FormatUI(_description, Version);
            } else {
                _fullDescription = "{0} ({1}, {2})".FormatUI(_description, Version, Architecture);
            }
        }

        /// <summary>
        /// Returns the prefix path of the Python installation. All files
        /// related to the installation should be underneath this path.
        /// </summary>
        public string PrefixPath { get; }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications.
        /// </summary>
        public string InterpreterPath { get; }

        /// <summary>
        /// Returns the path to the interpreter executable for launching Python
        /// applications which are windows applications (pythonw.exe, ipyw.exe).
        /// </summary>
        public string WindowsInterpreterPath { get; }

        /// <summary>
        /// Gets the environment variable which should be used to set sys.path.
        /// </summary>
        public string PathEnvironmentVariable { get; }

        /// <summary>
        /// The architecture of the interpreter executable.
        /// </summary>
        public InterpreterArchitecture Architecture { get; }

        public string ArchitectureString => Architecture.ToString();

        /// <summary>
        /// The language version of the interpreter (e.g. 2.7).
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// The UI behavior of the interpreter.
        /// </summary>
        /// <remarks>
        /// New in 2.2
        /// </remarks>
        public InterpreterUIMode UIMode { get; }

        public static bool operator ==(InterpreterConfiguration x, InterpreterConfiguration y)
            => x?.Equals(y) ?? object.ReferenceEquals(y, null);
        public static bool operator !=(InterpreterConfiguration x, InterpreterConfiguration y)
            => !(x?.Equals(y) ?? object.ReferenceEquals(y, null));

        public override bool Equals(object obj) => Equals(obj as InterpreterConfiguration);

        public bool Equals(InterpreterConfiguration other) {
            if (other == null) {
                return false;
            }

            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.Equals(PrefixPath, other.PrefixPath) &&
                string.Equals(Id, other.Id) &&
                cmp.Equals(Description, other.Description) &&
                cmp.Equals(InterpreterPath, other.InterpreterPath) &&
                cmp.Equals(WindowsInterpreterPath, other.WindowsInterpreterPath) &&
                cmp.Equals(PathEnvironmentVariable, other.PathEnvironmentVariable) &&
                Architecture == other.Architecture &&
                Version == other.Version &&
                UIMode == other.UIMode;
        }

        public override int GetHashCode() {
            var cmp = StringComparer.OrdinalIgnoreCase;
            return cmp.GetHashCode(PrefixPath ?? "") ^
                Id.GetHashCode() ^
                cmp.GetHashCode(Description) ^
                cmp.GetHashCode(InterpreterPath ?? "") ^
                cmp.GetHashCode(WindowsInterpreterPath ?? "") ^
                cmp.GetHashCode(PathEnvironmentVariable ?? "") ^
                Architecture.GetHashCode() ^
                Version.GetHashCode() ^
                UIMode.GetHashCode();
        }

        public override string ToString() {
            return Description;
        }

        /// <summary>
        /// Attempts to update descriptions to be unique within the
        /// provided sequence by adding information about the
        /// interpreter that is missing from the default description.
        /// </summary>
        public static void DisambiguateDescriptions(IReadOnlyList<InterpreterConfiguration> configs) {
            foreach (var c in configs) {
                c._fullDescription = null;
            }
            foreach (var c in configs.GroupBy(i => i._description ?? "").Where(g => g.Count() > 1).SelectMany()) {
                c.SwitchToFullDescription();
            }
        }
    }
}
