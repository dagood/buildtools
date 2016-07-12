// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.VersionTools.Automation.PullRequest
{
    /// <summary>
    /// The interesting parts of a GitHub pull request, as returned by the pull request api.
    /// </summary>
    public class GitHubPullRequest
    {
        public int Number { get; set; }
        public GitHubHead Head { get; set; }
        public GitHubUser User { get; set; }
    }

    public class GitHubHead
    {
        public string Label { get; set; }
        public string Ref { get; set; }
        public GitHubUser User { get; set; }
    }

    public class GitHubUser
    {
        public string Login { get; set; }
    }
}
