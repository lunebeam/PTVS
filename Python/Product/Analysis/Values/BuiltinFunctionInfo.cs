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
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinFunctionInfo : BuiltinNamespace<IPythonType>, IHasRichDescription {
        private IPythonFunction _function;
        private string _doc;
        private ReadOnlyCollection<OverloadResult> _overloads;
        private readonly Lazy<IAnalysisSet> _returnTypes;
        private BuiltinMethodInfo _method;

        public BuiltinFunctionInfo(IPythonFunction function, PythonAnalyzer projectState)
            : base(projectState.Types[BuiltinTypeId.BuiltinFunction], projectState) {

            _function = function;
            _returnTypes = new Lazy<IAnalysisSet>(() => Utils.GetReturnTypes(function, projectState).GetInstanceType());
        }

        public override IPythonType PythonType {
            get { return _type; }
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            return klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Function]) ||
                klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.BuiltinFunction]);
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return _returnTypes.Value;
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (_function.IsClassMethod) {
                instance = context;
            }

            if (_function.IsStatic || instance.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.NoneType])) {
                return base.GetDescriptor(node, instance, context, unit);
            } else if (_method == null) {
                _method = new BuiltinMethodInfo(_function, PythonMemberType.Method, ProjectState);
            }

            return _method.GetDescriptor(node, instance, context, unit);
        }

        public IPythonFunction Function {
            get {
                return _function;
            }
        }

        public override string Name {
            get {
                return _function.Name;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            var def = _function.IsBuiltin ? "built-in function " : "function ";
            return GetRichDescription(def, _function, Documentation);
        }

        internal static IEnumerable<KeyValuePair<string, string>> GetRichDescription(string def, IPythonFunction function, string doc) {
            bool needNewline = false;
            foreach (var overload in function.Overloads.OrderByDescending(o => o.GetParameters().Length)) {
                if (needNewline) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "\r\n");
                }
                needNewline = true;

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, def);

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, GetFullName(function.DeclaringType, function.Name));
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "(");
                foreach (var kv in GetParameterString(overload)) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")");
            }
            if (!string.IsNullOrEmpty(doc)) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.EndOfDeclaration, needNewline ? "\r\n" : "");
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, doc);
            }
        }

        private static string GetFullName(IPythonType type, string name) {
            if (type == null) {
                return name;
            }
            name = type.Name + "." + name;
            if (type.IsBuiltin || type.DeclaringModule == null) {
                return name;
            }
            return type.DeclaringModule.Name + "." + name;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetParameterString(IPythonFunctionOverload overload) {
            var parameters = overload.GetParameters();
            for (int i = 0; i < parameters.Length; i++) {
                if (i != 0) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                var p = parameters[i];

                var name = p.Name;
                if (p.IsKeywordDict) {
                    name = "**" + name;
                } else if (p.IsParamArray) {
                    name = "*" + name;
                }

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Parameter, name);

                if (!string.IsNullOrWhiteSpace(p.DefaultValue)) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " = ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, p.DefaultValue);
                }
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_overloads == null) {
                    var overloads = _function.Overloads;
                    var result = new OverloadResult[overloads.Count];
                    for (int i = 0; i < result.Length; i++) {
                        result[i] = new BuiltinFunctionOverloadResult(ProjectState, _function.Name, overloads[i], 0, () => Description);
                    }
                    _overloads = new ReadOnlyCollection<OverloadResult>(result);
                }
                return _overloads;
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    _doc = Utils.StripDocumentation(_function.Documentation);
                }
                return _doc;
            }
        }

        public override PythonMemberType MemberType {
            get {
                return _function.MemberType;
            }
        }

        public override ILocatedMember GetLocatedMember() {
            return _function as ILocatedMember;
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ns is BuiltinFunctionInfo || ns is FunctionInfo || ns == ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance.UnionHashCode(strength);
            }
            return base.UnionHashCode(strength);
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionMergeTypes(ns, strength);
        }
    }
}
