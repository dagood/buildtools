// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Dependencies;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class DependencyUpdater
    {
        private GitHubAuth _gitHubAuth;
        private string _projectRepo;
        private string _projectRepoOwner;
        private string _projectRepoBranch;
        private string _gitAuthorName;
        private IEnumerable<string> _notifyGitHubUsers;

        public string CommitMessageOverride { get; set; }

        public DependencyUpdater(
            GitHubAuth gitHubAuth,
            string projectRepo,
            string projectRepoOwner = null,
            string projectRepoBranch = null,
            string gitAuthorName = null,
            IEnumerable<string> notifyGitHubUsers = null)
        {
            if (gitHubAuth == null)
            {
                throw new ArgumentNullException(nameof(gitHubAuth));
            }
            _gitHubAuth = gitHubAuth;

            if (projectRepo == null)
            {
                throw new ArgumentNullException(nameof(projectRepo));
            }
            _projectRepo = projectRepo;

            _projectRepoOwner = projectRepoOwner ?? "dotnet";
            _projectRepoBranch = projectRepoBranch ?? "master";
            _gitAuthorName = gitAuthorName ?? "dotnet-bot";
            _notifyGitHubUsers = notifyGitHubUsers;
        }

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
            IEnumerable<BuildInfo> buildInfos)
        {
            IEnumerable<BuildInfo> usedBuildInfos = Update(updaters, buildInfos);

            string commitMessage = CommitMessageOverride;
            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                string updatedDependencyNames = string.Join(", ", usedBuildInfos.Select(d => d.Name));
                string updatedDependencyVersions = string.Join(", ", usedBuildInfos.Select(d => d.LatestReleaseVersion));

                commitMessage = $"Update {updatedDependencyNames} to {updatedDependencyVersions}";
                if (usedBuildInfos.Count() > 1)
                {
                    commitMessage += ", respectively";
                }
            }

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

            string remoteBranchName = $"UpdateDependencies{DateTime.UtcNow.ToString("yyyyMMddhhmmss")}";

            PushNewCommit(commitMessage, remoteBranchName);

            await SubmitPullRequestAsync(commitMessage, remoteBranchName);
        }

        private bool GitHasChanges()
        {
            return !string.IsNullOrWhiteSpace(Git("status", "--porcelain"));
        }

        private void PushNewCommit(string commitMessage, string remoteBranchName)
        {
            // Set committer in process rather than through command start options because net45 doesn't have environment options.
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", _gitAuthorName);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", _gitHubAuth.Email);

            Git("commit", "-a", "-m", commitMessage, "--author", $"{_gitAuthorName} <{_gitHubAuth.Email}>");

            string remoteUrl = $"github.com/{_gitHubAuth.User}/{_projectRepo}.git";
            string refSpec = $"HEAD:refs/heads/{remoteBranchName}";

            string logMessage = $"git push https://{remoteUrl} {refSpec}";
            Trace.TraceInformation($"EXEC {logMessage}");

            Git("push", $"https://{_gitHubAuth.User}:{_gitHubAuth.AuthToken}@{remoteUrl}", refSpec);
        }

        private async Task SubmitPullRequestAsync(string title, string remoteBranchName)
        {
            string description = "Automated update based on dotnet/versions repository.";
            if (_notifyGitHubUsers != null)
            {
                description += $"\n\n/cc @{string.Join(" @", _notifyGitHubUsers)}";
            }

            using (GitHubHttpClient client = new GitHubHttpClient(_gitHubAuth))
            {
                await client.PostGitHubPullRequestAsync(
                    title,
                    description,
                    originOwner: _gitHubAuth.User,
                    originBranch: remoteBranchName,
                    upstreamOwner: _projectRepoOwner,
                    upstreamBranch: _projectRepoBranch,
                    project: _projectRepo);
            }
        }

        private static string Git(params string[] args)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = string.Join(" ", args),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            var standardOutputData = new StringBuilder();

            process.OutputDataReceived += (sender, eventArgs) =>
            {
                standardOutputData.Append(eventArgs.Data);
                standardOutputData.Append(Environment.NewLine);

                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    Trace.TraceInformation(eventArgs.Data);
                }
            };

            process.ErrorDataReceived += (sender, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    Trace.TraceError(eventArgs.Data);
                }
            };

            string processString = $"{process.StartInfo.FileName} {process.StartInfo.Arguments}";
            Trace.TraceInformation($"Starting process '{processString}'");

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            int exitCode = process.ExitCode;

            Trace.TraceInformation($"Process ended with exit code {exitCode}.");

            if (exitCode != 0)
            {
                throw new Exception($"'{processString}' returned non-zero exit code {exitCode}.");
            }

            return standardOutputData.ToString();
        }
    }
}
