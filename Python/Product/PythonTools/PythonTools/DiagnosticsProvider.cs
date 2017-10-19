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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Web;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools {
    internal sealed class DiagnosticsProvider {
        private readonly IServiceProvider _serviceProvider;

        private static readonly IEnumerable<string> InterestingDteProperties = new[] {
            "InterpreterId",
            "InterpreterVersion",
            "StartupFile",
            "WorkingDirectory",
            "PublishUrl",
            "SearchPath",
            "CommandLineArguments",
            "InterpreterPath"
        };
        
        private static readonly IEnumerable<string> InterestingProjectProperties = new[] {
            "ClusterRunEnvironment",
            "ClusterPublishBeforeRun",
            "ClusterWorkingDir",
            "ClusterMpiExecCommand",
            "ClusterAppCommand",
            "ClusterAppArguments",
            "ClusterDeploymentDir",
            "ClusterTargetPlatform",
            PythonWebLauncher.DebugWebServerTargetProperty,
            PythonWebLauncher.DebugWebServerTargetTypeProperty,
            PythonWebLauncher.DebugWebServerArgumentsProperty,
            PythonWebLauncher.DebugWebServerEnvironmentProperty,
            PythonWebLauncher.RunWebServerTargetProperty,
            PythonWebLauncher.RunWebServerTargetTypeProperty,
            PythonWebLauncher.RunWebServerArgumentsProperty,
            PythonWebLauncher.RunWebServerEnvironmentProperty,
            PythonWebPropertyPage.StaticUriPatternSetting,
            PythonWebPropertyPage.StaticUriRewriteSetting,
            PythonWebPropertyPage.WsgiHandlerSetting
        };

        private static readonly Regex InterestingApplicationLogEntries = new Regex(
            @"^Application: (devenv\.exe|.+?Python.+?\.exe|ipy(64)?\.exe)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        public DiagnosticsProvider(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public void WriteLog(TextWriter writer, bool includeAnalysisLog) {
            var pythonPathIsMasked = _serviceProvider.GetPythonToolsService().GeneralOptions.ClearGlobalPythonPath
                ? " (masked)"
                : "";
            var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
            var model = _serviceProvider.GetComponentModel();
            var knownProviders = model.GetExtensions<IPythonInterpreterFactoryProvider>().ToArray();
            var launchProviders = model.GetExtensions<IPythonLauncherProvider>().ToArray();
            var inMemLogger = model.GetService<InMemoryLogger>();

            writer.WriteLine("Projects: ");

            var projects = dte.Solution.Projects;

            foreach (EnvDTE.Project project in projects) {
                string name;
                try {
                    // Some projects will throw rather than give us a unique
                    // name. They are not ours, so we will ignore them.
                    name = project.UniqueName;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    bool isPythonProject = false;
                    try {
                        isPythonProject = Utilities.GuidEquals(PythonConstants.ProjectFactoryGuid, project.Kind);
                    } catch (Exception ex2) when (!ex2.IsCriticalException()) {
                    }

                    if (isPythonProject) {
                        // Actually, it was one of our projects, so we do care
                        // about the exception. We'll add it to the output,
                        // rather than crashing.
                        writer.WriteLine("    Project: " + ex.Message);
                        writer.WriteLine("        Kind: Python");
                    }
                    continue;
                }
                writer.WriteLine("    Project: " + name);

                if (Utilities.GuidEquals(PythonConstants.ProjectFactoryGuid, project.Kind)) {
                    writer.WriteLine("        Kind: Python");

                    foreach (var prop in InterestingDteProperties) {
                        writer.WriteLine("        " + prop + ": " + GetProjectProperty(project, prop));
                    }

                    var pyProj = project.GetPythonProject();
                    if (pyProj != null) {
                        foreach (var prop in InterestingProjectProperties) {
                            var propValue = pyProj.GetProjectProperty(prop);
                            if (propValue != null) {
                                writer.WriteLine("        " + prop + ": " + propValue);
                            }
                        }

                        foreach (var factory in pyProj.InterpreterFactories) {
                            writer.WriteLine();
                            writer.WriteLine("        Interpreter: " + factory.Configuration.Description);
                            writer.WriteLine("            Id: " + factory.Configuration.Id);
                            writer.WriteLine("            Version: " + factory.Configuration.Version);
                            writer.WriteLine("            Arch: " + factory.Configuration.Architecture);
                            writer.WriteLine("            Prefix Path: " + factory.Configuration.PrefixPath ?? "(null)");
                            writer.WriteLine("            Path: " + factory.Configuration.InterpreterPath ?? "(null)");
                            writer.WriteLine("            Windows Path: " + factory.Configuration.WindowsInterpreterPath ?? "(null)");
                            writer.WriteLine(string.Format("            Path Env: {0}={1}{2}",
                                factory.Configuration.PathEnvironmentVariable ?? "(null)",
                                Environment.GetEnvironmentVariable(factory.Configuration.PathEnvironmentVariable ?? ""),
                                pythonPathIsMasked
                            ));
                        }
                    }
                } else {
                    writer.WriteLine("        Kind: " + project.Kind);
                }

                writer.WriteLine();
            }

            writer.WriteLine("Environments: ");
            foreach (var provider in knownProviders.MaybeEnumerate()) {
                writer.WriteLine("    " + provider.GetType().FullName);
                foreach (var config in provider.GetInterpreterConfigurations()) {
                    writer.WriteLine("        Id: " + config.Id);
                    writer.WriteLine("        Factory: " + config.Description);
                    writer.WriteLine("        Version: " + config.Version);
                    writer.WriteLine("        Arch: " + config.Architecture);
                    writer.WriteLine("        Prefix Path: " + config.PrefixPath ?? "(null)");
                    writer.WriteLine("        Path: " + config.InterpreterPath ?? "(null)");
                    writer.WriteLine("        Windows Path: " + config.WindowsInterpreterPath ?? "(null)");
                    writer.WriteLine("        Path Env: " + config.PathEnvironmentVariable ?? "(null)");
                    writer.WriteLine();
                }
            }

            writer.WriteLine("Launchers:");
            foreach (var launcher in launchProviders.MaybeEnumerate()) {
                writer.WriteLine("    Launcher: " + launcher.GetType().FullName);
                writer.WriteLine("        " + launcher.Description);
                writer.WriteLine("        " + launcher.Name);
                writer.WriteLine();
            }

            try {
                writer.WriteLine("Logged events/stats:");
                writer.WriteLine(inMemLogger.ToString());
                writer.WriteLine();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                writer.WriteLine("  Failed to access event log.");
                writer.WriteLine(ex.ToString());
                writer.WriteLine();
            }

            if (includeAnalysisLog) {
                try {
                    writer.WriteLine("System events:");

                    var application = new EventLog("Application");
                    var lastWeek = DateTime.Now.Subtract(TimeSpan.FromDays(7));
                    foreach (var entry in application.Entries.Cast<EventLogEntry>()
                        .Where(e => e.InstanceId == 1026L)  // .NET Runtime
                        .Where(e => e.TimeGenerated >= lastWeek)
                        .Where(e => InterestingApplicationLogEntries.IsMatch(e.Message))
                        .OrderByDescending(e => e.TimeGenerated)
                    ) {
                        writer.WriteLine(string.Format("Time: {0:s}", entry.TimeGenerated));
                        using (var reader = new StringReader(entry.Message.TrimEnd())) {
                            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                                writer.WriteLine(line);
                            }
                        }
                        writer.WriteLine();
                    }

                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    writer.WriteLine("  Failed to access event log.");
                    writer.WriteLine(ex.ToString());
                    writer.WriteLine();
                }
            }

            writer.WriteLine("Loaded assemblies:");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(assem => assem.FullName)) {
                AssemblyFileVersionAttribute assemFileVersion;
                var error = "(null)";
                try {
                    assemFileVersion = assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false)
                        .OfType<AssemblyFileVersionAttribute>()
                        .FirstOrDefault();
                } catch (Exception e) when (!e.IsCriticalException()) {
                    assemFileVersion = null;
                    error = string.Format("{0}: {1}", e.GetType().Name, e.Message);
                }

                writer.WriteLine(string.Format("  {0}, FileVersion={1}",
                    assembly.FullName,
                    assemFileVersion?.Version ?? error
                ));
            }
            writer.WriteLine();

            string globalAnalysisLog = PythonTypeDatabase.GlobalLogFilename;
            if (File.Exists(globalAnalysisLog)) {
                writer.WriteLine("Global Analysis:");
                try {
                    writer.WriteLine(File.ReadAllText(globalAnalysisLog));
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    writer.WriteLine("Error reading the global analysis log.");
                    writer.WriteLine("Please wait for analysis to complete and try again.");
                    writer.WriteLine(ex.ToString());
                }
            }
            writer.WriteLine();

            if (includeAnalysisLog) {
                writer.WriteLine("Environment Analysis Logs: ");
                foreach (var provider in knownProviders) {
                    foreach (var factory in provider.GetInterpreterFactories().OfType<IPythonInterpreterFactoryWithLog>()) {
                        writer.WriteLine(((IPythonInterpreterFactory)factory).Configuration.Description);
                        string analysisLog = factory.GetAnalysisLogContent(CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(analysisLog)) {
                            writer.WriteLine(analysisLog);
                        }
                        writer.WriteLine();
                    }
                }
            }
        }

        private static string GetProjectProperty(EnvDTE.Project project, string name) {
            try {
                return project.Properties.Item(name)?.Value?.ToString() ?? "<undefined>";
            } catch {
                return "<undefined>";
            }
        }
    }
}
