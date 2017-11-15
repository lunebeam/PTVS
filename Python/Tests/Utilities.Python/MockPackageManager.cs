﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockPackageManager : IPackageManager {
        private readonly List<PackageSpec> _installed = new List<PackageSpec>();
        private readonly List<PackageSpec> _installable = new List<PackageSpec>();

        private bool _seenChange;

        public bool IsReady { get; set; }
        public IPythonInterpreterFactory Factory { get; set; }

        public event EventHandler InstalledFilesChanged { add { throw new NotImplementedException(); } remove { } }
        public event EventHandler InstalledPackagesChanged;
        public event EventHandler IsReadyChanged { add { throw new NotImplementedException(); } remove { } }

        public string ExtensionDisplayName => string.Empty;

        public string IndexDisplayName => string.Empty;

        public string SearchHelpText => string.Empty;

        public string GetInstallCommandDisplayName(string searchQuery) => string.Empty;

        public bool CanBeUninstalled(PackageSpec package) => true;

        public void SetInterpreterFactory(IPythonInterpreterFactory factory) {
            Factory = factory;
        }

        public Task<bool> ExecuteAsync(string arguments, IPackageManagerUI ui, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        public async Task<PackageSpec> GetInstallablePackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            if (!package.IsValid) {
                return new PackageSpec();
            }
            await Task.Delay(10);
            return _installable.FirstOrDefault(p => p.Name == package.Name) ?? new PackageSpec();
        }

        public async Task<IList<PackageSpec>> GetInstallablePackagesAsync(CancellationToken cancellationToken) {
            await Task.Delay(10);
            return _installable;
        }

        public Task<PackageSpec> GetInstalledPackageAsync(PackageSpec package, CancellationToken cancellationToken) {
            if (!package.IsValid) {
                return Task.FromResult(new PackageSpec());
            }
            return Task.FromResult(_installed.FirstOrDefault(p => p.Name == package.Name) ?? new PackageSpec());
        }

        public Task<IList<PackageSpec>> GetInstalledPackagesAsync(CancellationToken cancellationToken) {
            return Task.FromResult<IList<PackageSpec>>(_installed);
        }

        public async Task<bool> InstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            ui?.OnOperationStarted(this, "install " + package.FullSpec);
            _installed.Add(package);
            await Task.Delay(100, cancellationToken);
            _seenChange = true;
            ui?.OnOperationFinished(this, "install " + package.FullSpec, true);
            return true;
        }

        public void AddInstallable(PackageSpec package) {
            _installable.Add(package);
        }

        public void NotifyPackagesChanged() {
            if (_seenChange) {
                InstalledPackagesChanged?.Invoke(this, EventArgs.Empty);
                _seenChange = false;
            }
        }

        public Task PrepareAsync(IPackageManagerUI ui, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        public IDisposable SuppressNotifications() {
            return NoPackageManager.NoSuppression.Instance;
        }

        public Task<bool> UninstallAsync(PackageSpec package, IPackageManagerUI ui, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
