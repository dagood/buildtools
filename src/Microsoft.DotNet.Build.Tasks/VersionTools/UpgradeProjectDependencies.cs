using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Upgrade;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpgradeProjectDependencies : Task
    {
        [Required]
        public ITaskItem[] DependencyBuildInfo { get; set; }

        public ITaskItem[] ProjectJsonFiles { get; set; }

        public ITaskItem[] XmlUpgradeStep { get; set; }

        public string ProjectRepo { get; set; }
        public string ProjectRepoOwner { get; set; }
        public string ProjectRepoBranch { get; set; }

        [Required]
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }
        public string GitHubEmail { get; set; }

        /// <summary>
        /// The git author of the upgrade commit. Defaults to the same as GitHubUser.
        /// </summary>
        public string GitHubAuthor { get; set; }

        public ITaskItem[] NotifyGitHubUsers { get; set; }

        public override bool Execute()
        {
            MsBuildTraceListener[] listeners = Trace.Listeners.AddMsBuildTraceListeners(Log);

            IDependencyUpgrader[] upgraders = GetDependencyUpgraders().ToArray();
            BuildInfo[] buildInfos = GetBuildInfos().ToArray();

            var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);

            var upgradePr = new ProjectUpgradePr(
                gitHubAuth,
                ProjectRepo,
                ProjectRepoOwner,
                ProjectRepoBranch,
                GitHubAuthor ?? GitHubUser,
                NotifyGitHubUsers?.Select(item => item.ItemSpec));

            upgradePr.CreateAndSubmitAsync(upgraders, buildInfos).Wait();

            Trace.Listeners.RemoveMsBuildTraceListeners(listeners);

            return true;
        }

        private IEnumerable<IDependencyUpgrader> GetDependencyUpgraders()
        {
            if (ProjectJsonFiles?.Any() ?? false)
            {
                yield return new ProjectJsonUpgrader(ProjectJsonFiles.Select(item => item.ItemSpec));
            }

            if (XmlUpgradeStep != null)
            {
                foreach (ITaskItem step in XmlUpgradeStep)
                {
                    yield return new FileRegexReleaseUpgrader
                    {
                        Path = step.GetMetadata("Path"),
                        Regex = new Regex($@"<{step.GetMetadata("ElementName")}>(?<version>.*)<"),
                        VersionGroupName = "version",
                        BuildInfoName = step.GetMetadata("BuildInfoName")
                    };
                }
            }
        }

        private IEnumerable<BuildInfo> GetBuildInfos()
        {
            return DependencyBuildInfo.Select(buildInfoItem => BuildInfo.Get(
                buildInfoItem.ItemSpec,
                buildInfoItem.GetMetadata("RawUrl")));
        }
    }
}
