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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstBuiltinsPythonModule : AstScrapedPythonModule, IBuiltinPythonModule {
        // protected by lock(_members)
        private readonly HashSet<string> _hiddenNames;

        public AstBuiltinsPythonModule(PythonLanguageVersion version)
            : base(version.Is3x() ? SharedDatabaseState.BuiltinName3x : SharedDatabaseState.BuiltinName2x, null) {
            _hiddenNames = new HashSet<string>();
        }

        public override IMember GetMember(IModuleContext context, string name) {
            lock (_members) {
                if (_hiddenNames.Contains(name)) {
                    return null;
                }
            }
            return base.GetMember(context, name);
        }

        public IMember GetAnyMember(string name) {
            lock (_members) {
                IMember m;
                _members.TryGetValue(name, out m);
                return m;
            }
        }

        public override IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            lock (_members) {
                return base.GetMemberNames(moduleContext).Except(_hiddenNames).ToArray();
            }
        }

        protected override Stream LoadCachedCode(AstPythonInterpreter interpreter) {
            var fact = interpreter.Factory as AstPythonInterpreterFactory;
            if (fact?.Configuration.InterpreterPath == null) {
                return null;
            }
            return fact.ReadCachedModule(fact.Configuration.InterpreterPath);
        }

        protected override void SaveCachedCode(AstPythonInterpreter interpreter, Stream code) {
            var fact = interpreter.Factory as AstPythonInterpreterFactory;
            if (fact?.Configuration.InterpreterPath == null) {
                return;
            }
            fact.WriteCachedModule(fact.Configuration.InterpreterPath, code);
        }

        protected override List<string> GetScrapeArguments(IPythonInterpreterFactory factory) {
            var args = new List<string> { "-B", "-E" };

            var sb = PythonToolsInstallPath.TryGetFile("scrape_module.py", GetType().Assembly);
            if (!File.Exists(sb)) {
                return null;
            }
            args.Add(sb);

            return args;
        }

        protected override PythonWalker PrepareWalker(IPythonInterpreter interpreter, PythonAst ast) {
            var walker = new AstAnalysisWalker(interpreter, ast, this, null, _members, false, true);
            walker.CreateBuiltinTypes = true;
            walker.Scope.SuppressBuiltinLookup = true;
            return walker;
        }

        protected override void PostWalk(PythonWalker walker) {
            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                IMember m;
                AstPythonBuiltinType biType;
                if (_members.TryGetValue($"__{typeId}", out m) && (biType = m as AstPythonBuiltinType) != null) {
                    if (typeId != BuiltinTypeId.Str &&
                        typeId != BuiltinTypeId.StrIterator) {
                        biType.TrySetTypeId(typeId);
                    }

                    if (biType.IsHidden) {
                        _hiddenNames.Add(biType.Name);
                    }
                    _hiddenNames.Add($"__{typeId}");
                }
            }
            _hiddenNames.Add("__builtin_module_names");
        }

    }
}
