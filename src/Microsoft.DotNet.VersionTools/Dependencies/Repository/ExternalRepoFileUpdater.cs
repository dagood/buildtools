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
                        matchingInfo.Commit).Result;

                    string remoteContents = GitHubClient.FromBase64(remoteFile.Content);

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

                    string fullPath = Path.Combine(LocalRootDir, path);

                    string localMode = GetGitCachedFileMode(fullPath);

                    var tasks = localMode == null
                        ? CreateTask(matchingInfo, fullPath, remoteContents, remoteObject)
                        : UpdateTasks(matchingInfo, fullPath, localMode, remoteContents, remoteObject);

                    updateTasks.AddRange(tasks);
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

        private static IEnumerable<DependencyUpdateTask> UpdateTasks(
            IDependencyInfo info,
            string fullPath,
            string localMode,
            string remoteContents,
            GitObject remoteObject)
        {
            Action contentUpdateTask = FileUtils.GetUpdateFileContentsTask(
                fullPath,
                localContents =>
                {
                    // Detect current line ending. Depending on the platform and how core.autocrlf
                    // is configured, this may be CRLF or LF. Instead of assuming any particular
                    // config, handle both by checking the file state on disk and matching it.
                    if (localContents.Contains("\r\n"))
                    {
                        // Assume that the script is always LF in Git.
                        return remoteContents.Replace("\n", "\r\n");
                    }
                    return remoteContents;
                });

            if (contentUpdateTask != null)
            {
                yield return new DependencyUpdateTask(
                    contentUpdateTask,
                    new[] { info },
                    new[]
                    {
                        $"'{fullPath}' must have contents of '{remoteObject.Path}' at {info}"
                    });
            }

            if (localMode != remoteObject.Mode)
            {
                yield return new DependencyUpdateTask(
                    () => GitUpdateIndex(fullPath, remoteObject.Mode),
                    new[] { info },
                    new[]
                    {
                        $"'{fullPath}' must have mode {remoteObject.Mode} " +
                        $"of '{remoteObject.Path}' at {info}"
                    });
            }
        }

        private static IEnumerable<DependencyUpdateTask> CreateTask(
            IDependencyInfo info,
            string fullPath,
            string remoteContents,
            GitObject remoteObject)
        {
            yield return new DependencyUpdateTask(
                () =>
                {
                    Trace.TraceInformation($"Creating new file '{fullPath}'.");
                    File.WriteAllText(fullPath, remoteContents);

                    GitUpdateIndex(fullPath, remoteObject.Mode);
                },
                new[] { info },
                new[]
                {
                    $"'{fullPath}' must exist with contents " +
                    $"'{remoteObject.Path}' ({remoteObject.Mode}) at {info}"
                });
        }

        private static string GetGitCachedFileMode(string path)
        {
            var modeResult = GitCommand.Create("ls-files", "--stage", "--", path)
                .CaptureStdOut()
                .Execute();
            modeResult.EnsureSuccessful();

            if (modeResult.StdOut.Length < 6)
            {
                // ls-files returned no data about the file, indicating it isn't in the index.
                Trace.TraceInformation(
                    $"ls-files returned no mode for '{path}'. Stdout: '{modeResult.StdOut}'");
                return null;
            }

            string mode = modeResult.StdOut.Substring(0, 6);
            Trace.TraceInformation($"ls-files shows mode '{mode}' for path '{path}'");
            return mode;
        }

        private static void GitUpdateIndex(
            string fullPath,
            string mode)
        {
            string chmod = mode == GitObject.ModeExecutable ? "+x" : "-x";

            Trace.TraceInformation(
                $"Setting '{fullPath}' to mode {mode} (chmod {chmod}) in index.");

            GitCommand.Create("update-index", "--add", $"--chmod={chmod}", "--", fullPath)
                .Execute()
                .EnsureSuccessful();
        }
    }
}

