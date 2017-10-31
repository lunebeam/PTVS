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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Ipc;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    class ClassifierWalker : PythonWalker {
        class StackData {
            public readonly string Name;
            public readonly HashSet<string> Parameters;
            public readonly HashSet<string> Functions;
            public readonly HashSet<string> Types;
            public readonly HashSet<string> Modules;
            public readonly List<Tuple<string, Span>> Names;
            public readonly StackData Previous;

            public StackData(string name, StackData previous) {
                Name = name;
                Previous = previous;
                Parameters = new HashSet<string>();
                Functions = new HashSet<string>();
                Types = new HashSet<string>();
                Modules = new HashSet<string>();
                Names = new List<Tuple<string, Span>>();
            }

            public IEnumerable<StackData> EnumerateTowardsGlobal {
                get {
                    for (var sd = this; sd != null; sd = sd.Previous) {
                        yield return sd;
                    }
                }
            }
        }

        private readonly PythonAst _ast;
        private readonly ModuleAnalysis _analysis;
        private StackData _head;
        public readonly List<AP.AnalysisClassification> Spans;

        public static class Classifications {
            public const string Keyword = "keyword";
            public const string Class = "class";
            public const string Function = "function";
            public const string Module = "module";
            public const string Parameter = "parameter";
            public const string RegexLiteral = "regexliteral";
            public const string DocString = "docstring";
        }

        public ClassifierWalker(PythonAst ast, ModuleAnalysis analysis) {
            _ast = ast;
            _analysis = analysis;
            Spans = new List<AP.AnalysisClassification>();
        }

        private void AddSpan(Tuple<string, Span> node, string type) {
            Spans.Add(
                new AP.AnalysisClassification() {
                    start = node.Item2.Start,
                    length = node.Item2.Length,
                    type = type
                }
            );
        }

        private void BeginScope(string name = null) {
            if (_head != null) {
                if (name == null) {
                    name = _head.Name;
                } else if (_head.Name != null) {
                    name = _head.Name + "." + name;
                }
            }
            _head = new StackData(name, _head);
        }

        private void AddParameter(Parameter node) {
            Debug.Assert(_head != null);
            _head.Parameters.Add(node.Name);
            _head.Names.Add(Tuple.Create(node.Name, new Span(node.StartIndex, node.Name.Length)));
        }

        private void AddParameter(Node node) {
            NameExpression name;
            TupleExpression tuple;
            Debug.Assert(_head != null);
            if ((name = node as NameExpression) != null) {
                _head.Parameters.Add(name.Name);
            } else if ((tuple = node as TupleExpression) != null) {
                foreach (var expr in tuple.Items) {
                    AddParameter(expr);
                }
            } else {
                Trace.TraceWarning("Unable to find parameter in {0}", node);
            }
        }

        public override bool Walk(NameExpression node) {
            _head.Names.Add(Tuple.Create(node.Name, Span.FromBounds(node.StartIndex, node.EndIndex)));
            return base.Walk(node);
        }

        private static string GetFullName(MemberExpression expr) {
            var ne = expr.Target as NameExpression;
            if (ne != null) {
                return ne.Name + "." + expr.Name ?? string.Empty;
            }
            var me = expr.Target as MemberExpression;
            if (me != null) {
                var baseName = GetFullName(me);
                if (baseName == null) {
                    return null;
                }
                return baseName + "." + expr.Name ?? string.Empty;
            }
            return null;
        }

        public override bool Walk(MemberExpression node) {
            var fullname = GetFullName(node);
            if (fullname != null) {
                _head.Names.Add(Tuple.Create(fullname, Span.FromBounds(node.NameHeader, node.EndIndex)));
            }
            return base.Walk(node);
        }

        public override bool Walk(DottedName node) {
            string totalName = "";
            foreach (var name in node.Names) {
                _head.Names.Add(Tuple.Create(totalName + name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                totalName += name.Name + ".";
            }
            return base.Walk(node);
        }

        private string ClassifyName(Tuple<string, Span> node) {
            var name = node.Item1;
            foreach (var sd in _head.EnumerateTowardsGlobal) {
                if (sd.Parameters.Contains(name)) {
                    return Classifications.Parameter;
                } else if (sd.Functions.Contains(name)) {
                    return Classifications.Function;
                } else if (sd.Types.Contains(name)) {
                    return Classifications.Class;
                } else if (sd.Modules.Contains(name)) {
                    return Classifications.Module;
                }
            }

            if (_analysis != null) {
                var memberType = PythonMemberType.Unknown;
                lock (_analysis) {
                    memberType = _analysis
                        .GetValuesByIndex(name, node.Item2.Start)
                        .Select(v => v.MemberType)
                        .DefaultIfEmpty(PythonMemberType.Unknown)
                        .Aggregate((a, b) => a == b ? a : PythonMemberType.Unknown);
                }

                if (memberType == PythonMemberType.Module) {
                    return Classifications.Module;
                } else if (memberType == PythonMemberType.Class) {
                    return Classifications.Class;
                } else if (memberType == PythonMemberType.Function || memberType == PythonMemberType.Method) {
                    return Classifications.Function;
                }
            }

            return null;
        }

        private void EndScope(bool mergeNames) {
            var sd = _head;
            foreach (var node in sd.Names) {
                var classificationName = ClassifyName(node);
                if (classificationName != null) {
                    AddSpan(node, classificationName);
                    if (mergeNames && sd.Previous != null) {
                        if (classificationName == Classifications.Module) {
                            sd.Previous.Modules.Add(sd.Name + "." + node.Item1);
                        } else if (classificationName == Classifications.Class) {
                            sd.Previous.Types.Add(sd.Name + "." + node.Item1);
                        } else if (classificationName == Classifications.Function) {
                            sd.Previous.Functions.Add(sd.Name + "." + node.Item1);
                        }
                    }
                }
            }
            _head = sd.Previous;
        }

        public override bool Walk(PythonAst node) {
            Debug.Assert(_head == null);
            _head = new StackData(string.Empty, null);
            return base.Walk(node);
        }

        public override void PostWalk(PythonAst node) {
            EndScope(false);
            Debug.Assert(_head == null);
            base.PostWalk(node);
        }

        private void MaybeAddDocstring(Node body) {
            var docString = (body as SuiteStatement)?.Statements?[0] as ExpressionStatement;
            if (docString?.Expression is ConstantExpression ce && (ce.Value is string || ce.Value is AsciiString)) {
                AddSpan(Tuple.Create("", Span.FromBounds(ce.StartIndex, ce.EndIndex)), Classifications.DocString);
            }
        }

        public override bool Walk(ClassDefinition node) {
            Debug.Assert(_head != null);
            _head.Types.Add(node.NameExpression.Name);
            node.NameExpression.Walk(this);
            BeginScope(node.NameExpression.Name);
            MaybeAddDocstring(node.Body);
            return base.Walk(node);
        }

        public override bool Walk(FunctionDefinition node) {
            if (node.IsCoroutine) {
                AddSpan(Tuple.Create("", new Span(node.DefIndex, 5)), Classifications.Keyword);
            }

            Debug.Assert(_head != null);
            _head.Functions.Add(node.NameExpression.Name);
            node.NameExpression.Walk(this);
            BeginScope();
            MaybeAddDocstring(node.Body);
            return base.Walk(node);
        }

        public override bool Walk(DictionaryComprehension node) {
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(ListComprehension node) {
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(GeneratorExpression node) {
            BeginScope();
            return base.Walk(node);
        }

        public override bool Walk(ComprehensionFor node) {
            AddParameter(node.Left);

            if (node.IsAsync) {
                AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), Classifications.Keyword);
            }

            return base.Walk(node);
        }

        public override bool Walk(Parameter node) {
            AddParameter(node);
            return base.Walk(node);
        }

        public override bool Walk(ImportStatement node) {
            Debug.Assert(_head != null);
            if (node.AsNames != null) {
                foreach (var name in node.AsNames) {
                    if (name != null && !string.IsNullOrEmpty(name.Name)) {
                        _head.Modules.Add(name.Name);
                        _head.Names.Add(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                    }
                }
            }
            if (node.Names != null) {
                for (int i = 0; i < node.Names.Count; ++i) {
                    var dottedName = node.Names[i];
                    var hasAsName = (node.AsNames != null && node.AsNames.Count > i) ? node.AsNames[i] != null : false;
                    foreach (var name in dottedName.Names) {
                        if (name != null && !string.IsNullOrEmpty(name.Name)) {
                            if (!hasAsName) {
                                _head.Modules.Add(name.Name);
                                _head.Names.Add(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                            } else {
                                // Only want to highlight this instance of the
                                // name, since it isn't going to be bound in the
                                // rest of the module.
                                AddSpan(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)), Classifications.Module);
                            }
                        }
                    }
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            Debug.Assert(_head != null);
            if (node.Root != null) {
                foreach (var name in node.Root.Names) {
                    if (name != null && !string.IsNullOrEmpty(name.Name)) {
                        AddSpan(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)), Classifications.Module);
                    }
                }
            }
            if (node.Names != null) {
                foreach (var name in node.Names) {
                    if (name != null && !string.IsNullOrEmpty(name.Name)) {
                        _head.Names.Add(Tuple.Create(name.Name, Span.FromBounds(name.StartIndex, name.EndIndex)));
                    }
                }
            }
            return base.Walk(node);
        }



        public override void PostWalk(ClassDefinition node) {
            EndScope(true);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(FunctionDefinition node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(DictionaryComprehension node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(ListComprehension node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }

        public override void PostWalk(GeneratorExpression node) {
            EndScope(false);
            Debug.Assert(_head != null);
            base.PostWalk(node);
        }


        public override bool Walk(AwaitExpression node) {
            AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), Classifications.Keyword);
            return base.Walk(node);
        }

        public override bool Walk(ForStatement node) {
            if (node.IsAsync) {
                AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), Classifications.Keyword);
            }
            return base.Walk(node);
        }

        public override bool Walk(WithStatement node) {
            if (node.IsAsync) {
                AddSpan(Tuple.Create("", new Span(node.StartIndex, 5)), Classifications.Keyword);
            }
            return base.Walk(node);
        }

        private static readonly HashSet<string> RegexFunctionNames = new HashSet<string> {
            "compile",
            "escape",
            "findall",
            "finditer",
            "fullmatch",
            "match",
            "search",
            "split",
            "sub",
            "subn"
        };

        public override bool Walk(CallExpression node) {
            bool isRegex = false;

            if (node.Target is MemberExpression me && RegexFunctionNames.Contains(me.Name) && me.Target is NameExpression target) {
                if (_analysis.GetValues(target.Name, me.GetStart(_ast)).Any(m => m is IModule && m.Name == "re")) {
                    isRegex = true;
                }
            } else if (node.Target is NameExpression ne && RegexFunctionNames.Contains(ne.Name)) {
                if (_analysis.GetValues(ne.Name, ne.GetStart(_ast)).OfType<BuiltinFunctionInfo>()
                    .Any(f => f.Function?.DeclaringType == null && f.Function?.DeclaringModule.Name == "re")) {
                    isRegex = true;
                }
            }

            if (isRegex && node.Args != null && node.Args.Count > 0 && node.Args[0].Expression is ConstantExpression ce) {
                if (ce.Value is string || ce.Value is AsciiString) {
                    AddSpan(Tuple.Create("", Span.FromBounds(ce.StartIndex, ce.EndIndex)), Classifications.RegexLiteral);
                }
            }

            return base.Walk(node);
        }

        public struct Span {
            public readonly int Start, Length;

            public Span(int start, int length) {
                Start = start;
                Length = length;
            }

            public static Span FromBounds(int start, int end) {
                return new Span(start, end - start);
            }
        }

        public struct Classification {
            public readonly Span Span;
            public readonly string Type;

            public Classification(Span span, string type) {
                Span = span;
                Type = type;
            }
        }
    }
}
