// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Versioning;

namespace Microsoft.DotNet.VersionTools.Upgrade
{
    public class ProjectJsonUpgrader : IDependencyUpgrader
    {
        private List<BuildInfo> _buildInfosUsed = new List<BuildInfo>();

        public IEnumerable<BuildInfo> BuildInfosUsed => _buildInfosUsed;

        public IEnumerable<string> ProjectJsonPaths { get; }

        public bool SkipStableVersions { get; set; } = true;

        public ProjectJsonUpgrader(IEnumerable<string> projectJsonPaths)
        {
            ProjectJsonPaths = projectJsonPaths;
        }

        public void Upgrade(IEnumerable<BuildInfo> buildInfos)
        {
            foreach (string projectJsonFile in ProjectJsonPaths)
            {
                JObject projectRoot;
                try
                {
                    projectRoot = ReadProject(projectJsonFile);
                }
                catch (Exception e)
                {
                    Trace.TraceWarning($"Non-fatal exception occurred reading '{projectJsonFile}'. Skipping file. Exception: {e}. ");
                    continue;
                }

                if (projectRoot == null)
                {
                    Trace.TraceWarning($"A non valid JSON file was encountered '{projectJsonFile}'. Skipping file.");
                    continue;
                }

                bool changedAnyDependency = FindAllDependencyProperties(projectRoot)
                    .Select(dependencyProperty => ReplaceDependencyVersion(projectJsonFile, dependencyProperty, buildInfos))
                    .ToArray()
                    .Any(shouldWrite => shouldWrite);

                if (changedAnyDependency)
                {
                    Trace.TraceInformation($"Writing changes to {projectJsonFile}");
                    WriteProject(projectRoot, projectJsonFile);
                }
            }
        }

        /// <summary>
        /// Replaces the single dependency with the updated version, if it matches any of the
        /// dependencies that need to be updated. Stops on the first upgraded value found.
        /// </summary>
        private bool ReplaceDependencyVersion(
            string projectJsonFile,
            JProperty dependencyProperty,
            IEnumerable<BuildInfo> buildInfos)
        {
            string id = dependencyProperty.Name;
            foreach (BuildInfo buildInfo in buildInfos)
            {
                foreach (PackageInfo packageInfo in buildInfo.LatestPackages)
                {
                    if (id != packageInfo.Id)
                    {
                        continue;
                    }

                    string oldVersion;
                    if (dependencyProperty.Value is JObject)
                    {
                        oldVersion = (string)dependencyProperty.Value["version"];
                    }
                    else
                    {
                        oldVersion = (string)dependencyProperty.Value;
                    }
                    VersionRange parsedOldVersionRange;
                    if (!VersionRange.TryParse(oldVersion, out parsedOldVersionRange))
                    {
                        Trace.TraceWarning($"Couldn't parse '{oldVersion}' for package '{id}' in '{projectJsonFile}'. Skipping.");
                        continue;
                    }
                    NuGetVersion oldNuGetVersion = parsedOldVersionRange.MinVersion;

                    if (SkipStableVersions && !oldNuGetVersion.IsPrerelease)
                    {
                        continue;
                    }

                    if (oldNuGetVersion != packageInfo.Version)
                    {
                        string newVersion = packageInfo.Version.ToNormalizedString();
                        if (dependencyProperty.Value is JObject)
                        {
                            dependencyProperty.Value["version"] = newVersion;
                        }
                        else
                        {
                            dependencyProperty.Value = newVersion;
                        }

                        // mark the BuildInfo as used so we can tell which dependencies were used for this upgrade.
                        _buildInfosUsed.Add(buildInfo);

                        return true;
                    }
                }
            }

            return false;
        }

        private static JObject ReadProject(string projectJsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine);
        }

        private static IEnumerable<JProperty> FindAllDependencyProperties(JObject projectJsonRoot)
        {
            return projectJsonRoot
                .Descendants()
                .OfType<JProperty>()
                .Where(property => property.Name == "dependencies")
                .Select(property => property.Value)
                .SelectMany(o => o.Children<JProperty>());
        }
    }
}
