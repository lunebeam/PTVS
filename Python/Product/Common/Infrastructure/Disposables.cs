// Visual Studio Shared Project
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

namespace Microsoft.PythonTools.Infrastructure {
    /// <summary>
    /// Provides a set of static methods for creating Disposables.
    /// </summary>
    /// <remarks>
    /// Made to look identical to System.Reactive.Disposables.Disposable.
    /// </remarks>
    public static class Disposable {
        /// <summary>
        /// Gets the disposable that does nothing when disposed.
        /// </summary>
        public static IDisposable Empty => Create(null);

        /// <summary>
        /// Creates the disposable that invokes the specified action when disposed.
        /// </summary>
        /// <param name="dispose">The action to run during <see cref="IDisposable.Dispose"/>.</param>
        /// <returns>The disposable object that runs the given action upon disposal.</returns>
        public static IDisposable Create(Action dispose) {
            return new ActionDisposable(dispose);
        }
    }

    internal sealed class ActionDisposable : IDisposable {
        private readonly Action _dispose;

        public ActionDisposable(Action dispose) {
            _dispose = dispose;
        }

        public void Dispose() {
            _dispose?.Invoke();
        }
    }
}
