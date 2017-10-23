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

using System.IO;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public sealed class ExpressionFinder {
        public ExpressionFinder(PythonAst ast, GetExpressionOptions options) {
            Ast = ast;
            Options = options.Clone();
        }

        public ExpressionFinder(string expression, PythonLanguageVersion version, GetExpressionOptions options) {
            var parserOpts = new ParserOptions { Verbatim = true };
            using (var parser = Parser.CreateParser(new StringReader(expression), version, parserOpts)) {
                Ast = parser.ParseTopExpression();
                Ast.Body.SetLoc(0, expression.Length);
            }
            Options = options.Clone();
        }

        public PythonAst Ast { get; }
        public GetExpressionOptions Options { get; }

        public Node GetExpression(int index) {
            return GetExpression(index, index);
        }

        public Node GetExpression(SourceLocation location) {
            return GetExpression(new SourceSpan(location, location));
        }

        public SourceSpan? GetExpressionSpan(int index) {
            return GetExpression(index, index)?.GetSpan(Ast);
        }

        public SourceSpan? GetExpressionSpan(SourceLocation location) {
            return GetExpression(new SourceSpan(location, location))?.GetSpan(Ast);
        }

        public Node GetExpression(int startIndex, int endIndex) {
            ExpressionWalker walker;
            if (Options.MemberTarget) {
                walker = new MemberTargetExpressionWalker(Ast, startIndex);
            } else {
                walker = new NormalExpressionWalker(Ast, startIndex, endIndex, Options);
            }
            Ast.Walk(walker);
            return walker.Expression;
        }

        public Node GetExpression(SourceSpan range) {
            int startIndex = Ast.LocationToIndex(range.Start);
            int endIndex = Ast.LocationToIndex(range.End);
            return GetExpression(startIndex, endIndex);
        }

        public SourceSpan? GetExpressionSpan(int startIndex, int endIndex) {
            return GetExpression(startIndex, endIndex)?.GetSpan(Ast);
        }

        public SourceSpan? GetExpressionSpan(SourceSpan range) {
            return GetExpression(range)?.GetSpan(Ast);
        }

        private abstract class ExpressionWalker : PythonWalkerWithLocation {
            public ExpressionWalker(int location) : base(location) { }
            public Node Expression { get; protected set; }
        }

        private class NormalExpressionWalker : ExpressionWalker {
            private readonly int _endLocation;
            private readonly PythonAst _ast;
            private readonly GetExpressionOptions _options;

            private bool _walkingClassBases;

            public NormalExpressionWalker(PythonAst ast, int location, int endLocation, GetExpressionOptions options) : base(location) {
                _ast = ast;
                _endLocation = endLocation;
                _options = options;
            }

            private bool Save(Node node, bool baseWalk, bool ifTrue) {
                if (baseWalk && !(node.StartIndex <= _endLocation && _endLocation <= node.EndIndex)) {
                    return false;
                }

                if (baseWalk && ifTrue) {
                    Expression = node;
                }
                return baseWalk;
            }

            private bool BeforeBody(Node body) {
                if (Location >= body.StartIndex) {
                    return false;
                }

                var ws = body.GetLeadingWhiteSpace(_ast);
                if (string.IsNullOrEmpty(ws)) {
                    return false;
                }

                if (Location >= body.StartIndex - ws.Length) {
                    return false;
                }

                return true;
            }

            public override bool Walk(CallExpression node) => Save(node, base.Walk(node), _options.Calls);
            public override bool Walk(ConstantExpression node) => Save(node, base.Walk(node), _options.Literals);
            public override bool Walk(IndexExpression node) => Save(node, base.Walk(node), _options.Indexing);
            public override bool Walk(NameExpression node) => Save(node, base.Walk(node), _options.Names);
            public override bool Walk(Parameter node) => Save(node, base.Walk(node), _options.ParameterNames && Location <= node.StartIndex + node.Name.Length);
            public override bool Walk(ParenthesisExpression node) => Save(node, base.Walk(node), _options.ParenthesisedExpression);
            public override bool Walk(FunctionDefinition node) => Save(node, base.Walk(node), _options.FunctionDefinition && BeforeBody(node.Body));

            public override bool Walk(MemberExpression node) {
                if (Save(node, base.Walk(node), _options.Members && Location >= node.NameHeader)) {
                    if (_options.MemberName && Location >= node.NameHeader) {
                        var nameNode = new NameExpression(node.Name);
                        nameNode.SetLoc(node.NameHeader, node.EndIndex);
                        Expression = nameNode;
                    }
                    return true;
                }
                return false;
            }

            public override bool Walk(ClassDefinition node) {
                if (!base.Walk(node)) {
                    return false;
                }

                if (_options.ClassDefinition && BeforeBody(node.Body)) {
                    Expression = node;
                }

                node.NameExpression?.Walk(this);
                node.Decorators?.Walk(this);
                _walkingClassBases = true;
                try {
                    foreach (var b in node.Bases.MaybeEnumerate()) {
                        b.Walk(this);
                    }
                } finally {
                    _walkingClassBases = false;
                }
                node.Body?.Walk(this);

                return true;
            }

            public override bool Walk(Arg node) {
                if (!base.Walk(node)) {
                    return false;
                }

                if (_walkingClassBases) {
                    node.Expression?.Walk(this);
                    return true;
                }

                var n = node.NameExpression;
                if (_options.NamedArgumentNames && n != null && Location >= n.StartIndex && Location <= n.EndIndex) {
                    Expression = n;
                }

                return true;
            }

            public override bool Walk(DottedName node) {
                if (base.Walk(node)) {
                    string totalName = null;
                    foreach (var n in node.Names.MaybeEnumerate()) {
                        if (n?.Name == null) {
                            break;
                        }
                        if (Location >= n.StartIndex && Location <= n.EndIndex) {
                            if (totalName == null) {
                                Expression = n;
                            } else {
                                Expression = new NameExpression(totalName + n.Name);
                                Expression.SetLoc(node.StartIndex, n.EndIndex);
                            }
                            break;
                        }
                        totalName = (totalName ?? "") + n.Name + ".";
                    }
                }
                return false;
            }

            public override bool Walk(ImportStatement node) {
                if (!base.Walk(node)) {
                    return false;
                }

                if (_options.ImportNames) {
                    foreach (var n in node.Names.MaybeEnumerate()) {
                        n?.Walk(this);
                    }
                }
                if (_options.ImportAsNames) {
                    foreach (var n in node.AsNames.MaybeEnumerate()) {
                        n?.Walk(this);
                    }
                }

                return true;
            }

            public override bool Walk(FromImportStatement node) {
                if (!base.Walk(node)) {
                    return false;
                }

                if (_options.ImportNames) {
                    node.Root?.Walk(this);
                }

                foreach (var n in node.Names.MaybeEnumerate()) {
                    n?.Walk(this);
                }
                foreach (var n in node.AsNames.MaybeEnumerate()) {
                    n?.Walk(this);
                }

                return true;
            }
        }

        private class MemberTargetExpressionWalker : ExpressionWalker {
            private readonly PythonAst _ast;

            public MemberTargetExpressionWalker(PythonAst ast, int location) : base(location) {
                _ast = ast;
            }

            public override bool Walk(MemberExpression node) {
                if (base.Walk(node)) {
                    if (Location >= node.NameHeader && Location <= node.EndIndex) {
                        Expression = node.Target;
                    }
                    return true;
                }
                return false;
            }
        }
    }

    public sealed class GetExpressionOptions {
        public static GetExpressionOptions Hover => new GetExpressionOptions {
            Calls = true,
            Indexing = true,
            Names = true,
            Members = true,
            ParameterNames = true,
            ParenthesisedExpression = true,
            Literals = true,
            ImportNames = true,
            ImportAsNames = true,
        };
        public static GetExpressionOptions Evaluate => new GetExpressionOptions {
            Calls = true,
            Indexing = true,
            Names = true,
            Members = true,
            ParameterNames = true,
            Literals = true,
            ParenthesisedExpression = true,
            ClassDefinition = true,
            FunctionDefinition = true,
            ImportNames = true,
            ImportAsNames = true,
        };
        public static GetExpressionOptions EvaluateMembers => new GetExpressionOptions {
            Members = true,
            MemberTarget = true,
        };
        public static GetExpressionOptions FindDefinition => new GetExpressionOptions {
            Names = true,
            Members = true,
            ParameterNames = true,
            NamedArgumentNames = true,
            ImportNames = true,
            ImportAsNames = true,
        };
        public static GetExpressionOptions Rename => new GetExpressionOptions {
            Names = true,
            MemberName = true,
            NamedArgumentNames = true,
            ParameterNames = true,
            ImportNames = true,
            ImportAsNames = true,
        };

        public bool Calls { get; set; } = false;
        public bool Indexing { get; set; } = false;
        public bool Names { get; set; } = false;
        public bool Members { get; set; } = false;
        public bool MemberTarget { get; set; } = false;
        public bool MemberName { get; set; } = false;
        public bool Literals { get; set; } = false;
        public bool ParenthesisedExpression { get; set; } = false;
        public bool NamedArgumentNames { get; set; } = false;
        public bool ParameterNames { get; set; } = false;
        public bool ClassDefinition { get; set; } = false;
        public bool FunctionDefinition { get; set; } = false;
        public bool ImportNames { get; set; } = false;
        public bool ImportAsNames { get; set; } = false;

        public GetExpressionOptions Clone() {
            return (GetExpressionOptions)MemberwiseClone();
        }
    }
}
