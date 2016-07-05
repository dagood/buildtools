using Microsoft.DotNet.VersionTools.Upgrade;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class ProjectUpgradePr
    {
        private GitHubAuth _gitHubAuth;
        private string _projectRepo;
        private string _projectRepoOwner;
        private string _projectRepoBranch;
        private string _gitAuthorName;
        private IEnumerable<string> _notifyGitHubUsers;

        public string CommitMessageOverride { get; set; }

        public ProjectUpgradePr(
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

        public async Task CreateAndSubmitAsync(
            IEnumerable<IDependencyUpgrader> upgraders,
            IEnumerable<BuildInfo> buildInfos)
        {
            IEnumerable<BuildInfo> usedBuildInfos = Enumerable.Empty<BuildInfo>();

            foreach (IDependencyUpgrader upgrader in upgraders)
            {
                upgrader.Upgrade(buildInfos);
                usedBuildInfos = usedBuildInfos.Union(upgrader.BuildInfosUsed);
            }
            usedBuildInfos = usedBuildInfos.ToArray();

            string commitMessage = CommitMessageOverride;
            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                string updatedDependencyNames = string.Join(", ", usedBuildInfos.Select(d => d.Name));
                string updatedDependencyVersions = string.Join(", ", usedBuildInfos.Select(d => d.LatestReleaseVersion));

                commitMessage = $"Upgrade {updatedDependencyNames} to {updatedDependencyVersions}";
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
            CommandResult statusResult = Git("status", "--porcelain")
                .CaptureStdOut()
                .Execute();
            statusResult.EnsureSuccessful();

            return !string.IsNullOrWhiteSpace(statusResult.StdOut);
        }

        private void PushNewCommit(string commitMessage, string remoteBranchName)
        {
            // Set committer in process rather than through command start options because net45 doesn't have environment options.
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", _gitAuthorName);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", _gitHubAuth.Email);

            Git("commit", "-a", "-m", commitMessage, "--author", $"{_gitAuthorName} <{_gitHubAuth.Email}>")
                .Execute()
                .EnsureSuccessful();

            string remoteUrl = $"github.com/{_gitHubAuth.User}/{_projectRepo}.git";
            string refSpec = $"HEAD:refs/heads/{remoteBranchName}";

            string logMessage = $"git push https://{remoteUrl} {refSpec}";
            Trace.TraceInformation($"EXEC {logMessage}");

            CommandResult pushResult =
                Git("push", $"https://{_gitHubAuth.User}:{_gitHubAuth.AuthToken}@{remoteUrl}", refSpec)
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

        private async Task SubmitPullRequestAsync(string title, string remoteBranchName)
        {
            string description = "Automated upgrade based on dotnet/versions repository.";
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

        private static Command Git(params string[] args) => Command.Create("git", args);
    }
}
