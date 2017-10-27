// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;

namespace Microsoft.DotNet.VersionTools.Dependencies.Repository
{
    public class FileRepoUpdater : IDependencyUpdater
    {
        public string RepositoryIdentity { get; set; }

        public string LocalRootDir { get; set; }
        public string RemoteRootDir { get; set; }

        public string[] RelativePaths { get; set; }

        public GitHubClient ProvidedClient { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<IDependencyInfo> dependencyInfos)
        {
            RepositoryDependencyInfo matchingInfo = dependencyInfos
                .OfType<RepositoryDependencyInfo>()
                .Single(info => string.Equals(info.Identity, RepositoryIdentity, StringComparison.OrdinalIgnoreCase));

            var updateTasks = new List<DependencyUpdateTask>();

            UseClient(client =>
            {
                foreach (string path in RelativePaths)
                {
                    string remotePath = path;
                    if (!string.IsNullOrEmpty(RemoteRootDir))
                    {
                        remotePath = string.Join("/", RemoteRootDir, path);
                    }

                    var remoteProject = GitHubProject.ParseUrl(matchingInfo.Repository);

                    var remoteContents = client.GetGitHubFileContentsAsync(
                        remotePath,
                        remoteProject,
                        matchingInfo.Ref).Result;

                    string fullPath = Path.Combine(LocalRootDir, path);

                    updateTasks.Add(new DependencyUpdateTask(
                        FileUtils.GetUpdateFileContentsTask(
                            fullPath,
                            localContents =>
                            {
                                // Detect current line ending. Depending on the platform and how
                                // core.autocrlf is configured, this may be CRLF or LF. Instead of
                                // assuming any particular config, handle both by checking the file
                                // state on disk and matching it.
                                if (localContents.Contains("\r\n"))
                                {
                                    // Assume that the script is always LF in Git.
                                    return remoteContents.Replace("\n", "\r\n");
                                }
                                return remoteContents;
                            }),
                        new[] { matchingInfo },
                        new[]
                        {
                            $"'{fullPath}' must have contents of {matchingInfo} '{remotePath}'"
                        }));
                }
            });

            return updateTasks;
        }

        private void UseClient(Action<GitHubClient> action)
        {
            if (ProvidedClient != null)
            {
                action(ProvidedClient);
            }
            else
            {
                using (var client = new GitHubClient(null))
                {
                    action(client);
                }
            }
        }
    }
}
