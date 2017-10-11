﻿// Visual Studio Shared Project
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
using System.ComponentModel.Design;
using Microsoft.VisualStudio.ComponentModelHost;

namespace TestUtilities.Mocks {
    public class MockServiceProvider : IServiceProvider, IServiceContainer {
        public readonly Dictionary<Guid, object> Services = new Dictionary<Guid, object>();
        private readonly Dictionary<Guid, Func<object>> _serviceCreators = new Dictionary<Guid, Func<object>>();
        public readonly MockComponentModel ComponentModel = new MockComponentModel();

        public MockServiceProvider() {
            Services[typeof(SComponentModel).GUID] = ComponentModel;
        }

        public object GetService(Type serviceType) {
            object service;
            Console.WriteLine("MockServiceProvider.GetService({0})", serviceType.Name);
            if (Services.TryGetValue(serviceType.GUID, out service)) {
                return service;
            }
            Func<object> serviceCreator;
            if (_serviceCreators.TryGetValue(serviceType.GUID, out serviceCreator)) {
                Console.WriteLine("Creating service {0} lazily", serviceType.Name);
                _serviceCreators.Remove(serviceType.GUID);
                Services[serviceType.GUID] = service = serviceCreator();
                return service;
            }
            return null;
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback, bool promote) {
            if (callback == null) {
                Services[serviceType.GUID] = null;
            } else {
                _serviceCreators[serviceType.GUID] = () => callback(this, serviceType);
            }
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback) {
            AddService(serviceType, callback, true);
        }

        public void AddService(Type serviceType, object serviceInstance, bool promote) {
            Services[serviceType.GUID] = serviceInstance;
        }

        public void AddService(Type serviceType, object serviceInstance) {
            AddService(serviceType, serviceInstance, true);
        }

        public void RemoveService(Type serviceType, bool promote) {
            Services.Remove(serviceType.GUID);
        }

        public void RemoveService(Type serviceType) {
            RemoveService(serviceType, true);
        }
    }
}
