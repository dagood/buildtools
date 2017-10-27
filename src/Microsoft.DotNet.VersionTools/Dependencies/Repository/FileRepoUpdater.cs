// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;

namespace Microsoft.DotNet.VersionTools.Dependencies.Repository
{
    public class FileRepoUpdater : IDependencyUpdater
    {
        public string Repository { get; set; }
        public string Ref { get; set; }

        public string LocalRootDir { get; set; }
        public string RemoteRootDir { get; set; }

        public string[] RelativePaths { get; set; }

        public GitHubClient ProvidedClient { get; set; }

        public IEnumerable<DependencyUpdateTask> GetUpdateTasks(
            IEnumerable<IDependencyInfo> dependencyInfos)
        {
            RepositoryDependencyInfo matchingInfo = DependencyInfoUtils
                .FindRepositoryDependencyInfo(dependencyInfos, Repository, Ref);

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

                    var remoteProject = GitHubProject.ParseUrl(Repository);

                    var remoteContents = client.GetGitHubFileContentsAsync(
                        remotePath,
                        remoteProject,
                        Ref).Result;

                    string fullPath = Path.Combine(LocalRootDir, path);

                    updateTasks.Add(new DependencyUpdateTask(
                        FileUtils.GetUpdateFileContentsTask(fullPath, _ => remoteContents),
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
