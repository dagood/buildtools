// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation.PullRequest
{
    public class UpdatePullRequestSubmitter : PullRequestSubmitter
    {
        /// <summary>
        /// The pull request to update during the current CommitSubmitAsync. Found in
        /// GetRemoteBranchNameAsync. If null, no suitable pull request was found.
        /// </summary>
        private GitHubPullRequest _pullRequest;

        public UpdatePullRequestSubmitter(PullRequestConfig config) : base(config)
        {
        }

        /// <summary>
        /// Use base behavior (create a new PR) if there is no PR to update or if the found PR
        /// isn't available to update because someone else pushed a commit.
        /// </summary>
        private bool IsPullRequestUpdatable =>
            _pullRequest == null || _pullRequest.Head.User.Login != Config.GitHubAuth.User;

        protected sealed override async Task<string> GetRemoteBranchNameAsync()
        {
            using (var client = new GitHubHttpClient(Config.GitHubAuth))
            {
                _pullRequest = await client.FindPullRequestByHeadAsync(
                    Config.ProjectRepoOwner,
                    Config.ProjectRepo,
                    UpdateDependenciesBranchPrefix,
                    Config.GitHubAuth.User);

                if (!IsPullRequestUpdatable)
                {
                    return await base.GetRemoteBranchNameAsync();
                }

                // Push to the found PR's branch.
                return _pullRequest.Head.Ref;
            }
        }

        protected sealed override Task SubmitPullRequestAsync(string title, string remoteBranchName)
        {
            if (!IsPullRequestUpdatable)
            {
                return base.SubmitPullRequestAsync(title, remoteBranchName);
            }

            // The pull request has already been updated by Push.
            // TODO: Notify any PR subscribers with a comment.

            // Use FromResult(0) as a way to return a completed task without Task.CompletedTask available.
            return Task.FromResult(0);
        }
    }
}
