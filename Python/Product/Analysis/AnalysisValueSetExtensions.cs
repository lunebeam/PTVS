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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides operations which can be performed in bulk over a set of 
    /// analysis values, which results in a new analysis set.
    /// </summary>
    public static class AnalysisValueSetExtensions {
        /// <summary>
        /// Performs a GetMember operation for the given name and returns the
        /// types of variables which are associated with that name.
        /// </summary>
        public static IAnalysisSet GetMember(this IAnalysisSet self, Node node, AnalysisUnit unit, string name) {
            var res = AnalysisSet.Empty;
            // name can be empty if we have "fob."
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    res = res.Union(ns.GetMember(node, unit, name));
                }
            }
            return res;
        }

        /// <summary>
        /// Performs a SetMember operation for the given name and propagates the
        /// given values types for the provided member name.
        /// </summary>
        public static void SetMember(this IAnalysisSet self, Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    ns.SetMember(node, unit, name, value);
                }
            }
        }

        /// <summary>
        /// Performs a delete index operation propagating the index types into
        /// the provided object.
        /// </summary>
        public static void DeleteMember(this IAnalysisSet self, Node node, AnalysisUnit unit, string name) {
            if (name != null && name.Length > 0) {
                foreach (var ns in self) {
                    ns.DeleteMember(node, unit, name);
                }
            }
        }

        /// <summary>
        /// Performs a call operation propagating the argument types into any
        /// user defined functions or classes and returns the set of types which
        /// result from the call.
        /// </summary>
        public static IAnalysisSet Call(this IAnalysisSet self, Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                var call = ns.Call(node, unit, args, keywordArgNames);
                Debug.Assert(call != null);

                res = res.Union(call);
            }

            return res;
        }

        /// <summary>
        /// Performs a get iterator operation propagating any iterator types
        /// into the value and returns the associated types associated with the
        /// object.
        /// </summary>
        public static IAnalysisSet GetIterator(this IAnalysisSet self, Node node, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetIterator(node, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs a get iterator operation propagating any iterator types
        /// into the value and returns the associated types associated with the
        /// object.
        /// </summary>
        public static IAnalysisSet GetAsyncIterator(this IAnalysisSet self, Node node, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetAsyncIterator(node, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs a get index operation propagating any index types into the
        /// value and returns the associated types associated with the object.
        /// </summary>
        public static IAnalysisSet GetIndex(this IAnalysisSet self, Node node, AnalysisUnit unit, IAnalysisSet index) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetIndex(node, unit, index));
            }

            return res;
        }

        /// <summary>
        /// Performs a set index operation propagating the index types and value
        /// types into the provided object.
        /// </summary>
        public static void SetIndex(this IAnalysisSet self, Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            foreach (var ns in self) {
                ns.SetIndex(node, unit, index, value);
            }
        }

        /// <summary>
        /// Performs a delete index operation propagating the index types into
        /// the provided object.
        /// </summary>
        public static void DeleteIndex(this IAnalysisSet self, Node node, AnalysisUnit unit, IAnalysisSet index) {
        }

        /// <summary>
        /// Performs an augmented assignment propagating the value into the
        /// object.
        /// </summary>
        public static void AugmentAssign(this IAnalysisSet self, AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            foreach (var ns in self) {
                ns.AugmentAssign(node, unit, value);
            }
        }

        /// <summary>
        /// Returns the set of types which are accessible when code enumerates
        /// over the object
        /// in a for statement.
        /// </summary>
        public static IAnalysisSet GetEnumeratorTypes(this IAnalysisSet self, Node node, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetEnumeratorTypes(node, unit));
            }

            return res;
        }
        /// <summary>
        /// Returns the set of types which are accessible when code enumerates
        /// over the object
        /// in a for statement.
        /// </summary>
        public static IAnalysisSet GetAsyncEnumeratorTypes(this IAnalysisSet self, Node node, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetAsyncEnumeratorTypes(node, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs a __get__ on the object.
        /// </summary>
        public static IAnalysisSet GetDescriptor(this IAnalysisSet self, Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.GetDescriptor(node, instance, context, unit));
            }

            return res;
        }

        /// <summary>
        /// Performs the specified operation on the value and the rhs value.
        /// </summary>
        public static IAnalysisSet BinaryOperation(this IAnalysisSet self, Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.BinaryOperation(node, unit, operation, rhs));
            }

            return res;
        }

        /// <summary>
        /// Performs the specified operation on the value.
        /// </summary>
        public static IAnalysisSet UnaryOperation(this IAnalysisSet self, Node node, AnalysisUnit unit, PythonOperator operation) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.UnaryOperation(node, unit, operation));
            }

            return res;
        }

        internal static AnalysisValue GetUnionType(this IAnalysisSet types) {
            var union = AnalysisSet.CreateUnion(types, UnionComparer.Instances[0]);
            AnalysisValue type = null;
            if (union.Count == 2) {
                type = union.FirstOrDefault(t => t.GetConstantValue() != null);
            }
            return type ?? union.FirstOrDefault();
        }

        /// <summary>
        /// Gets instance representations of all members of the set.
        /// </summary>
        public static IAnalysisSet GetInstanceType(this IAnalysisSet types) {
            return AnalysisSet.Create(types.SelectMany(ns => ns.GetInstanceType()));
        }

        /// <summary>
        /// Returns true if the set contains no or only the object type
        /// </summary>
        internal static bool IsObjectOrUnknown(this IAnalysisSet res) {
            return res.Count == 0 || (res.Count == 1 && res.First().TypeId == BuiltinTypeId.Object);
        }

        /// <summary>
        /// Returns a sequence of all recognized string values in the set.
        /// </summary>
        internal static IEnumerable<string> GetConstantValueAsString(this IAnalysisSet values) {
            return values
                .Select(v => v.GetConstantValueAsString())
                .Where(s => !string.IsNullOrEmpty(s));
        }

        /// <summary>
        /// Performs an await operation.
        /// </summary>
        public static IAnalysisSet Await(this IAnalysisSet self, Node node, AnalysisUnit unit) {
            var res = AnalysisSet.Empty;
            foreach (var ns in self) {
                res = res.Union(ns.Await(node, unit));
            }

            return res;
        }

        class DotsLastStringComparer : IComparer<string> {
            public readonly static IComparer<string> Instance = new DotsLastStringComparer();

            private DotsLastStringComparer() { }

            public int Compare(string x, string y) {
                if (x == "...") {
                    return y == "..." ? 0 : 1;
                } else if (y == "...") {
                    return -1;
                }

                return x.CompareTo(y);
            }
        }

        public static IEnumerable<string> GetDescriptions(this IAnalysisSet self) {
            return self
                .Select(v => {
                    if (v.Push()) {
                        try {
                            return v.Description;
                        } finally {
                            v.Pop();
                        }
                    }
                    return "...";
                })
                .Where(d => !string.IsNullOrEmpty(d))
                .OrderBy(d => d, DotsLastStringComparer.Instance)
                .Distinct();
        }

        public static IEnumerable<string> GetShortDescriptions(this IAnalysisSet self) {
            return self
                .Select(v => {
                    if (v.Push()) {
                        try {
                            return v.ShortDescription;
                        } finally {
                            v.Pop();
                        }
                    }
                    return "...";
                })
                .Where(d => !string.IsNullOrEmpty(d))
                .OrderBy(d => d, DotsLastStringComparer.Instance)
                .Distinct();
        }
    }
}
