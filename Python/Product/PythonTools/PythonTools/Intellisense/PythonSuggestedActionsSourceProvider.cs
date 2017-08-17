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

using System.ComponentModel.Composition;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Intellisense {
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Python Suggested Actions")]
    [ContentType(PythonCoreConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Analyzable)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class PythonSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider {
        [Import]
        internal PythonEditorServices _services = null;

        public ISuggestedActionsSource CreateSuggestedActionsSource(
            ITextView textView,
            ITextBuffer textBuffer
        ) {
            if (textView == null && textBuffer == null) {
                return null;
            }
            return new PythonSuggestedActionsSource(_services, textView, textBuffer);
        }
    }
}
