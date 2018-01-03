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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonMultipleMembers : IPythonMultipleMembers, ILocatedMember {
        private IList<IMember> _members;
        private bool _checkForLazy;

        public AstPythonMultipleMembers() {
            _members = Array.Empty<IMember>();
        }

        private AstPythonMultipleMembers(IMember[] members) {
            _members = members;
            _checkForLazy = true;
        }

        public AstPythonMultipleMembers(IEnumerable<IMember> members) {
            _members = members.ToArray();
            _checkForLazy = true;
        }

        public static IMember Combine(IMember x, IMember y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            } else if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyMember))) {
                return y;
            } else if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyMember))) {
                return x;
            } else if (x == y) {
                return x;
            }

            var mmx = x as AstPythonMultipleMembers;
            var mmy = y as AstPythonMultipleMembers;

            if (mmx != null && mmy == null) {
                mmx.AddMember(y);
                return mmx;
            } else if (mmy != null && mmx == null) {
                mmy.AddMember(x);
                return mmy;
            } else if (mmx != null && mmy != null) {
                mmx.AddMembers(mmy._members);
                return mmx;
            } else {
                return new AstPythonMultipleMembers(new[] { x, y });
            }
        }

        public void AddMember(IMember member) {
            if (member == this) {
                return;
            } else if (member is IPythonMultipleMembers mm) {
                AddMembers(mm.Members);
                return;
            }
            var old = _members;
            if (!old.Contains(member)) {
                _members = old.Concat(Enumerable.Repeat(member, 1)).ToArray();
                _checkForLazy = true;
            } else if (!old.Any()) {
                _members = new[] { member };
                _checkForLazy = true;
            }
        }

        public void AddMembers(IEnumerable<IMember> members) {
            var old = _members;
            if (old.Any()) {
                _members = old.Union(members.Where(m => m != this)).ToArray();
                _checkForLazy = true;
            } else {
                _members = members.Where(m => m != this).ToArray();
                _checkForLazy = true;
            }
        }


        public IList<IMember> Members {
            get {
                if (_checkForLazy) {
                    _members = _members.Select(m => (m as ILazyMember)?.Get() ?? m).Where(m => m != this).ToArray();
                    _checkForLazy = false;
                }
                return _members;
            }
        }

        public PythonMemberType MemberType => PythonMemberType.Multiple;

        public IEnumerable<LocationInfo> Locations => _members.OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());
    }
}
