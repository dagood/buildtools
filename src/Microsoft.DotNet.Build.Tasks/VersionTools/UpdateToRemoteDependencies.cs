﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdateToRemoteDependencies : BaseDependenciesTask
    {
        public string CurrentRefXmlPath { get; set; }

        /// <summary>
        /// If provided, GitHub authentication info is used to fetch the remote dotnet/versions
        /// commit. The anonymous user rate limit is small, and this can be used to use an account's
        /// quota instead. The "...AndSubmitPullRequest" subclass also uses this to create the PR.
        /// </summary>
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }

        [Output]
        public bool MadeChanges { get; set; }

        [Output]
        public string SuggestedCommitMessage { get; set; }

        protected override void TraceListenedExecute()
        {
            GitHubAuth auth = null;

            if (string.IsNullOrEmpty(GitHubAuthToken))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    $"No value for '{nameof(GitHubAuthToken)}'. " +
                    "Accessing GitHub API anonymously.");
            }
            else
            {
                auth = new GitHubAuth(GitHubAuthToken, GitHubUser, null);
            }

            using (var client = new GitHubClient(auth))
            {
                DependencyUpdateResults updateResults = UpdateToRemote(client);

                MadeChanges = updateResults.ChangesDetected();
                SuggestedCommitMessage = updateResults.GetSuggestedCommitMessage();

                Log.LogMessage(
                    MessageImportance.Normal,
                    $"Suggested commit message: '{SuggestedCommitMessage}'");
            }
        }

        protected DependencyUpdateResults UpdateToRemote(GitHubClient client)
        {
            // Use the commit hash of the remote dotnet/versions repo master branch.
            string versionsCommitHash = client
                .GetReferenceAsync(new GitHubProject("versions", "dotnet"), "heads/master")
                .Result.Object.Sha;

            DependencyUpdateResults updateResults = DependencyUpdateUtils.Update(
                CreateUpdaters().ToArray(),
                CreateDependencyInfos(true, versionsCommitHash).ToArray());

            // Update CurrentRef for each applicable build info used.
            if (!string.IsNullOrEmpty(CurrentRefXmlPath))
            {
                foreach (ITaskItem item in updateResults.UsedInfos
                    .OfType<TaskItemBuildDependencyInfo>()
                    .Distinct()
                    .Select(info => info.SourceItem)
                    .Where(item => !string.IsNullOrEmpty(item.GetMetadata(CurrentRefMetadataName))))
                {
                    UpdateProperty(
                        CurrentRefXmlPath,
                        $"{item.ItemSpec}{CurrentRefMetadataName}",
                        versionsCommitHash);
                }
            }

            return updateResults;
        }

        private void UpdateProperty(string path, string elementName, string newValue)
        {
            const string valueGroup = "valueGroup";
            Action updateAction = FileUtils.GetUpdateFileContentsTask(
                path,
                contents =>
                {
                    Group g = CreateXmlUpdateRegex(elementName, valueGroup)
                        .Match(contents)
                        .Groups[valueGroup];

                    return contents
                        .Remove(g.Index, g.Length)
                        .Insert(g.Index, newValue);
                });
            // There may not be an task to perform for the value to be up to date: allow null.
            updateAction?.Invoke();
        }
    }
}
