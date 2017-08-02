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
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Navigation.Navigable {
    class NavigableSymbol : INavigableSymbol {
        private readonly IServiceProvider _serviceProvider;

        public NavigableSymbol(IServiceProvider serviceProvider, AnalysisLocation location, SnapshotSpan span) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Location = location ?? throw new ArgumentNullException(nameof(location));
            SymbolSpan = span;
        }

        public SnapshotSpan SymbolSpan { get; }

        internal AnalysisLocation Location { get; }

        // FYI: This is for future extensibility, it's currently ignored (in 15.3)
        public IEnumerable<INavigableRelationship> Relationships =>
            new List<INavigableRelationship>() { PredefinedNavigableRelationships.Definition };

        public void Navigate(INavigableRelationship relationship) {
            try {
                Location.GotoSource(_serviceProvider);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                MessageBox.Show(Strings.CannotGoToDefn_Name.FormatUI(SymbolSpan.GetText()), Strings.ProductTitle);
            }
        }
    }
}
