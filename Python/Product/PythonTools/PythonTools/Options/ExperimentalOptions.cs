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
using EO = Microsoft.PythonTools.Interpreter.ExperimentalOptions;

namespace Microsoft.PythonTools.Options {
    public sealed class ExperimentalOptions {
        internal ExperimentalOptions(PythonToolsService service) {
            Load();
        }

        public void Load() {
            NoDatabaseFactory = EO.GetNoDatabaseFactory();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            EO.NoDatabaseFactory = NoDatabaseFactory;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            NoDatabaseFactory = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

        /// <summary>
        /// True to use the experimental feature of interpreter factories
        /// without old-style completion databases.
        /// </summary>
        public bool NoDatabaseFactory { get; set; }
    }
}
