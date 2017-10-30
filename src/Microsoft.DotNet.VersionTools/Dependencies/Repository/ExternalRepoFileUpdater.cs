// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.VersionTools.Dependencies.Repository
{
    public class ExternalRepoFileUpdater : IDependencyUpdater
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

            var treeCache = new Dictionary<string, GitTree>();

            UseClient(client =>
            {
                foreach (string path in RelativePaths)
                {
                    string[] remoteDirSegments = RemoteRootDir?.Split('/', '\\') ?? new string[0];
                    string[] remotePathSegments = remoteDirSegments
                        .Concat(path.Split('/', '\\'))
                        .ToArray();

                    string remotePath = string.Join("/", remotePathSegments);

                    var remoteProject = GitHubProject.ParseUrl(matchingInfo.Repository);

                    GitHubContents remoteFile = client.GetGitHubFileAsync(
                        remotePath,
                        remoteProject,
                        matchingInfo.Ref).Result;

                    GitCommit remoteCommit = client.GetCommitAsync(
                        remoteProject,
                        matchingInfo.Commit).Result;

                    treeCache[string.Empty] = client.GetTreeAsync(remoteProject, remoteCommit.Tree.Sha).Result;

                    string remoteDir = remoteDirSegments.Aggregate(
                        string.Empty,
                        (acc, segment) =>
                        {
                            GitTree parent = treeCache[acc];

                            Trace.TraceInformation($"Looking in {acc} for {segment} ({parent.Url})");

                            GitObject gitObject = parent.Tree
                                .Single(o =>
                                    string.Equals(o.Path, segment, StringComparison.OrdinalIgnoreCase) &&
                                    o.Type == GitObject.TypeTree);

                            GitTree gitTree = client.GetTreeAsync(remoteProject, gitObject.Sha).Result;

                            string s = acc + "/" + segment;
                            treeCache[s] = gitTree;
                            return s;
                        });

                    GitObject remoteObject = treeCache[remoteDir].Tree
                        .Single(o =>
                            string.Equals(o.Path, remotePathSegments.Last(), StringComparison.OrdinalIgnoreCase) &&
                            o.Type == GitObject.TypeBlob);

                    string remoteContents = GitHubClient.FromBase64(remoteFile.Content);

                    string fullPath = Path.Combine(LocalRootDir, path);

                    Action fileUpdate = FileUtils.GetUpdateFileContentsTask(
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
                        },
                        () =>
                        {
                            File.WriteAllText(fullPath, remoteContents);

                            string chmod = remoteObject.Mode == GitObject.ModeExecutable
                                ? "+x"
                                : "-x";

                            GitCommand.Create("update-index", "--add", $"--chmod={chmod}")
                                .Execute()
                                .EnsureSuccessful();
                        });

                    var modeResult = GitCommand.Create("ls-files", "--cached", "--", fullPath)
                        .CaptureStdOut()
                        .Execute();
                    modeResult.EnsureSuccessful();

                    string mode = modeResult.StdOut.Substring(0, 6);

                    if ((mode == GitObject.ModeFile || mode == GitObject.ModeExecutable) &&
                        mode != remoteObject.Mode)
                    {
                        GitCommand.Create("update-index", "--add", $"--chmod={chmod}")
                            .Execute()
                            .EnsureSuccessful();
                    }

                    if (fileUpdate != null)
                    {
                        updateTasks.Add(new DependencyUpdateTask(
                            fileUpdate,
                            new[] { matchingInfo },
                            new[]
                            {
                                $"'{fullPath}' must have contents of '{remotePath}' at {matchingInfo}."
                            }));
                    }
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

