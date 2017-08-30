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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class NameLookupContext {
        private readonly Stack<Dictionary<string, IMember>> _scopes;
        private readonly Lazy<IPythonModule> _builtinModule;

        public NameLookupContext(
            IPythonInterpreter interpreter,
            IModuleContext context,
            PythonAst ast,
            string filePath,
            bool includeLocationInfo
        ) {
            Interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            Context = context;
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));
            FilePath = filePath;
            IncludeLocationInfo = includeLocationInfo;

            DefaultLookupOptions = LookupOptions.Normal;

            _scopes = new Stack<Dictionary<string, IMember>>();
            _builtinModule = new Lazy<IPythonModule>(ImportBuiltinModule);
        }

        public IPythonInterpreter Interpreter { get; }
        public IModuleContext Context { get; }
        public PythonAst Ast { get; }
        public string FilePath { get; }
        public bool IncludeLocationInfo { get; }

        public LookupOptions DefaultLookupOptions { get; set; }
        public bool SuppressBuiltinLookup { get; set; }

        public NameLookupContext Clone(bool copyScopeContents = false) {
            var ctxt = new NameLookupContext(
                Interpreter,
                Context,
                Ast,
                FilePath,
                IncludeLocationInfo
            );

            ctxt.DefaultLookupOptions = DefaultLookupOptions;
            ctxt.SuppressBuiltinLookup = SuppressBuiltinLookup;

            foreach (var scope in _scopes.Reverse()) {
                if (copyScopeContents) {
                    ctxt._scopes.Push(new Dictionary<string, IMember>(scope));
                } else {
                    ctxt._scopes.Push(scope);
                }
            }

            return ctxt;
        }

        private IPythonModule ImportBuiltinModule() {
            var modname = Ast.LanguageVersion.Is3x() ? SharedDatabaseState.BuiltinName3x : SharedDatabaseState.BuiltinName2x;
            var mod = Interpreter.ImportModule(modname);
            Debug.Assert(mod != null, "Failed to import " + modname);
            mod?.Imported(Context);
            return mod;
        }

        public Dictionary<string, IMember> PushScope(Dictionary<string, IMember> scope = null) {
            scope = scope ?? new Dictionary<string, IMember>();
            _scopes.Push(scope);
            return scope;
        }

        public Dictionary<string, IMember> PopScope() {
            return _scopes.Pop();
        }

        internal LocationInfo GetLoc(Node node) {
            if (!IncludeLocationInfo) {
                return null;
            }
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.GetStart(Ast);
            var end = node.GetEnd(Ast);
            return new LocationInfo(FilePath, start.Line, start.Column, end.Line, end.Column);
        }

        internal LocationInfo GetLocOfName(Node node, NameExpression header) {
            var loc = GetLoc(node);
            if (loc == null || header == null) {
                return null;
            }

            var nameStart = header.GetStart(Ast);
            if (!nameStart.IsValid) {
                return loc;
            }

            if (nameStart.Line > loc.StartLine || (nameStart.Line == loc.StartLine && nameStart.Column > loc.StartColumn)) {
                return new LocationInfo(loc.FilePath, nameStart.Line, nameStart.Column, loc.EndLine, loc.EndColumn);
            }

            return loc;
        }

        private string GetNameFromExpressionWorker(Expression expr) {
            if (expr is NameExpression ne) {
                return ne.Name;
            }

            if (expr is MemberExpression me) {
                return "{0}.{1}".FormatInvariant(GetNameFromExpressionWorker(me.Target), me.Name);
            }

            throw new FormatException();
        }

        public string GetNameFromExpression(Expression expr) {
            try {
                return GetNameFromExpressionWorker(expr);
            } catch (FormatException) {
                return null;
            }
        }

        public IMember GetValueFromExpression(Expression expr) {
            return GetValueFromExpression(expr, DefaultLookupOptions);
        }

        public IMember GetValueFromExpression(Expression expr, LookupOptions options) {
            if (expr is NameExpression ne) {
                IMember existing = LookupNameInScopes(ne.Name, options);
                if (existing != null) {
                    return existing;
                }
            }

            if (expr is MemberExpression me && me.Target != null && !string.IsNullOrEmpty(me.Name)) {
                var mc = GetValueFromExpression(me.Target) as IMemberContainer;
                if (mc != null) {
                    return mc.GetMember(Context, me.Name);
                }
                return null;
            }

            IPythonType type;

            if (expr is CallExpression cae) {
                var m = GetValueFromExpression(cae.Target);
                type = m as IPythonType;
                if (type != null) {
                    if (type == Interpreter.GetBuiltinType(BuiltinTypeId.Type) && cae.Args.Count >= 1) {
                        var aType = GetTypeFromValue(GetValueFromExpression(cae.Args[0].Expression));
                        if (aType != null) {
                            return aType;
                        }
                    }
                    return new AstPythonConstant(type, GetLoc(expr));
                }

                if (m is IPythonFunction fn) {
                    // TODO: Select correct overload and handle multiple return types
                    if (fn.Overloads.Count > 0 && fn.Overloads[0].ReturnType.Count > 0) {
                        return new AstPythonConstant(fn.Overloads[0].ReturnType[0]);
                    }
                    return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.NoneType), GetLoc(expr));
                }
            }

            type = GetTypeFromLiteral(expr);
            if (type != null) {
                return new AstPythonConstant(type, GetLoc(expr));
            }

            return null;
        }

        public IPythonType GetTypeFromValue(IMember value) {
            if (value == null) {
                return null;
            }

            var type = (value as IPythonConstant)?.Type;
            if (type != null) {
                return type;
            }

            if (value is IPythonType) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            }

            if (value is IPythonFunction) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            if (value is IPythonModule) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Module);
            }

            Debug.Fail("Unhandled type() value: " + value.GetType().FullName);
            return null;
        }

        public IPythonType GetTypeFromLiteral(Expression expr) {
            if (expr is ConstantExpression ce) {
                if (ce.Value == null) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                }
                switch (Type.GetTypeCode(ce.Value.GetType())) {
                    case TypeCode.Boolean: return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                    case TypeCode.Double: return Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    case TypeCode.Int32: return Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                    case TypeCode.String: return Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
                    case TypeCode.Object:
                        if (ce.Value.GetType() == typeof(Complex)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
                        } else if (ce.Value.GetType() == typeof(AsciiString)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                        } else if (ce.Value.GetType() == typeof(BigInteger)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                        } else if (ce.Value.GetType() == typeof(Ellipsis)) {
                            return Interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
                        }
                        break;
                }
                return null;
            }

            if (expr is ListExpression || expr is ListComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.List);
            }
            if (expr is DictionaryExpression || expr is DictionaryComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            }
            if (expr is TupleExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
            }
            if (expr is SetExpression || expr is SetComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }
            if (expr is LambdaExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            return null;
        }

        public IMember GetInScope(string name) {
            if (_scopes.Count == 0) {
                return null;
            }

            IMember obj;
            if (_scopes.Peek().TryGetValue(name, out obj)) {
                return obj;
            }
            return null;
        }

        public void SetInScope(string name, IMember value) {
            _scopes.Peek()[name] = value;
        }

        [Flags]
        public enum LookupOptions {
            None = 0,
            Local,
            Nonlocal,
            Global,
            Builtins,
            Normal = Local | Nonlocal | Global | Builtins
        }

        public IMember LookupNameInScopes(string name) {
            return LookupNameInScopes(name, DefaultLookupOptions);
        }

        public IMember LookupNameInScopes(string name, LookupOptions options) {
            IMember value;

            var scopes = _scopes.ToList();
            if (scopes.Count == 1) {
                if (!options.HasFlag(LookupOptions.Local) && !options.HasFlag(LookupOptions.Global)) {
                    scopes = null;
                }
            } else if (scopes.Count >= 2) {
                if (!options.HasFlag(LookupOptions.Nonlocal)) {
                    while (scopes.Count > 2) {
                        scopes.RemoveAt(1);
                    }
                }
                if (!options.HasFlag(LookupOptions.Local)) {
                    scopes.RemoveAt(0);
                }
                if (!options.HasFlag(LookupOptions.Global)) {
                    scopes.RemoveAt(scopes.Count - 1);
                }
            }

            if (scopes != null) {
                foreach (var scope in scopes) {
                    if (scope.TryGetValue(name, out value) && value != null) {
                        return value;
                    }
                }
            }

            if (!SuppressBuiltinLookup && options.HasFlag(LookupOptions.Builtins)) {
                return _builtinModule.Value.GetMember(Context, name);
            }

            return null;
        }
    }
}
