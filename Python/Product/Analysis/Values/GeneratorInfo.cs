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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a generator instance - either constructed using a generator expression or
    /// by calling a function definition which contains yield expressions.
    /// </summary>
    internal class GeneratorInfo : BuiltinInstanceInfo, IHasRichDescription {
        private AnalysisValue _nextMethod;
        private AnalysisValue _sendMethod;
        private readonly IPythonProjectEntry _declaringModule;
        private readonly int _declaringVersion;
        public readonly VariableDef Yields;
        public readonly VariableDef Sends;
        public readonly VariableDef Returns;

        public GeneratorInfo(PythonAnalyzer projectState, IPythonProjectEntry entry)
            : base(projectState.ClassInfos[BuiltinTypeId.Generator]) {
            
            _declaringModule = entry;
            _declaringVersion = entry.AnalysisVersion;
            Yields = new VariableDef();
            Sends = new VariableDef();
            Returns = new VariableDef();
        }

        public override IPythonProjectEntry DeclaringModule { get { return _declaringModule; } }
        public override int DeclaringVersion { get { return _declaringVersion; } }

        public override string Description => string.Join("", GetRichDescription().Select(kv => kv.Value));
        public override string ShortDescription => string.Join("", GetRichDescription().TakeWhile(kv => kv.Key != WellKnownRichDescriptionKinds.EndOfDeclaration).Select(kv => kv.Value));

        private IAnalysisSet GeneratorNext(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return GetEnumeratorTypes(node, unit);
        }

        private IAnalysisSet GeneratorSend(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length > 0) {
                AddSend(node, unit, args[0]);
            }
            return GetEnumeratorTypes(node, unit);
        }


        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);

            switch(name) {
                case "next":
                    if (unit.ProjectState.LanguageVersion.Is2x()) {
                        return _nextMethod = _nextMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            GeneratorNext,
                            false
                        );
                    }
                    break;
                case "__next__":
                    if (unit.ProjectState.LanguageVersion.Is3x()) {
                        return _nextMethod = _nextMethod ?? new SpecializedCallable(
                            res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                            GeneratorNext,
                            false
                        );
                    }
                    break;
                case "send":
                    return _sendMethod = _sendMethod ?? new SpecializedCallable(
                        res.OfType<BuiltinNamespace<IPythonType>>().FirstOrDefault(),
                        GeneratorSend,
                        false
                    );
            }

            return res;
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return SelfSet;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            Yields.AddDependency(unit);

            return Yields.Types;
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            base.AddReference(node, analysisUnit);
        }

        public void AddYield(Node node, AnalysisUnit unit, IAnalysisSet yieldValue, bool enqueue = true) {
            Yields.MakeUnionStrongerIfMoreThan(ProjectState.Limits.YieldTypes, yieldValue);
            Yields.AddTypes(unit, yieldValue, enqueue, DeclaringModule);
        }

        public void AddReturn(Node node, AnalysisUnit unit, IAnalysisSet returnValue, bool enqueue = true) {
            Returns.MakeUnionStrongerIfMoreThan(ProjectState.Limits.ReturnTypes, returnValue);
            Returns.AddTypes(unit, returnValue, enqueue, DeclaringModule);
        }

        public void AddSend(Node node, AnalysisUnit unit, IAnalysisSet sendValue, bool enqueue = true) {
            Sends.AddTypes(unit, sendValue, enqueue, DeclaringModule);
        }

        public void AddYieldFrom(Node node, AnalysisUnit unit, IAnalysisSet yieldsFrom, bool enqueue = true) {
            foreach (var ns in yieldsFrom) {
                AddYield(node, unit, ns.GetEnumeratorTypes(node, unit), enqueue);
                var gen = ns as GeneratorInfo;
                if (gen != null) {
                    gen.AddSend(node, unit, Sends.Types, enqueue);
                    AddReturn(node, unit, gen.Returns.Types, enqueue);
                }
            }
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (ns is GeneratorInfo other) {
                return Yields.Types.SetEquals(other.Yields.Types);
            }
            return false;
        }

        internal override int UnionHashCode(int strength) {
            // Arbitrarily selected prime number
            return 968897;
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (ns is GeneratorInfo other) {
                other.Yields.CopyTo(Yields);
                other.Sends.CopyTo(Sends);
                other.Returns.CopyTo(Returns);
            }
            return this;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            var desc = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, "generator")
            };

            bool needClosingBracket = false;

            var yieldTypes = Yields.Types.GetRichDescriptions(unionPrefix: "{", unionSuffix: "}").ToList();
            if (yieldTypes.Any()) {
                needClosingBracket = true;
                desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "["));
                desc.AddRange(yieldTypes);
            }

            var sendTypes = Sends.Types.GetRichDescriptions(unionPrefix: "{", unionSuffix: "}").ToList();
            if (sendTypes.Any()) {
                if (!needClosingBracket) {
                    needClosingBracket = true;
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "["));
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, "..."));
                }
                desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                desc.AddRange(sendTypes);
            }

            var resTypes = Returns.Types.GetRichDescriptions(unionPrefix: "{", unionSuffix: "}").ToList();
            if (resTypes.Any()) {
                if (!needClosingBracket) {
                    needClosingBracket = true;
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "["));
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, "..."));
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, "..."));
                } else if (!sendTypes.Any()) {
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                    desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, "..."));
                }
                desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", "));
                desc.AddRange(resTypes);
            }

            if (needClosingBracket) {
                desc.Add(new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]"));
            }

            return desc;
        }
    }
}
