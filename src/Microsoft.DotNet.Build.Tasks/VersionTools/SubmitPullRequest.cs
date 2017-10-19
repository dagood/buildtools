﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class SubmitPullRequest : BuildTask
    {
        [Required]
        public string GitHubAuthToken { get; set; }
        [Required]
        public string GitHubUser { get; set; }
        [Required]
        public string GitHubEmail { get; set; }

        public string ProjectRepoOwner { get; set; }

        [Required]
        public string ProjectRepoName { get; set; }
        [Required]
        public string ProjectRepoBranch { get; set; }

        [Required]
        public string CommitMessage { get; set; }

        /// <summary>
        /// Title of the pull request. Defaults to the commit message.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Body of the pull request. Optional.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// The git author of the update commit. Defaults to the same as GitHubUser.
        /// </summary>
        public string GitHubAuthor { get; set; }

        public ITaskItem[] NotifyGitHubUsers { get; set; }

        public bool AlwaysCreateNewPullRequest { get; set; }

        public override bool Execute()
        {
            Trace.Listeners.MsBuildListenedInvoke(Log, TraceListenedExecute);
            return !Log.HasLoggedErrors;
        }

        private void TraceListenedExecute()
        {
            var auth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);

            using (GitHubClient client = new GitHubClient(auth))
            {
                var origin = new GitHubProject(ProjectRepoName, GitHubUser);

                var upstreamBranch = new GitHubBranch(
                    ProjectRepoBranch,
                    new GitHubProject(ProjectRepoName, ProjectRepoOwner));

                string body = string.Empty;
                if (NotifyGitHubUsers != null)
                {
                    body += PullRequestCreator.NotificationString(NotifyGitHubUsers.Select(item => item.ItemSpec));
                }

                var prCreator = new PullRequestCreator(client.Auth, origin, upstreamBranch, GitHubAuthor);
                prCreator.CreateOrUpdateAsync(
                    CommitMessage,
                    CommitMessage + $" ({ProjectRepoBranch})",
                    body,
                    forceCreate: AlwaysCreateNewPullRequest).Wait();
            }
        }
    }
}
