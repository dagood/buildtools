// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Upgrade
{
    public class FileRegexReleaseUpgrader : FileRegexUpgrader
    {
        public string BuildInfoName { get; set; }

        protected override string GetDesiredValue(IEnumerable<BuildInfo> buildInfos)
        {
            BuildInfo project = buildInfos.SingleOrDefault(d => d.Name == BuildInfoName);

            if (project == null)
            {
                Trace.TraceError($"Could not find build info for project named {BuildInfoName}");
                return $"PROJECT '{BuildInfoName}' NOT FOUND";
            }

            BuildInfosUsed.Add(project);

            return project.LatestReleaseVersion;
        }
    }
}