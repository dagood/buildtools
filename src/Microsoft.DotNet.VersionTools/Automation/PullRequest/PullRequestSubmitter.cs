// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation.PullRequest
{
    public class PullRequestSubmitter
    {
        public const string UpdateDependenciesBranchPrefix = "UpdateDependencies";

        protected PullRequestConfig Config { get; }

        public PullRequestSubmitter(PullRequestConfig config)
        {
            if (Config.GitHubAuth == null)
            {
                throw new ArgumentNullException(nameof(Config.GitHubAuth));
            }
            if (Config.ProjectRepo == null)
            {
                throw new ArgumentNullException(nameof(Config.ProjectRepo));
            }
            Config = config;
        }

        public async Task CommitSubmitAsync(IEnumerable<BuildInfo> usedBuildInfos)
        {
            string commitMessage = GetCommitMessage(usedBuildInfos);

            string remoteBranchName = await GetRemoteBranchNameAsync();

            Commit(commitMessage);
            Push(remoteBranchName);
            await SubmitPullRequestAsync(commitMessage, remoteBranchName);
        }

        protected virtual string GetCommitMessage(IEnumerable<BuildInfo> usedBuildInfos)
        {
            string updatedDependencyNames = string.Join(", ", usedBuildInfos.Select(d => d.Name));
            string updatedDependencyVersions = string.Join(", ", usedBuildInfos.Select(d => d.LatestReleaseVersion));

            string commitMessage = $"Update {updatedDependencyNames} to {updatedDependencyVersions}";
            if (usedBuildInfos.Count() > 1)
            {
                commitMessage += ", respectively";
            }
            return commitMessage;
        }

        protected virtual Task<string> GetRemoteBranchNameAsync()
        {
            return Task.FromResult($"UpdateDependencies{DateTime.UtcNow.ToString("yyyyMMddhhmmss")}");
        }

        protected virtual async Task SubmitPullRequestAsync(string title, string remoteBranchName)
        {
            string description = "Automated update based on dotnet/versions repository.";
            if (Config.NotifyGitHubUsers != null)
            {
                description += $"\n\n/cc @{string.Join(" @", Config.NotifyGitHubUsers)}";
            }

            using (GitHubHttpClient client = new GitHubHttpClient(Config.GitHubAuth))
            {
                await client.PostGitHubPullRequestAsync(
                    title,
                    description,
                    originOwner: Config.GitHubAuth.User,
                    originBranch: remoteBranchName,
                    upstreamOwner: Config.ProjectRepoOwner,
                    upstreamBranch: Config.ProjectRepoBranch,
                    project: Config.ProjectRepo);
            }
        }

        protected void Commit(string message)
        {
            // Set committer in process rather than through command start options because net45 doesn't have environment options.
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", Config.GitAuthorName);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", Config.GitHubAuth.Email);

            Command.Git("commit", "-a", "-m", message, "--author", $"{Config.GitAuthorName} <{Config.GitHubAuth.Email}>")
                .Execute()
                .EnsureSuccessful();
        }

        protected void Push(string remoteBranchName)
        {
            string remoteUrl = $"github.com/{Config.GitHubAuth.User}/{Config.ProjectRepo}.git";
            string refSpec = $"HEAD:refs/heads/{remoteBranchName}";

            string logMessage = $"git push https://{remoteUrl} {refSpec}";
            Trace.TraceInformation($"EXEC {logMessage}");

            CommandResult pushResult =
                Command.Git("push", $"https://{Config.GitHubAuth.User}:{Config.GitHubAuth.AuthToken}@{remoteUrl}", refSpec)
                    .QuietBuildReporter()  // we don't want secrets showing up in our logs
                    .CaptureStdErr() // git push will write to StdErr upon success, disable that
                    .CaptureStdOut()
                    .Execute();

            var message = logMessage + $" exited with {pushResult.ExitCode}";
            if (pushResult.ExitCode == 0)
            {
                Trace.TraceInformation($"EXEC success: {message}");
            }
            else
            {
                Trace.TraceError($"EXEC failure: {message}");
            }

            pushResult.EnsureSuccessful(suppressOutput: true);
        }
    }
}
