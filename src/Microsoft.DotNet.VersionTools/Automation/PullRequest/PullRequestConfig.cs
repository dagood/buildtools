using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Automation.PullRequest
{
    public class PullRequestConfig
    {
        public GitHubAuth GitHubAuth { get; set; }

        public string ProjectRepo { get; set; }
        public string ProjectRepoOwner { get; set; }
        public string ProjectRepoBranch { get; set; }

        public string GitAuthorName { get; set; }

        public IEnumerable<string> NotifyGitHubUsers { get; set; }

        public void SetUnspecifiedToDefault()
        {
            ProjectRepoOwner = ProjectRepoOwner ?? "dotnet";
            ProjectRepoBranch = ProjectRepoBranch ?? "master";
            GitAuthorName = GitAuthorName ?? "dotnet-bot";
        }
    }
}
