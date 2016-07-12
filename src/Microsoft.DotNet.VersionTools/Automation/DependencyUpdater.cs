// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation.PullRequest;
using Microsoft.DotNet.VersionTools.Dependencies;
using Microsoft.DotNet.VersionTools.Util;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class DependencyUpdater
    {
        /// <summary>
        /// Runs the updaters given using buildInfo sources, and returns the build infos used
        /// during the update. The returned enumerable has no duplicate entries.
        /// </summary>
        public IEnumerable<BuildInfo> Update(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<BuildInfo> buildInfos)
        {
            IEnumerable<BuildInfo> usedBuildInfos = Enumerable.Empty<BuildInfo>();

            foreach (IDependencyUpdater updater in updaters)
            {
                IEnumerable<BuildInfo> newUsedBuildInfos = updater.Update(buildInfos);
                usedBuildInfos = usedBuildInfos.Union(newUsedBuildInfos);
            }

            return usedBuildInfos.ToArray();
        }

        public async Task UpdateAndSubmitPullRequestAsync(
            IEnumerable<IDependencyUpdater> updaters,
            IEnumerable<BuildInfo> buildInfos,
            PullRequestSubmitter submitter)
        {
            IEnumerable<BuildInfo> usedBuildInfos = Update(updaters, buildInfos);

            // Ensure changes were performed as expected.
            bool hasModifiedFiles = GitHasChanges();
            bool hasUsedBuildInfo = usedBuildInfos.Any();
            if (hasModifiedFiles != hasUsedBuildInfo)
            {
                Trace.TraceError(
                    "'git status' does not match DependencyInfo information. " +
                    $"Git has modified files: {hasModifiedFiles}. " +
                    $"DependencyInfo is updated: {hasUsedBuildInfo}.");
                return;
            }
            if (!hasModifiedFiles)
            {
                Trace.TraceWarning("Dependencies are currently up to date");
                return;
            }

            await submitter.CommitSubmitAsync(usedBuildInfos);
        }

        private bool GitHasChanges()
        {
            CommandResult statusResult = Command.Git("status", "--porcelain")
                .CaptureStdOut()
                .Execute();
            statusResult.EnsureSuccessful();

            return !string.IsNullOrWhiteSpace(statusResult.StdOut);
        }
    }
}
