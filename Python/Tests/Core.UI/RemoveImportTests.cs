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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class RemoveImportTests {
        public void FromImport1(VisualStudioApp app) {
            string expectedText = @"from sys import oar

oar";

            RemoveSmartTagTest(app, "FromImport1.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "FromImport1.py", 1, 1, false, expectedText);
        }

        public void FromImport2(VisualStudioApp app) {
            string expectedText = @"from sys import baz

baz";

            RemoveSmartTagTest(app, "FromImport2.py", 1, 1, true, expectedText);
        }

        public void FromImportParens1(VisualStudioApp app) {
            string expectedText = @"from sys import (oar)

oar";

            RemoveSmartTagTest(app, "FromImportParens1.py", 1, 1, true, expectedText);
        }

        public void FromImportParens2(VisualStudioApp app) {
            string expectedText = @"from sys import (baz)

baz";

            RemoveSmartTagTest(app, "FromImportParens2.py", 1, 1, true, expectedText);
        }

        public void FromImportParensTrailingComma1(VisualStudioApp app) {
            string expectedText = @"from sys import (baz, )

baz";

            RemoveSmartTagTest(app, "FromImportParensTrailingComma1.py", 1, 1, true, expectedText);
        }

        public void FromImportParensTrailingComma2(VisualStudioApp app) {
            string expectedText = @"from sys import (oar, )

oar";

            RemoveSmartTagTest(app, "FromImportParensTrailingComma2.py", 1, 1, true, expectedText);
        }

        public void Import1(VisualStudioApp app) {
            string expectedText = @"import oar

oar";

            RemoveSmartTagTest(app, "Import1.py", 1, 1, true, expectedText);
        }

        public void Import2(VisualStudioApp app) {
            string expectedText = @"import baz

baz";

            RemoveSmartTagTest(app, "Import2.py", 1, 1, true, expectedText);
        }

        public void Import3(VisualStudioApp app) {
            string expectedText = @"import baz

baz";

            RemoveSmartTagTest(app, "Import3.py", 1, 1, true, expectedText);
        }

        public void Import4(VisualStudioApp app) {
            string expectedText = @"import oar, quox

oar
quox";

            RemoveSmartTagTest(app, "Import4.py", 1, 1, true, expectedText);
        }

        public void Import5(VisualStudioApp app) {
            string expectedText = @"import oar, quox

oar
quox";

            RemoveSmartTagTest(app, "Import5.py", 1, 1, true, expectedText);
        }

        public void Import6(VisualStudioApp app) {
            string expectedText = @"import oar,          quox

oar
quox";

            RemoveSmartTagTest(app, "Import6.py", 1, 1, true, expectedText);
        }

        public void ImportComment(VisualStudioApp app) {
            string expectedText = @"#baz
import oar,          quox
#fob
#oar

oar
quox";

            RemoveSmartTagTest(app, "ImportComment.py", 1, 1, true, expectedText);
        }

        public void FromImportComment(VisualStudioApp app) {
            string expectedText = @"#baz
from xyz import oar,          quox
#fob
#oar

oar
quox";

            RemoveSmartTagTest(app, "FromImportComment.py", 1, 1, true, expectedText);
        }

        public void ImportDup(VisualStudioApp app) {
            string expectedText = @"";

            RemoveSmartTagTest(app, "ImportDup.py", 1, 1, true, expectedText);
        }

        public void FromImportDup(VisualStudioApp app) {
            string expectedText = @"";

            RemoveSmartTagTest(app, "FromImportDup.py", 1, 1, true, expectedText);
        }

        public void Import(VisualStudioApp app) {
            string expectedText = @"";

            RemoveSmartTagTest(app, "Import.py", 1, 1, true, expectedText);
        }

        public void FromImport(VisualStudioApp app) {
            string expectedText = @"";

            RemoveSmartTagTest(app, "FromImport.py", 1, 1, true, expectedText);
        }

        public void FutureImport(VisualStudioApp app) {
            string expectedText = @"from __future__ import with_statement";

            RemoveSmartTagTest(app, "FutureImport.py", 1, 1, true, expectedText);
        }

        public void LocalScopeDontRemoveGlobal(VisualStudioApp app) {
            string expectedText = @"import dne

def f():
    import baz

    baz";

            RemoveSmartTagTest(app, "LocalScopeDontRemoveGlobal.py", 4, 10, false, expectedText);
        }

        public void LocalScopeOnly(VisualStudioApp app) {
            string expectedText = @"import dne

def f():

    oar";

            RemoveSmartTagTest(app, "LocalScopeOnly.py", 4, 10, false, expectedText);
        }

        public void ImportTrailingWhitespace(VisualStudioApp app) {
            string expectedText = @"fob";

            RemoveSmartTagTest(app, "ImportTrailingWhitespace.py", 1, 1, true, expectedText);
        }

        public void ClosureReference(VisualStudioApp app) {
            string expectedText = @"def f():
    import something
    def g():
        something";

            RemoveSmartTagTest(app, "ClosureReference.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "ClosureReference.py", 2, 14, false, expectedText);
        }

        public void NameMangledUnmangled(VisualStudioApp app) {
            string expectedText = @"class C:
    def f(self):
        import __fob
        x = _C__fob";

            RemoveSmartTagTest(app, "NameMangleUnmangled.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "NameMangleUnmangled.py", 3, 14, false, expectedText);
        }

        public void NameMangledMangled(VisualStudioApp app) {
            string expectedText = @"class C:
    def f(self):
        import __fob
        x = __fob";

            RemoveSmartTagTest(app, "NameMangleMangled.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "NameMangleMangled.py", 3, 14, false, expectedText);
        }

        public void EmptyFuncDef1(VisualStudioApp app) {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest(app, "EmptyFuncDef1.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "EmptyFuncDef1.py", 2, 7, false, expectedText);
        }

        public void EmptyFuncDef2(VisualStudioApp app) {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest(app, "EmptyFuncDef2.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "EmptyFuncDef2.py", 2, 7, false, expectedText);
        }

        public void EmptyFuncDefWhitespace(VisualStudioApp app) {
            string expectedText = @"def f():
    pass";

            RemoveSmartTagTest(app, "EmptyFuncDefWhitespace.py", 1, 1, true, expectedText);
            RemoveSmartTagTest(app, "EmptyFuncDefWhitespace.py", 2, 7, false, expectedText);
        }

        public void ImportStar(VisualStudioApp app) {
            string expectedText = @"from sys import *";

            RemoveSmartTagTest(app, "ImportStar.py", 1, 1, true, expectedText);
        }

        private static void RemoveSmartTagTest(VisualStudioApp app, string filename, int line, int column, bool allScopes, string expectedText) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\RemoveImport.sln"));
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);

            doc.Invoke(() => {
                var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line - 1).Start.Add(column - 1);
                doc.TextView.Caret.MoveTo(point);
                doc.WaitForAnalyzerAtCaret();
            });

            if (allScopes) {
                app.ExecuteCommand("EditorContextMenus.CodeWindow.RemoveImports.AllScopes");
            } else {
                app.ExecuteCommand("EditorContextMenus.CodeWindow.RemoveImports.CurrentScope");
            }

            doc.WaitForText(expectedText);
        }
    }
}
